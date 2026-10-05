// Latency bench for the Rule of Two Players (E4-06): how often do two players who press their commands a given time apart (as THEY see it)
// actually succeed once their inputs travel over links with 150-250 ms of ping, jitter and loss?
// No Nakama, no Unity: the real match handlers run tick by tick, fed by simulated TCP links (in-order delivery, a lost packet is
// retransmitted 200 ms later and holds back everything behind it, like the spike's SimulatedLatencyNetworkService).
// The interphone (voice) is not simulated: it does not exist yet (E8-07). Run: node --test server/test/latency.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });
const DT = m.DT;
const ACTION = m.COUPLED_ACTIONS.find((x) => x.id === "demo2");
const IDX = m.COUPLED_ACTIONS.indexOf(ACTION);
const JITTER = 0.05;                 // +/- 50 ms, uniform, per packet
const RETRANSMIT = 0.2;              // s
const TRIALS = 200;

// Deterministic PRNG (mulberry32)
function rng(seed) {
  let a = seed >>> 0;
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}

// One TCP-like link: send(time) -> delivery time (>= the previous delivery: in order).
function makeLink(pingS, loss, rand) {
  let last = 0;
  return (sendTime) => {
    let t = sendTime + pingS / 2 + (rand() * 2 - 1) * JITTER;
    if (rand() < loss) t += RETRANSMIT;
    if (t < last) t = last;
    last = t;
    return t;
  };
}

// One trial: player a presses at real time T1, player b at T1 + gap. Each client sends one input per 100 ms tick (its own phase), the
// press riding on the first input sent after it. The server applies what has arrived at each of its ticks. Returns true on success.
function trial(state, pingA, pingB, gap, loss, seed) {
  const rand = rng(seed);
  state.cp = m.newCoupled();
  state.players.a.queue = []; state.players.b.queue = [];
  state.players.a.lastQueued = 0; state.players.b.lastQueued = 0; state.players.a.allowance = 0; state.players.b.allowance = 0;
  state.players.a.x = ACTION.a.x; state.players.a.z = ACTION.a.z;
  state.players.b.x = ACTION.b.x; state.players.b.z = ACTION.b.z;
  const T1 = 1.0 + rand() * 0.1, T2 = T1 + gap;
  const clients = [{ id: "a", press: T1, link: makeLink(pingA, loss, rand), phase: rand() * DT, seq: 0 },
                   { id: "b", press: T2, link: makeLink(pingB, loss, rand), phase: rand() * DT, seq: 0 }];
  const inflight = [];                                                       // { id, at, msg }
  const sent = [];
  const d = { broadcastMessage: (op, data) => sent.push(data) };
  const endTime = T2 + 3.0;
  for (const c of clients) {
    for (let t = c.phase; t <= endTime; t += DT) {
      const act = c.press > t - DT && c.press <= t;                          // the press happened since the previous client tick
      c.seq++;
      inflight.push({ at: c.link(t), msg: { opCode: m.OP_INPUT, sender: presence(c.id), data: JSON.stringify({ seq: c.seq, mx: 0, mz: 0, act }) } });
    }
  }
  inflight.sort((x, y) => x.at - y.at);
  let k = 0, tick = 0;
  const startN = state.cp.acts[IDX].count;
  const total = Math.ceil(endTime / DT);
  while (tick < total) {
    tick++;
    const now = tick * DT, msgs = [];
    while (k < inflight.length && inflight[k].at <= now) msgs.push(inflight[k++].msg);
    state = h.matchLoop({}, logger, nk, d, tick, state, msgs).state;
    if (state.cp.acts[IDX].count > startN) return true;
  }
  return false;
}

function freshState() {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a"), presence("b")]).state;
  state.leaks.nextTick = 1e9;                                                // no scheduled leak: the bench measures the interlock only
  return state;
}

