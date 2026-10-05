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

// ---- Carried and sliding cargo ----
function inputGrab(ctx, id) {
  ctx.seq[id] = (ctx.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify({ seq: ctx.seq[id], mx: 0, mz: 0, grab: true }) };
}
function cargoOf(ctx, id) { return ctx.d.sent[ctx.d.sent.length - 1].data.cargo.find((c) => c.id === id); }
function tickN(ctx, n) { for (let i = 0; i < n; i++) tickWith(ctx, []); }
// Park the cargo at a given spot with a flat floor assumption: tests that need stillness run at a calm tick.
function calmTick(maxTilt) { // first tick whose tilt keeps loose cargo pinned by static friction
  for (let t = 0; t < 2000; t++) {
    const u = m.boatUpHorizontal(t * m.DT);
    if (Math.sqrt(u.x * u.x + u.z * u.z) * m.GRAVITY < maxTilt) return t;
  }
  throw new Error("no calm tick");
}

test("cargo: grab within reach, follows the carrier, drop leaves it where released", () => {
  const c = lockSetup(["a"]);
  c.tick = calmTick(m.FRICTION * m.GRAVITY * 0.5);
  place(c, "a", 2.0, -3.0);                                 // on top of crate1
  tickWith(c, [inputGrab(c, "a")]);
  assert.deepStrictEqual(cargoOf(c, "crate1").c, ["a"]);
  place(c, "a", -1, 1);
  tickN(c, 1);
  assert.ok(Math.abs(cargoOf(c, "crate1").x - (-1)) < 1e-9 && Math.abs(cargoOf(c, "crate1").z - 1) < 1e-9);
  tickWith(c, [inputGrab(c, "a")]);                         // drop
  assert.deepStrictEqual(cargoOf(c, "crate1").c, []);
});

test("cargo: out of reach cannot be grabbed (reach 1.5 m)", () => {
  const c = lockSetup(["a"]);
  place(c, "a", 2.0, -1.0);                                 // 2 m from crate1
  tickWith(c, [inputGrab(c, "a")]);
  assert.deepStrictEqual(cargoOf(c, "crate1").c, []);
  place(c, "a", 2.0, -1.8);                                 // 1.2 m
  tickWith(c, [inputGrab(c, "a")]);
  assert.deepStrictEqual(cargoOf(c, "crate1").c, ["a"]);
});

test("cargo: loose cargo slides when the boat tilts past the friction angle, and stays inside the boat", () => {
  const c = lockSetup(["a"]);
  let moved = 0, start = null;
  for (let i = 0; i < 700; i++) {                           // 70 s covers several roll/pitch cycles
    tickWith(c, []);
    const o = cargoOf(c, "crate2");
    if (!start) start = { x: o.x, z: o.z };
    moved = Math.max(moved, Math.abs(o.x - start.x) + Math.abs(o.z - start.z));
    assert.ok(Math.abs(o.x) <= m.HALF_X + 1e-9 && Math.abs(o.z) <= m.HALF_Z + 1e-9, "cargo left the boat");
  }
  assert.ok(moved > 0.5, "crate never slid (moved " + moved + " m)");
});

// First tick from which the tilt stays under 90 % of the static-friction limit for the next n ticks.
function calmWindow(n) {
  for (let t = 0; t < 5000; t++) {
    let ok = true;
    for (let k = 0; k <= n && ok; k++) { const u = m.boatUpHorizontal((t + k) * m.DT); ok = Math.sqrt(u.x * u.x + u.z * u.z) * m.GRAVITY < m.FRICTION * m.GRAVITY * 0.9; }
    if (ok) return t;
  }
  throw new Error("no calm window of " + n + " ticks");
}

test("cargo: static friction pins loose cargo on a nearly flat floor", () => {
  const c = lockSetup(["a"]);
  c.tick = calmWindow(6);
  tickWith(c, []);
  const o1 = cargoOf(c, "crate2");
  tickN(c, 4);
  const o2 = cargoOf(c, "crate2");
  assert.ok(Math.abs(o1.x - o2.x) < 1e-9 && Math.abs(o1.z - o2.z) < 1e-9);
});

