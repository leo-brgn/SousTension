// Acceptance tests for the mission flow: lobby -> briefing -> run -> debrief (MVP loop, no solo game). Run: node --test server/test/mission.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers, nk = { binaryToString: (d) => d }, logger = { info() {} };
const pr = (id) => ({ userId: id, sessionId: "s" + id });
function lobby(ids, params) {
  let { state } = h.matchInit({}, logger, nk, Object.assign({ lobby: true, debug: true }, params || {}));
  const c = { state, tick: 0, seq: {}, sent: [] };
  c.d = { broadcastMessage: (op, data) => c.sent.push(JSON.parse(data)) };
  for (const id of ids) c.state = h.matchJoin({}, logger, nk, null, 0, c.state, [pr(id)]).state;
  return c;
}
const input = (c, id, extra) => { c.seq[id] = (c.seq[id] || 0) + 1; return { opCode: m.OP_INPUT, sender: pr(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) }; };
const tick = (c, msgs) => { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; return c.sent[c.sent.length - 1]; };
const ticks = (c, n) => { for (let i = 0; i < n; i++) tick(c); };
const ready = (c, ids) => tick(c, ids.map((id) => input(c, id, { rdy: true })));
const secs = (n) => n * m.TICK_RATE;
function startRun(c, ids) { ready(c, ids || c.state.order); ticks(c, secs(m.MISSION_BRIEFING_SECONDS) + 2); assert.strictEqual(c.state.mission.phase, "run"); }

test("a bare match (tests, tools) runs at once with no objective; the real server asks for the lobby", () => {
  const s = h.matchInit({}, logger, nk, {}).state;
  assert.strictEqual(s.mission.phase, "run"); assert.strictEqual(s.mission.minPlayers, 0);
  const l = h.matchInit({}, logger, nk, { lobby: true }).state;
  assert.strictEqual(l.mission.phase, "lobby"); assert.strictEqual(l.mission.minPlayers, 2);
});

test("lobby: the world is frozen, players walk but nothing else answers, and the state says so", () => {
  const c = lobby(["a", "b"]);
  const t0 = c.state.reactor.t;
  tick(c, [input(c, "a", { mx: 1 }), input(c, "b", { act: true, use: "regime", mz: 1 })]);
  ticks(c, 50);
  assert.strictEqual(c.state.reactor.t, t0, "the reactor does not run in the lobby");
  assert.ok(c.state.players.a.x > -1.5, "walking works");
  const v = tick(c).ms;
  assert.strictEqual(v.ph, "lobby"); assert.strictEqual(v.n, 2); assert.strictEqual(v.mp, 2); assert.deepStrictEqual(v.r, []);
  assert.strictEqual(c.state.reactor.regime, "veille", "a press on the selector did nothing");
});

test("no solo game: one ready player alone does not start, two players must both be ready; unready cancels", () => {
  const c = lobby(["a"]);
  ready(c, ["a"]);
  ticks(c, 20);
  assert.strictEqual(c.state.mission.phase, "lobby", "alone: no game");
  c.state = h.matchJoin({}, logger, nk, null, 0, c.state, [pr("b")]).state;
  ticks(c, 5);
  assert.strictEqual(c.state.mission.phase, "lobby", "b is not ready yet");
  assert.deepStrictEqual(tick(c).ms.r, ["a"]);
  tick(c, [input(c, "a", { rdy: false })]);
  ready(c, ["b"]);
  assert.strictEqual(c.state.mission.phase, "lobby", "a took its readiness back");
  ready(c, ["a"]);
  assert.strictEqual(c.state.mission.phase, "briefing");
});

test("briefing counts down 10 s with the goal in the state, then the run starts and the world moves", () => {
  const c = lobby(["a", "b"]);
  ready(c, ["a", "b"]);
  const v = tick(c).ms;
  assert.strictEqual(v.ph, "briefing"); assert.strictEqual(v.g, m.MISSION_GOAL_M); assert.ok(v.bt >= 9 && v.bt <= 10);
  const t0 = c.state.reactor.t;
  ticks(c, secs(5));
  assert.strictEqual(c.state.reactor.t, t0, "still frozen during the briefing");
  ticks(c, secs(5) + 2);
  assert.strictEqual(c.state.mission.phase, "run");
  ticks(c, 10);
  assert.ok(c.state.reactor.t > t0, "the reactor runs now");
});

