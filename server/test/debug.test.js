// Acceptance tests for the debug tools: a command channel that is off by default (E1-09).
// Run: node --test server/test/debug.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });

function setup(opts) {
  opts = opts || {};
  let { state } = h.matchInit(opts.ctx || {}, logger, nk, opts.params || {});
  (opts.ids || ["a"]).forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state; });
  state.leaks.nextTick = 1e9;
  state.reactor = m.newReactor(1234, "croisiere");
  const sent = [], debugMsgs = [];
  const d = { broadcastMessage: (op, data, presences) => { if (op === m.OP_DEBUG) debugMsgs.push({ to: (presences || []).map((p) => p.userId), lines: JSON.parse(data).lines }); else sent.push(JSON.parse(data)); } };
  return { state, d, sent, debugMsgs, tick: 0, seq: {} };
}
const on = (extra) => setup(Object.assign({ params: { debug: true } }, extra || {}));
function send(c, id, extra) {
  c.seq[id] = (c.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) };
}
function tick(c, msgs) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; }
function cmd(c, line, pid) { c.debugMsgs.length = 0; tick(c, [send(c, pid || "a", { dbg: line })]); return c.debugMsgs.length ? c.debugMsgs[0].lines.join("\n") : ""; }
function resetParams() { for (const k of Object.keys(m.DEBUG_PARAMS)) m.DEBUG_PARAMS[k].set(m.DEBUG_PARAMS[k].def); }

test("disabled by default: a command is ignored (the game is untouched) and the sender is told debug is off", () => {
  const c = setup();
  const reply = cmd(c, "scram");
  assert.strictEqual(c.state.reactor.scram, false);
  assert.match(reply, /debug disabled/);
  cmd(c, "set water_flow 5"); cmd(c, "ff 60"); cmd(c, "leak 2 3");
  assert.strictEqual(m.DEBUG_PARAMS.water_flow.get(), m.DEBUG_PARAMS.water_flow.def);
  assert.strictEqual(c.state.debug.skew, 0); assert.strictEqual(c.state.leaks.list.length, 0);
});

test("debug mode comes from the Nakama environment variable SOUSTENSION_DEBUG=1 or the match parameter, and nothing else", () => {
  assert.strictEqual(m.debugEnabled({ env: { SOUSTENSION_DEBUG: "1" } }, {}), true);
  assert.strictEqual(m.debugEnabled({ env: { SOUSTENSION_DEBUG: "0" } }, {}), false);
  assert.strictEqual(m.debugEnabled({ env: {} }, {}), false);
  assert.strictEqual(m.debugEnabled({}, {}), false);
  assert.strictEqual(m.debugEnabled(undefined, undefined), false);
  assert.strictEqual(m.debugEnabled({}, { debug: true }), true);
  assert.strictEqual(m.debugEnabled({}, { debug: "yes" }), false, "only a real true");
  assert.strictEqual(setup({ ctx: { env: { SOUSTENSION_DEBUG: "1" } } }).state.debug.enabled, true);
});

test("replies go to the sender only, as one debug message, and the game state broadcast is unchanged", () => {
  const c = on({ ids: ["a", "b"] });
  cmd(c, "help", "a");
  assert.strictEqual(c.debugMsgs.length, 1);
  assert.deepStrictEqual(c.debugMsgs[0].to, ["a"]);
  assert.ok(c.debugMsgs[0].lines.length >= 2);
  const before = c.debugMsgs.length;
  tick(c);
  assert.strictEqual(c.debugMsgs.length, before, "no reply without a command");
});

test("state summarises the game (regime, grid, water, leaks, boat, manual) with the digest", () => {
  const c = on();
  const text = cmd(c, "state");
  for (const word of ["tick", "reactor", "croisiere", "grid", "water", "boat", "manual", "digest"]) assert.ok(text.includes(word), word);
  assert.ok(text.includes(m.stateDigest(c.state).slice(0, 4)) || /digest [0-9a-f]{16}/.test(text));
});

test("tp puts you where you ask, inside the boat; bad arguments get a usage message", () => {
  const c = on();
  cmd(c, "tp 1.5 -4");
  assert.deepStrictEqual([c.state.players.a.x, c.state.players.a.z], [1.5, -4]);
  cmd(c, "tp 99 -99");
  assert.deepStrictEqual([c.state.players.a.x, c.state.players.a.z], [m.HALF_X, -m.HALF_Z]);
  assert.match(cmd(c, "tp 1"), /usage/);
  assert.match(cmd(c, "tp a b"), /usage/);
});

