// Node tests for the Nakama match logic (no Nakama needed): node --test server/test
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");
const h = m.handlers;

const nk = { binaryToString: (d) => d };
const logger = { info() {} };
function makeDispatcher() {
  const sent = [];
  return { sent, broadcastMessage: (op, data) => sent.push({ op, data: JSON.parse(data) }) };
}
function presence(id) { return { userId: id, sessionId: "s" + id }; }
function input(id, seq, mx, mz) {
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify({ seq, mx, mz }) };
}

test("joins up to 4 players, rejects the 5th", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  for (const id of ["a", "b", "c", "d"]) {
    assert.ok(h.matchJoinAttempt({}, logger, nk, null, 0, state, presence(id), {}).accept);
    state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state;
  }
  assert.strictEqual(h.matchJoinAttempt({}, logger, nk, null, 0, state, presence("e"), {}).accept, false);
});

test("applies one input per tick and acknowledges its sequence", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  const x0 = state.players.a.x;
  state = h.matchLoop({}, logger, nk, d, 1, state, [input("a", 1, 1, 0), input("a", 2, 1, 0)]).state;
  const s1 = d.sent[0].data.players[0];
  assert.strictEqual(s1.seq, 1);
  assert.ok(Math.abs(s1.x - (x0 + m.MOVE_SPEED * m.DT)) < 1e-9);
  state = h.matchLoop({}, logger, nk, d, 2, state, []).state;
  assert.strictEqual(d.sent[1].data.players[0].seq, 2);
});

test("ignores stale/duplicate sequences and clamps to the boat interior", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  for (let t = 1; t <= 200; t++) {
    state = h.matchLoop({}, logger, nk, d, t, state, [input("a", t, 1, 1), input("a", t, 5, 5)]).state;
  }
  const p = d.sent[d.sent.length - 1].data.players[0];
  assert.ok(p.x <= m.HALF_X + 1e-9 && p.z <= m.HALF_Z + 1e-9);
  assert.strictEqual(p.seq, 200);
});

test("diagonal input is normalised (no faster diagonal)", () => {
  const p = { x: 0, z: 0 };
  m.stepPlayer(p, 1, 1);
  const dist = Math.sqrt(p.x * p.x + p.z * p.z);
  assert.ok(Math.abs(dist - m.MOVE_SPEED * m.DT) < 1e-9);
});

test("module registers match + rpc", () => {
  const reg = { rpcs: [], matches: [] };
  const published = [];
  const nkFull = { binaryToString: nk.binaryToString, matchCreate: () => "match-1", storageWrite: (w) => published.push(w) };
  m.InitModule({}, logger, nkFull, {
    registerMatch: (n, hnd) => reg.matches.push(n),
    registerRpc: (n) => reg.rpcs.push(n)
  });
  assert.deepStrictEqual(reg.matches, ["moving_frame"]);
  assert.deepStrictEqual(reg.rpcs, ["get_moving_frame_match"]);
  assert.strictEqual(published.length, 1); // match id published to storage at startup
});

test("backlog after a network stall drains in a few ticks (no permanent lag)", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  let tick = 0, seq = 0;
  const step = (msgs) => { state = h.matchLoop({}, logger, nk, d, ++tick, state, msgs).state; };
  for (let i = 0; i < 5; i++) { seq++; step([input("a", seq, 1, 0)]); }   // healthy: 1 input per tick
  step([]); step([]); step([]);                                           // stall: nothing arrives for 3 ticks
  const burst = []; for (let i = 0; i < 4; i++) { seq++; burst.push(input("a", seq, 1, 0)); }
  step(burst);                                                            // retransmission delivers 4 inputs at once
  step([]);
  assert.strictEqual(state.players.a.queue.length, 0, "queue should be empty");
  assert.strictEqual(state.players.a.seq, seq, "all inputs applied, not lagging behind");
});