test("cargo: the heavy flask needs two players grabbing within 3 s", () => {
  const c = lockSetup(["a", "b"]);
  c.tick = calmTick(m.FRICTION * m.GRAVITY * 0.5);
  place(c, "a", -2.0, -5.0); place(c, "b", -1.0, -5.0);
  tickWith(c, [inputGrab(c, "a")]);
  assert.strictEqual(cargoOf(c, "fuel").p, "a");
  assert.deepStrictEqual(cargoOf(c, "fuel").c, []);          // one player alone cannot lift it
  tickN(c, 10);
  tickWith(c, [inputGrab(c, "b")]);
  assert.deepStrictEqual(cargoOf(c, "fuel").c, ["a", "b"]);
  place(c, "a", 0, 0); place(c, "b", 2, 2);
  tickN(c, 1);
  assert.ok(Math.abs(cargoOf(c, "fuel").x - 1) < 1e-9 && Math.abs(cargoOf(c, "fuel").z - 1) < 1e-9); // midpoint
  tickWith(c, [inputGrab(c, "b")]);                          // one lets go: both release
  assert.deepStrictEqual(cargoOf(c, "fuel").c, []);
});

test("cargo: a second carrier arriving after the window does not lift the heavy flask", () => {
  const c = lockSetup(["a", "b"]);
  c.tick = calmTick(m.FRICTION * m.GRAVITY * 0.5);
  place(c, "a", -2.0, -5.0); place(c, "b", -1.0, -5.0);
  tickWith(c, [inputGrab(c, "a")]);
  tickN(c, m.INTERLOCK_WINDOW_TICKS);
  assert.strictEqual(cargoOf(c, "fuel").p, "");              // first grab expired
  const f = cargoOf(c, "fuel"); place(c, "b", f.x, f.z);     // the flask may have slid meanwhile: stand next to it
  tickWith(c, [inputGrab(c, "b")]);
  assert.deepStrictEqual(cargoOf(c, "fuel").c, []);
  assert.strictEqual(cargoOf(c, "fuel").p, "b");             // b is now the first carrier, waiting for a second one
});

test("cargo: a carrier leaving the match releases the cargo", () => {
  const c = lockSetup(["a", "b"]);
  c.tick = calmTick(m.FRICTION * m.GRAVITY * 0.5);
  place(c, "a", 2.0, -3.0);
  tickWith(c, [inputGrab(c, "a")]);
  c.state = h.matchLeave({}, logger, nk, null, 0, c.state, [presence("a")]).state;
  tickN(c, 1);
  assert.deepStrictEqual(cargoOf(c, "crate1").c, []);
});

test("cargo: tilt projection golden values (same numbers asserted in C#: BoatMotionTests)", () => {
  const golden = [
    [0, 0.289523314, 0.0],
    [1.5, 0.0856205745, -0.252473316],
    [3.7, -0.204979999, 0.0467290627],
    [10.25, 0.330693301, -0.0582228990]
  ];
  for (const [t, ux, uz] of golden) {
    const u = m.boatUpHorizontal(t);
    assert.ok(Math.abs(u.x - ux) < 1e-8 && Math.abs(u.z - uz) < 1e-8, "t=" + t + " got " + u.x + "," + u.z);
  }
});

// ---- RK-1 regime selector (E3-03) ----
const REGIME_POS = { x: -2.5, z: -1.7 };
function regimeOf(ctx) { return ctx.state.reactor.regime; }

test("selector: each press within reach turns it one position, with wrap-around (Veille -> Croisiere -> Pleine -> Veille)", () => {
  const c = lockSetup(["a"]);
  place(c, "a", REGIME_POS.x + 0.5, REGIME_POS.z);
  assert.strictEqual(regimeOf(c), "veille");
  const seen = [];
  for (let i = 0; i < 4; i++) { tickWith(c, [inputAct(c, "a", true)]); seen.push(regimeOf(c)); }
  assert.deepStrictEqual(seen, ["croisiere", "pleine", "veille", "croisiere"]);
});