test("regime, scram and restart act on the reactor, restart without the procedure", () => {
  const c = on();
  cmd(c, "regime pleine");
  assert.strictEqual(c.state.reactor.regime, "pleine");
  assert.match(cmd(c, "regime turbo"), /usage/);
  cmd(c, "scram");
  assert.strictEqual(c.state.reactor.scram, true);
  c.state.lever.reset = true;
  cmd(c, "restart");
  assert.strictEqual(c.state.reactor.scram, false);
  assert.strictEqual(c.state.lever.reset, false);
});

test("leak opens a leak in the compartment you name (1 = bow .. 6 = stern), with its size and wall; unleak closes them; bad arguments are refused", () => {
  const c = on();
  cmd(c, "leak 3 2 g");
  assert.strictEqual(c.state.leaks.list.length, 1);
  const k = c.state.leaks.list[0];
  assert.strictEqual(k.comp, 2); assert.strictEqual(k.size, 2); assert.strictEqual(k.side, -1);
  cmd(c, "leak 6 1");
  assert.strictEqual(c.state.leaks.list[1].comp, 5); assert.strictEqual(c.state.leaks.list[1].side, 1);
  for (const bad of ["leak 0 1", "leak 7 1", "leak 2 0", "leak 2 4", "leak x 1", "leak 2", "leak 1.5 1"]) assert.match(cmd(c, bad), /usage/, bad);
  assert.strictEqual(c.state.leaks.list.length, 2);
  assert.match(cmd(c, "unleak"), /2 leak/);
  assert.strictEqual(c.state.leaks.list.length, 0);
});

test("break and repair the primary pumps and the bilge pumps; trip a breaker by its id; unknown ids list the valid ones", () => {
  const c = on();
  cmd(c, "break pump 2");
  assert.strictEqual(c.state.reactor.pumpBroken[1], true);
  cmd(c, "repair pump 2");
  assert.strictEqual(c.state.reactor.pumpBroken[1], false);
  cmd(c, "break bilge 1");
  assert.strictEqual(c.state.bilge.pumps[0].broken, true);
  cmd(c, "repair bilge 1");
  assert.strictEqual(c.state.bilge.pumps[0].broken, false);
  assert.match(cmd(c, "break pump 3"), /usage/); assert.match(cmd(c, "break turbine 1"), /usage/);
  cmd(c, "trip galley");
  assert.strictEqual(c.state.power.br[m.BREAKERS.findIndex((b) => b.id === "galley")].tripped, true);
  assert.match(cmd(c, "trip nothing"), /galley/);
});

test("water floods a compartment, bring puts an item in front of you (even a thrown or carried one)", () => {
  const c = on();
  c.state.water.doors.forEach((d) => { d.open = false; });                       // keep the water where it is poured
  cmd(c, "water 4 3.5");
  assert.ok(Math.abs(c.state.water.comps[3].w - 3.5) < 1e-9);
  assert.match(cmd(c, "water 9 1"), /usage/); assert.match(cmd(c, "water 2 -1"), /usage/);
  c.state.players.a.x = 2; c.state.players.a.z = -6;
  cmd(c, "bring crate1");
  const crate = c.state.cargo.find((x) => x.id === "crate1");
  assert.ok(Math.abs(crate.x - 2) < 0.1 && Math.abs(crate.z + 6) < 0.1, "in front of you (it may start to slide with the swell): " + crate.x + "," + crate.z);
  assert.match(cmd(c, "bring nothing"), /crate1/);
});