function successRate(pingA, pingB, gap, loss) {
  const state = freshState();
  let ok = 0;
  for (let i = 0; i < TRIALS; i++) {
    state.tick = 0;
    if (trial(state, pingA, pingB, gap, loss, 1000 + i * 7 + Math.round(gap * 100) + pingA + 3 * pingB)) ok++;
  }
  return ok / TRIALS;
}

const PAIRS = [[0.15, 0.15], [0.15, 0.25], [0.25, 0.15], [0.25, 0.25]];       // ping of the first presser, of the second presser
const GAPS = [0, 1.0, 2.0, 2.5, 3.0, 3.3, 3.6];
const LOSSES = [0, 0.02];
const table = {};
function rate(pa, pb, gap, loss) {
  const key = [pa, pb, gap, loss].join("/");
  if (!(key in table)) table[key] = successRate(pa, pb, gap, loss);
  return table[key];
}

test("the bench is deterministic: the same trials give exactly the same success rate", () => {
  const a = successRate(0.25, 0.15, 3.0, 0.02), b = successRate(0.25, 0.15, 3.0, 0.02);
  assert.strictEqual(a, b);
});

test("with no latency at all the Rule of Two Players works up to the official 3 s window and fails beyond the tolerance", () => {
  assert.strictEqual(successRate(0, 0, 2.5, 0), 1);
  assert.strictEqual(successRate(0, 0, 3.6, 0), 0);
});

test("report: success rate by ping pair, gap between the two presses and packet loss (written to the output)", () => {
  const lines = ["", "Rule of Two Players under latency — success rate (" + TRIALS + " trials per cell, jitter +/-50 ms, loss retransmit 200 ms)",
    "ping first/second   loss   " + GAPS.map((g) => (g.toFixed(1) + " s").padStart(6)).join(" ")];
  for (const loss of LOSSES) for (const [pa, pb] of PAIRS)
    lines.push((Math.round(pa * 1000) + "/" + Math.round(pb * 1000) + " ms").padEnd(18) + (Math.round(loss * 100) + " %").padStart(5) + "  " +
      GAPS.map((g) => ((rate(pa, pb, g, loss) * 100).toFixed(0) + "%").padStart(6)).join(" "));
  console.log(lines.join("\n"));
  assert.ok(Object.keys(table).length > 0);
});

test("a press gap of 2.5 s or less succeeds at least 99 % of the time at 150-250 ms of ping, with jitter, with or without 2 % loss", () => {
  for (const loss of LOSSES) for (const [pa, pb] of PAIRS) for (const gap of [0, 1.0, 2.0, 2.5])
    assert.ok(rate(pa, pb, gap, loss) >= 0.99, "ping " + pa + "/" + pb + " gap " + gap + " loss " + loss + ": " + rate(pa, pb, gap, loss));
});

test("the official 3.0 s gap succeeds at least 95 % of the time at 150-250 ms of ping", () => {
  for (const loss of LOSSES) for (const [pa, pb] of PAIRS)
    assert.ok(rate(pa, pb, 3.0, loss) >= 0.95, "ping " + pa + "/" + pb + " loss " + loss + ": " + rate(pa, pb, 3.0, loss));
});

// The tolerance does not stretch. Without loss a 3.6 s gap never succeeds. With loss it can in rare cases (a few in a thousand): a lost packet of the
// FIRST presser is retransmitted 200 ms late, so the server sees the two presses closer together than the players pressed them, i.e. the system is
// compensating for the network, which is what the tolerance is for.
test("the tolerance does not stretch: a 3.6 s gap never succeeds without loss, and almost never with 2 % loss", () => {
  for (const [pa, pb] of PAIRS) {
    assert.strictEqual(rate(pa, pb, 3.6, 0), 0, "ping " + pa + "/" + pb);
    assert.ok(rate(pa, pb, 3.6, 0.02) <= 0.02, "ping " + pa + "/" + pb + " with loss: " + rate(pa, pb, 3.6, 0.02));
  }
});