test("selector: out of reach (2 m) the press does nothing", () => {
  const c = lockSetup(["a"]);
  place(c, "a", REGIME_POS.x + 2.5, REGIME_POS.z);                 // 2.5 m away
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(regimeOf(c), "veille");
  place(c, "a", REGIME_POS.x + 1.9, REGIME_POS.z);                 // 1.9 m away
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(regimeOf(c), "croisiere");
});

test("selector: ignored while the reactor is SCRAMmed (restart is a separate procedure)", () => {
  const c = lockSetup(["a"]);
  place(c, "a", REGIME_POS.x + 0.5, REGIME_POS.z);
  m.reactorScram(c.state.reactor);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(regimeOf(c), "veille");
});

test("selector: a single player is enough and the change is visible in the broadcast gauges", () => {
  const c = lockSetup(["a", "b"]);
  place(c, "a", REGIME_POS.x + 0.5, REGIME_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);
  const rx = c.d.sent[c.d.sent.length - 1].data.rx;
  assert.strictEqual(rx.reg, "croisiere");
});

test("interaction key goes to the nearest interactable: the interlock keeps working away from the selector", () => {
  const c = lockSetup(["a"]);
  place(c, "a", 0, -9);                                             // next to interlock station A, far from the selector
  const il = tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(il.ab, "a");
  assert.strictEqual(regimeOf(c), "veille");
});

// ---- SCRAM lever (E3-04): sealed cover, two presses, one player, and the boat sinks afterwards ----
const LEVER_POS = { x: -2.5, z: -2.7 };
function lastView(c) { return c.d.sent[c.d.sent.length - 1].data; }