test("the transit succeeds when the boat has gone the goal distance: debrief with the outcome and three stars for a clean run", () => {
  const c = lobby(["a", "b"]);
  startRun(c);
  ticks(c, 100);
  c.state.prop.dist = m.MISSION_GOAL_M + 1;
  const v = tick(c).ms;
  assert.strictEqual(v.ph, "debrief"); assert.strictEqual(v.res, "success"); assert.strictEqual(v.why, "arrived"); assert.strictEqual(v.stars, 3);
  assert.ok(v.st.t >= 10 && v.st.d >= m.MISSION_GOAL_M, JSON.stringify(v.st));
});

test("a slow run or a hurt boat costs stars, a flooded compartment caps it at one", () => {
  const finish = (setup) => { const c = lobby(["a", "b"]); startRun(c); setup(c); c.state.prop.dist = m.MISSION_GOAL_M + 1; return tick(c).ms.stars; };
  assert.strictEqual(finish(() => {}), 3);
  assert.strictEqual(finish((c) => { c.state.mission.runTicks = secs(m.MISSION_FAST_SECONDS) + 10; }), 2, "slow");
  assert.strictEqual(finish((c) => { c.state.mission.maxFill = 0.6; }), 2, "a compartment was over half full");
  assert.strictEqual(finish((c) => { c.state.mission.maxFill = 0.95; c.state.mission.flooded = true; }), 1, "flooded");
});

test("failure: a compartment kept flooded for a minute, or a boat sunk to the crush depth", () => {
  const c = lobby(["a", "b"]);
  startRun(c);
  m.waterSetDoor(c.state.water, 1, false); m.waterSetDoor(c.state.water, 2, false);
  c.state.water.comps[2].w = m.WATER_COMPARTMENTS[2].volume;
  ticks(c, secs(m.MISSION_FLOOD_FAIL_SECONDS) - 20);
  assert.strictEqual(c.state.mission.phase, "run", "not yet");
  ticks(c, 40);
  const v = tick(c).ms;
  assert.strictEqual(v.ph, "debrief"); assert.strictEqual(v.res, "failure"); assert.strictEqual(v.why, "flooded"); assert.strictEqual(v.stars, 0);
  const d = lobby(["a", "b"]); startRun(d);
  d.state.boat.depth = m.MISSION_CRUSH_DEPTH_M + 1;
  assert.strictEqual(tick(d).ms.why, "depth");
  const e = lobby(["a", "b"]); startRun(e);                       // a flooded compartment that gets bailed in time does not fail the run
  e.state.water.comps[2].w = m.WATER_COMPARTMENTS[2].volume; ticks(e, secs(30)); e.state.water.comps[2].w = 0; ticks(e, secs(60));
  assert.strictEqual(e.state.mission.phase, "run");
});

test("a run never continues with a lone player: it pauses, then resumes when a second one is back", () => {
  const c = lobby(["a", "b"]);
  startRun(c);
  ticks(c, 20);
  c.state = h.matchLeave({}, logger, nk, null, 0, c.state, [pr("b")]).state;
  const t0 = c.state.reactor.t, r0 = c.state.mission.runTicks;
  ticks(c, 50);
  assert.strictEqual(c.state.reactor.t, t0, "world frozen");
  assert.strictEqual(c.state.mission.runTicks, r0, "the run clock stops");
  assert.strictEqual(tick(c).ms.pz, 1);
  c.state = h.matchJoin({}, logger, nk, null, 0, c.state, [pr("b")]).state;
  ticks(c, 10);
  assert.ok(c.state.reactor.t > t0, "resumed");
  assert.strictEqual(tick(c).ms.pz, 0);
});

test("a briefing that loses a player goes back to the lobby", () => {
  const c = lobby(["a", "b"]);
  ready(c, ["a", "b"]);
  c.state = h.matchLeave({}, logger, nk, null, 0, c.state, [pr("b")]).state;
  ticks(c, 3);
  assert.strictEqual(c.state.mission.phase, "lobby");
});

test("debrief, everybody ready: a fresh boat, the crew back at the spawn point with empty hands, and a new briefing", () => {
  const c = lobby(["a", "b"]);
  startRun(c);
  c.state.reactor.regime = "pleine"; c.state.reactor.T = 360; c.state.water.comps[0].w = 5; c.state.players.a.x = 2; c.state.players.a.z = 12;
  c.state.prop.dist = m.MISSION_GOAL_M + 5; tick(c);
  assert.strictEqual(c.state.mission.phase, "debrief");
  ready(c, ["a"]);
  assert.strictEqual(c.state.mission.phase, "debrief", "waits for b");
  ready(c, ["b"]);
  assert.strictEqual(c.state.mission.phase, "briefing");
  assert.strictEqual(c.state.reactor.regime, "veille"); assert.strictEqual(c.state.water.comps[0].w, 0); assert.strictEqual(c.state.prop.dist, 0);
  assert.deepStrictEqual([c.state.players.a.x, c.state.players.a.z], [-1.5, 2]);
  assert.strictEqual(c.state.mission.games, 1);
  ticks(c, secs(m.MISSION_BRIEFING_SECONDS) + 2);
  assert.strictEqual(c.state.mission.phase, "run");
  assert.ok(c.state.mission.runTicks > 0 && c.state.mission.runTicks < 50, "the run clock restarted");
});