test("flooding the server with inputs does not speed a player up", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  let seq = 0;
  const TICKS = 50;
  for (let t = 1; t <= TICKS; t++) {
    const flood = []; for (let i = 0; i < 20; i++) { seq++; flood.push(input("a", seq, 1, 0)); }
    state = h.matchLoop({}, logger, nk, d, t, state, flood).state;
  }
  // At most TICKS inputs of budget (+ the initial burst allowance) can ever be applied.
  const applied = state.players.a.applied;
  assert.ok(applied <= TICKS + m.MAX_ALLOWANCE, "applied " + applied + " > budget");
});

// ---- Two-player interlock (Rule of Two Players) ----
function lockSetup(ids) {
  let { state } = h.matchInit({}, logger, nk, {});
  ids.forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state; });
  return { state, d: makeDispatcher(), tick: 0, seq: {} };
}
function inputAct(ctx, id, act) {
  ctx.seq[id] = (ctx.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify({ seq: ctx.seq[id], mx: 0, mz: 0, act }) };
}
function tickWith(ctx, msgs) { ctx.state = h.matchLoop({}, logger, nk, ctx.d, ++ctx.tick, ctx.state, msgs).state; return ctx.d.sent[ctx.d.sent.length - 1].data.il; }
function place(ctx, id, x, z) { ctx.state.players[id].x = x; ctx.state.players[id].z = z; }

test("interlock: two different players at the two stations within the window succeed", () => {
  const c = lockSetup(["a", "b"]);
  place(c, "a", 0, -9); place(c, "b", 0, 9);
  tickWith(c, [inputAct(c, "a", true)]);
  for (let i = 0; i < 10; i++) tickWith(c, []);           // 1 s later
  const il = tickWith(c, [inputAct(c, "b", true)]);
  assert.strictEqual(il.result, "success");
  assert.strictEqual(il.n, 1);
  assert.strictEqual(il.a, 0); assert.strictEqual(il.b, 0); // both stations released
});

test("interlock: the window is inclusive at exactly 3 s and expires after", () => {
  let c = lockSetup(["a", "b"]);
  place(c, "a", 0, -9); place(c, "b", 0, 9);
  tickWith(c, [inputAct(c, "a", true)]);
  for (let i = 0; i < m.INTERLOCK_WINDOW_TICKS - 1; i++) tickWith(c, []);
  assert.strictEqual(tickWith(c, [inputAct(c, "b", true)]).result, "success"); // exactly 30 ticks later

  c = lockSetup(["a", "b"]);
  place(c, "a", 0, -9); place(c, "b", 0, 9);
  tickWith(c, [inputAct(c, "a", true)]);
  for (let i = 0; i < m.INTERLOCK_WINDOW_TICKS; i++) tickWith(c, []);
  const il = tickWith(c, [inputAct(c, "b", true)]);                           // too late
  assert.strictEqual(il.result, "timeout");
  assert.strictEqual(il.n, 0);
});

test("interlock: one player can never hold both stations", () => {
  const c = lockSetup(["a", "b"]);
  place(c, "a", 0, -9);
  tickWith(c, [inputAct(c, "a", true)]);
  place(c, "a", 0, 9);                                                         // runs to the other end
  const il = tickWith(c, [inputAct(c, "a", true)]);
  assert.notStrictEqual(il.result, "success");
  assert.strictEqual(il.bb, "");
});

test("interlock: out-of-reach presses are ignored; reach is 2 m", () => {
  const c = lockSetup(["a", "b"]);
  place(c, "a", 0, -6.5); place(c, "b", 0, 9);                                 // 2.5 m from station A
  let il = tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(il.ab, "");
  place(c, "a", 0, -7.1);                                                      // 1.9 m
  il = tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(il.ab, "a");
});

test("interlock: remaining time counts down and leaving the match releases the station", () => {
  const c = lockSetup(["a", "b"]);
  place(c, "a", 0, -9);
  tickWith(c, [inputAct(c, "a", true)]);
  let il = tickWith(c, []);
  assert.ok(il.a > 0 && il.a < m.INTERLOCK_WINDOW_TICKS);
  c.state = h.matchLeave({}, logger, nk, null, 0, c.state, [presence("a")]).state;
  il = tickWith(c, []);
  assert.strictEqual(il.ab, "");
});