test("scram lever: the first press only lifts the cover, the second one pulls the lever (single player, no vote)", () => {
  const c = lockSetup(["a"]);
  place(c, "a", LEVER_POS.x + 0.5, LEVER_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(lastView(c).sc, { cv: 1, pl: 0 });
  assert.strictEqual(lastView(c).rx.scram, 0);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(lastView(c).sc, { cv: 1, pl: 1 });
  assert.strictEqual(lastView(c).rx.scram, 1);
});

test("scram lever: all the electricity is lost on the very tick of the pull", () => {
  const c = lockSetup(["a"]);
  assert.ok(lastViewAfter(c).rx.E > 0, "the plant produces electricity before the SCRAM");
  place(c, "a", LEVER_POS.x + 0.5, LEVER_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(lastView(c).rx.E, 0);
});
function lastViewAfter(c) { tickWith(c, []); return lastView(c); }

test("scram lever: the cover falls shut after 6 s, so the next press only lifts it again", () => {
  const c = lockSetup(["a"]);
  place(c, "a", LEVER_POS.x + 0.5, LEVER_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);                                    // cover open
  for (let i = 0; i < m.LEVER_COVER_TICKS; i++) tickWith(c, []);
  assert.deepStrictEqual(lastView(c).sc, { cv: 0, pl: 0 });
  tickWith(c, [inputAct(c, "a", true)]);                                    // lifts the cover again, no SCRAM
  assert.strictEqual(lastView(c).rx.scram, 0);
  assert.strictEqual(lastView(c).sc.cv, 1);
});

test("scram lever: out of reach (1.5 m) nothing happens", () => {
  const c = lockSetup(["a"]);
  place(c, "a", LEVER_POS.x + 1.6, LEVER_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(lastView(c).sc, { cv: 0, pl: 0 });
});

test("scram lever: pulling again once SCRAMmed does nothing, and a restart closes the cover again", () => {
  const c = lockSetup(["a"]);
  place(c, "a", LEVER_POS.x + 0.5, LEVER_POS.z);
  tickWith(c, [inputAct(c, "a", true)]);
  tickWith(c, [inputAct(c, "a", true)]);
  for (let i = 0; i < 5; i++) tickWith(c, [inputAct(c, "a", true)]);       // presses are ignored
  assert.deepStrictEqual(lastView(c).sc, { cv: 1, pl: 1 });
  m.reactorRestart(c.state.reactor);
  tickWith(c, []);
  assert.deepStrictEqual(lastView(c).sc, { cv: 0, pl: 0 });
});

test("scram lever: the nearest control wins, so the selector is not turned from the lever", () => {
  const c = lockSetup(["a"]);
  place(c, "a", LEVER_POS.x, LEVER_POS.z + 0.4);                            // 0.4 m from the lever, 0.6 m from the selector
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(lastView(c).sc.cv, 1);
  assert.strictEqual(c.state.reactor.regime, "veille");
});

test("scram lever: the broadcast always carries the lever and boat state (reconnection resync)", () => {
  const c = lockSetup(["a"]);
  tickWith(c, []);
  assert.deepStrictEqual(lastView(c).sc, { cv: 0, pl: 0 });
  assert.deepStrictEqual(lastView(c).boat, { d: 0, vz: 0 });
});

test("boat: stays level while the reactor runs, then sinks after a SCRAM with a ramped, bounded descent", () => {
  const c = lockSetup(["a"]);
  for (let i = 0; i < 100; i++) tickWith(c, []);
  assert.strictEqual(c.state.boat.depth, 0);
  m.reactorScram(c.state.reactor);
  const rampTicks = m.SINK_RAMP_SECONDS * m.TICK_RATE;
  for (let i = 0; i < rampTicks; i++) tickWith(c, []);
  assert.ok(Math.abs(c.state.boat.vz - m.SINK_RATE_MAX) < 1e-9, "full descent speed after the ramp");
  assert.ok(Math.abs(c.state.boat.depth - 0.5 * m.SINK_RATE_MAX * m.SINK_RAMP_SECONDS) < 0.15, "~2 m after the ramp, got " + c.state.boat.depth);
  for (let i = 0; i < 100; i++) tickWith(c, []);                            // 10 s more at full speed
  assert.ok(Math.abs(c.state.boat.depth - (0.5 * m.SINK_RATE_MAX * m.SINK_RAMP_SECONDS + 10 * m.SINK_RATE_MAX)) < 0.15);
  assert.ok(c.state.boat.vz <= m.SINK_RATE_MAX + 1e-9, "never faster than the cap");
});

test("boat: the automatic protection SCRAM sinks it too, and a restart eases the descent back to zero (it does not rise)", () => {
  const c = lockSetup(["a"]);
  c.state.reactor.autoScram = true; m.reactorScram(c.state.reactor);
  for (let i = 0; i < 300; i++) tickWith(c, []);
  const depth = c.state.boat.depth;
  assert.ok(depth > 2);
  m.reactorRestart(c.state.reactor);
  for (let i = 0; i < 300; i++) tickWith(c, []);
  assert.strictEqual(c.state.boat.vz, 0);
  assert.ok(c.state.boat.depth > depth, "keeps the depth reached while slowing down, never rises");
  const settled = c.state.boat.depth;
  for (let i = 0; i < 50; i++) tickWith(c, []);
  assert.strictEqual(c.state.boat.depth, settled);
});

test("boat: the descent is deterministic (same inputs, same depth)", () => {
  const run = () => { const c = lockSetup(["a"]); m.reactorScram(c.state.reactor); for (let i = 0; i < 250; i++) tickWith(c, []); return c.state.boat.depth; };
  assert.strictEqual(run(), run());
});

// ---- Primary valves and pumps (E3-06): valves are turned by HOLDING the key, pumps by a press, a broken pump will not start ----
function inputHold(ctx, id) {
  ctx.seq[id] = (ctx.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify({ seq: ctx.seq[id], mx: 0, mz: 0, act: false, hold: true }) };
}
function ctl(id) { return m.CONTROLS.find((c) => c.id === id); }
function rxOf(c) { return lastView(c).rx; }

test("valve: holding the key near a wheel opens it at VALVE_TURN_RATE per second, then stops at fully open", () => {
  const c = lockSetup(["a"]);
  c.state.reactor.valves = [0, 0, 0, 0];
  place(c, "a", ctl("valve1").x - 0.5, ctl("valve1").z);
  for (let i = 0; i < 20; i++) tickWith(c, [inputHold(c, "a")]);              // 2 s
  assert.ok(Math.abs(c.state.reactor.valves[1] - 2 * m.VALVE_TURN_RATE) < 1e-9, "valve1=" + c.state.reactor.valves[1]);
  for (let i = 0; i < 100; i++) tickWith(c, [inputHold(c, "a")]);
  assert.strictEqual(c.state.reactor.valves[1], 1);
  assert.deepStrictEqual([c.state.reactor.valves[0], c.state.reactor.valves[2], c.state.reactor.valves[3]], [0, 0, 0]);
});

test("valve: out of reach (1.0 m) holding does nothing; a single press (no hold) does nothing either", () => {
  const c = lockSetup(["a"]);
  c.state.reactor.valves = [0, 0, 0, 0];
  place(c, "a", ctl("valve2").x - 1.1, ctl("valve2").z);
  for (let i = 0; i < 10; i++) tickWith(c, [inputHold(c, "a")]);
  assert.strictEqual(c.state.reactor.valves[2], 0);
  place(c, "a", ctl("valve2").x - 0.5, ctl("valve2").z);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.strictEqual(c.state.reactor.valves[2], 0);
});

test("valve: holding the key near the selector or the lever does not turn any wheel", () => {
  const c = lockSetup(["a"]);
  c.state.reactor.valves = [0, 0, 0, 0];
  place(c, "a", REGIME_POS.x + 0.5, REGIME_POS.z);
  for (let i = 0; i < 10; i++) tickWith(c, [inputHold(c, "a")]);
  assert.deepStrictEqual(c.state.reactor.valves, [0, 0, 0, 0]);
  assert.strictEqual(c.state.reactor.regime, "veille");
});

test("pump: one press toggles run/stop, the flow follows, and it is visible in the broadcast (pu: 1 running, 0 stopped)", () => {
  const c = lockSetup(["a"]);
  place(c, "a", ctl("pump0").x - 0.5, ctl("pump0").z);
  for (let i = 0; i < 50; i++) tickWith(c, []);
  const before = rxOf(c).F;
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(rxOf(c).pu, [0, 1]);
  for (let i = 0; i < 100; i++) tickWith(c, []);
  assert.ok(rxOf(c).F < before, "one pump less, less flow");
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(rxOf(c).pu, [1, 1]);
});

test("pump: a broken pump shows 2, cannot be started, and a repaired one stays stopped until a player starts it", () => {
  const c = lockSetup(["a"]);
  place(c, "a", ctl("pump1").x - 0.5, ctl("pump1").z);
  m.reactorBreakPump(c.state.reactor, 1);
  tickWith(c, []);
  assert.deepStrictEqual(rxOf(c).pu, [1, 2]);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(rxOf(c).pu, [1, 2]);
  m.reactorRepairPump(c.state.reactor, 1);
  tickWith(c, []);
  assert.deepStrictEqual(rxOf(c).pu, [1, 0]);
  tickWith(c, [inputAct(c, "a", true)]);
  assert.deepStrictEqual(rxOf(c).pu, [1, 1]);
});

test("pumps: both stopped leaves only natural circulation and no sudden leak (the core heats up slowly)", () => {
  const c = lockSetup(["a"]);
  m.reactorBreakPump(c.state.reactor, 0); m.reactorBreakPump(c.state.reactor, 1);
  for (let i = 0; i < 100; i++) tickWith(c, []);                              // 10 s
  assert.ok(Math.abs(rxOf(c).F - m.REACTOR_K.flowNat) < 1e-6, "F=" + rxOf(c).F);
  assert.strictEqual(rxOf(c).leak, 0);
  assert.strictEqual(rxOf(c).auto, 0);
});

test("valve and pump state is always in the broadcast (reconnection resync)", () => {
  const c = lockSetup(["a"]);
  c.state.reactor.valves = [0.25, 0.5, 0.75, 1];
  tickWith(c, []);
  assert.deepStrictEqual(rxOf(c).v, [0.25, 0.5, 0.75, 1]);
  assert.deepStrictEqual(rxOf(c).pu, [1, 1]);
});

test("valve: holding sets a deterministic opening (same inputs, same valves, with the seeded drift)", () => {
  const run = () => { const c = lockSetup(["a"]); place(c, "a", ctl("valve0").x - 0.5, ctl("valve0").z); for (let i = 0; i < 300; i++) tickWith(c, [inputHold(c, "a")]); return JSON.stringify(c.state.reactor.valves); };
  assert.strictEqual(run(), run());
});