test("the fault generator counts its grace period from the start of the run, not from the lobby", () => {
  const c = lobby(["a", "b"], { faults: true });
  ticks(c, secs(120));                                            // two minutes in the lobby
  startRun(c);
  ticks(c, secs(30));
  assert.strictEqual(c.state.faults.started, 0, "no fault in the first minute of the run");
  assert.strictEqual(c.state.faults.nextTick, 60 * m.TICK_RATE, "the grace period is still whole: it counts run ticks");
  c.state.reactor.valves = [1, 1, 1, 1];
  ticks(c, secs(40));
  assert.ok(c.state.faults.started >= 1 || m.faultUnresolved(c.state) >= m.faultBudget(c.state, 0), "a fault, unless the crew is already over its budget");
});

test("the debug console drives the mission: start, goal, win", () => {
  const c = lobby(["a", "b"]);
  const say = (line) => tick(c, [input(c, "a", { dbg: line })]);
  say("mission start"); ticks(c, 3);
  assert.strictEqual(c.state.mission.phase, "briefing");
  ticks(c, secs(m.MISSION_BRIEFING_SECONDS) + 2);
  say("mission goal 100"); assert.strictEqual(c.state.mission.goal, 100);
  say("mission win"); ticks(c, 2);
  assert.strictEqual(c.state.mission.result, "success");
});

test("the debug ghost partner is always ready: one tester can start a game (lobby and debrief)", () => {
  const c = lobby(["a"]);
  tick(c, [input(c, "a", { dbg: "partner on" })]);
  assert.strictEqual(c.state.mission.phase, "lobby", "alone with a ghost: waits for the real player");
  ready(c, ["a"]);
  assert.strictEqual(c.state.mission.phase, "briefing");
  ticks(c, secs(m.MISSION_BRIEFING_SECONDS) + 2);
  c.state.prop.dist = m.MISSION_GOAL_M + 1; tick(c);
  assert.strictEqual(c.state.mission.phase, "debrief");
  ready(c, ["a"]);
  assert.strictEqual(c.state.mission.phase, "briefing", "the ghost is ready for the next game too");
});

test("when the last real player leaves the match is reset: no ghost in a seat, back to the lobby, a fresh boat", () => {
  const c = lobby(["a"]);
  tick(c, [input(c, "a", { dbg: "partner on" })]);
  ready(c, ["a"]);
  ticks(c, secs(m.MISSION_BRIEFING_SECONDS) + 50);
  assert.strictEqual(c.state.mission.phase, "run");
  c.state.reactor.regime = "pleine"; c.state.prop.dist = 700;
  c.state = h.matchLeave({}, logger, nk, null, 0, c.state, [pr("a")]).state;
  assert.strictEqual(c.state.order.length, 0, "the ghost left with the last human");
  assert.strictEqual(c.state.players.ghost, undefined);
  assert.strictEqual(c.state.mission.phase, "lobby");
  assert.strictEqual(c.state.mission.minPlayers, 2, "still a lobby match");
  assert.strictEqual(c.state.reactor.regime, "veille"); assert.strictEqual(c.state.prop.dist, 0);
  c.state = h.matchJoin({}, logger, nk, null, 0, c.state, [pr("b")]).state;
  assert.deepStrictEqual(c.state.order, ["b"]);
  assert.deepStrictEqual([c.state.players.b.x, c.state.players.b.z], [-1.5, 2]);
});

test("a bare match is not reset to a lobby when its players leave (tests and tools keep their rules)", () => {
  const s = h.matchInit({}, logger, nk, {}).state;
  let st = h.matchJoin({}, logger, nk, null, 0, s, [pr("a")]).state;
  st = h.matchLeave({}, logger, nk, null, 0, st, [pr("a")]).state;
  assert.strictEqual(st.mission.phase, "run"); assert.strictEqual(st.mission.minPlayers, 0);
});