test("get and set work on a whitelist with bounds; default restores; anything else is refused", () => {
  const c = on();
  try {
    assert.match(cmd(c, "get water_flow"), /water_flow = 2/);
    cmd(c, "set water_flow 5");
    assert.strictEqual(m.DEBUG_PARAMS.water_flow.get(), 5);
    assert.match(cmd(c, "set water_flow 999"), /between/);
    assert.match(cmd(c, "set water_flow abc"), /usage/);
    assert.match(cmd(c, "set no_such_param 1"), /unknown parameter/);
    assert.match(cmd(c, "get constructor"), /unknown parameter/, "no access to anything outside the list");
    assert.match(cmd(c, "set __proto__ 1"), /unknown parameter/);
    cmd(c, "set water_flow default");
    assert.strictEqual(m.DEBUG_PARAMS.water_flow.get(), 2);
    cmd(c, "set leak_large 0.5");
    assert.strictEqual(m.LEAK_RATES[3], 0.5);
    cmd(c, "set reactor.tauE 35");
    assert.strictEqual(m.REACTOR_K.tauE, 35);
    assert.match(cmd(c, "set reactor.tauE 1"), /between/, "a tenth to ten times the default");
    assert.ok(!("reactor.regimes" in m.DEBUG_PARAMS), "objects are not tweakable");
  } finally { resetParams(); }
  assert.strictEqual(m.REACTOR_K.tauE, 70);
});

test("a tweaked parameter really changes the simulation (the valve wheel opens twice as fast)", () => {
  const c = on();
  try {
    cmd(c, "set valve_turn_rate 0.5");
    c.state.reactor.valves = [0, 0, 0, 0];
    const v = m.CONTROLS.find((k) => k.id === "valve0");
    c.state.players.a.x = v.x - 0.5; c.state.players.a.z = v.z;
    for (let i = 0; i < 10; i++) tick(c, [send(c, "a", { hold: true, use: "valve0" })]);
    assert.ok(Math.abs(c.state.reactor.valves[0] - 0.5) < 1e-9, "10 ticks at 0.5 per second");
  } finally { resetParams(); }
});

test("ff advances the simulation by the given time in one go, exactly like the same number of ticks, with one consistent clock", () => {
  const a = on(), b = on();
  cmd(a, "ff 5");                                                                // 50 more ticks, run right away
  assert.strictEqual(a.state.tick, 51);
  assert.strictEqual(a.state.debug.skew, 50);
  tick(b, [send(b, "a", {})]);                                                   // the plain way: the same first input without a command ...
  for (let i = 0; i < 50; i++) tick(b);                                          // ... then 50 more ticks
  assert.strictEqual(m.stateDigest(a.state), m.stateDigest(b.state), "same game state");
  tick(a);
  assert.strictEqual(a.state.tick, 52, "the next match tick continues from the skipped time");
  assert.ok(a.sent[a.sent.length - 1].t > 5, "the broadcast clock includes the skipped time");
  for (const bad of ["ff 0", "ff -3", "ff 301", "ff x", "ff"]) assert.match(cmd(a, bad), /usage/, bad);
});

test("ff makes slow phenomena testable: a leak left for two minutes floods the compartment", () => {
  const c = on();
  c.state.water.doors.forEach((d) => { d.open = false; });
  cmd(c, "leak 3 3");
  cmd(c, "ff 120");
  assert.ok(c.state.water.comps[2].w > 25, "0.25 m3/s for two minutes: " + c.state.water.comps[2].w);
});

test("unknown commands, empty or oversized lines and a non-string dbg field are harmless", () => {
  const c = on();
  assert.match(cmd(c, "frobnicate"), /unknown command/);
  const before = m.stateDigest(c.state);
  tick(c, [send(c, "a", { dbg: "" })]);
  tick(c, [send(c, "a", { dbg: "x".repeat(121) })]);
  tick(c, [send(c, "a", { dbg: 42 })]);
  tick(c, [send(c, "a", { dbg: null })]);
  assert.strictEqual(c.debugMsgs.length, 1, "only the unknown-command reply from before");
  assert.ok(before !== undefined);
});

test("the debug console is not game state: the digest ignores it, and with debug off the game is exactly the normal one", () => {
  const c = on();
  const d1 = m.stateDigest(c.state);
  c.state.debug.out.push({ to: "a", text: "x" }); c.state.debug.skew = 99; c.state.debug.enabled = !c.state.debug.enabled;
  assert.strictEqual(m.stateDigest(c.state), d1);
  const normal = setup(), flagged = on();
  for (let i = 0; i < 200; i++) { tick(normal); tick(flagged); }
  assert.strictEqual(m.stateDigest(normal.state), m.stateDigest(flagged.state), "enabling the channel alone changes nothing");
});
