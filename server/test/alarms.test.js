// Acceptance tests for the alarm engine and the reactor alarms (E3-09a). Run: node --test server/test/alarms.test.js
const test = require("node:test");
const assert = require("node:assert");
const { createMatch, simulate, m } = require("./sim-harness.js");

const on = (s) => m.alarmsView(s.alarms).map((x) => x.replace("!", ""));
function quiet(regime) {
  const s = createMatch(["a", "b"]);
  s.reactor = m.newReactor(1234, regime || "croisiere");
  s.leaks.nextTick = 1e9;
  return s;
}
const step = (s, n) => simulate(s, n || 1, {});

test("the catalogue is data: unique ids, a system, a known severity, a manual page that exists, callable conditions", () => {
  const pages = new Set(m.MANUAL_PAGES.map((p) => p.id));
  const ids = new Set();
  for (const a of m.ALARMS) {
    assert.ok(!ids.has(a.id), "duplicate " + a.id); ids.add(a.id);
    assert.ok(a.system && ["warn", "crit"].includes(a.severity), a.id);
    assert.ok(pages.has(a.page), a.id + " points at manual page " + a.page);
    assert.strictEqual(typeof a.when, "function", a.id);
    assert.ok(a.clear === undefined || typeof a.clear === "function", a.id);
  }
  assert.ok(m.ALARMS.length >= 8);
});

test("a quiet reactor rings nothing", () => {
  const s = quiet("croisiere");
  step(s, 100);
  assert.deepStrictEqual(m.alarmsView(s.alarms), []);
});

test("the temperature alarms ring at their thresholds and are lifted below them, with a margin", () => {
  const s = quiet("croisiere");
  s.reactor.T = m.REACTOR_K.tWarn + 1; m.alarmsStep(s, 1);
  assert.deepStrictEqual(on(s), ["rx_temp_warn"]);
  s.reactor.T = m.REACTOR_K.tCrit + 1; m.alarmsStep(s, 2);
  assert.deepStrictEqual(on(s), ["rx_temp_warn", "rx_temp_crit"]);
  s.reactor.T = m.REACTOR_K.tWarn - 1; m.alarmsStep(s, 3);                      // just under the threshold: still ringing (hysteresis)
  assert.deepStrictEqual(on(s), ["rx_temp_warn"]);
  s.reactor.T = m.REACTOR_K.tWarn - 4; m.alarmsStep(s, 4);
  assert.deepStrictEqual(on(s), []);
});

test("no flicker: a value hovering around a threshold raises the alarm once", () => {
  const s = quiet("croisiere");
  for (let i = 0; i < 40; i++) { s.reactor.T = m.REACTOR_K.tWarn + (i % 2 ? 1.5 : -1.5); m.alarmsStep(s, i + 1); }
  assert.deepStrictEqual(on(s), ["rx_temp_warn"]);
  assert.strictEqual(s.alarms.list[0].count, 1, "raised once");
});

test("a drifted valve, a low flow and a broken pump each have their alarm", () => {
  const s = quiet("croisiere");
  s.reactor.valves = [0.3, 0.3, 0.3, 0.3]; m.alarmsStep(s, 1);
  assert.ok(on(s).includes("rx_valve_low"));
  s.reactor.valves = [1, 1, 1, 1]; s.reactor.flow = 0.4; m.alarmsStep(s, 2);
  assert.ok(!on(s).includes("rx_valve_low") && on(s).includes("rx_flow_low"));
  m.reactorBreakPump(s.reactor, 1); m.alarmsStep(s, 3);
  assert.ok(on(s).includes("rx_pump1_broken") && !on(s).includes("rx_pump0_broken"));
  m.reactorRepairPump(s.reactor, 1); m.alarmsStep(s, 4);
  assert.ok(!on(s).includes("rx_pump1_broken"));
});

test("the automatic protection rings in red, and nothing about low flow rings while SCRAMmed", () => {
  const s = quiet("pleine");
  step(s, 5000);                                                                   // 8 min at full power, nobody there: warning, then critical, then automatic SCRAM
  const seen = on(s);
  assert.ok(s.reactor.autoScram && seen.includes("rx_scram_auto"), JSON.stringify(seen));
  assert.ok(!seen.includes("rx_flow_low") && !seen.includes("rx_valve_low"), "no low-flow noise during a SCRAM");
  assert.strictEqual(m.ALARMS.find((a) => a.id === "rx_scram_auto").severity, "crit");
});

test("acknowledging silences one alarm without changing the simulation, and only while it rings", () => {
  const s = quiet("croisiere");
  s.reactor.T = m.REACTOR_K.tWarn + 1; m.alarmsStep(s, 1);
  assert.strictEqual(m.alarmAck(s, "nope"), false);
  assert.strictEqual(m.alarmAck(s, "rx_temp_crit"), false, "not ringing");
  const before = JSON.stringify(s.reactor);
  assert.strictEqual(m.alarmAck(s, "rx_temp_warn"), true);
  assert.deepStrictEqual(m.alarmsView(s.alarms), ["!rx_temp_warn"]);
  assert.strictEqual(JSON.stringify(s.reactor), before);
  s.reactor.T = 300; m.alarmsStep(s, 2); s.reactor.T = m.REACTOR_K.tWarn + 1; m.alarmsStep(s, 3);
  assert.deepStrictEqual(m.alarmsView(s.alarms), ["rx_temp_warn"], "a new occurrence rings again");
});

test("the active alarms are in the broadcast state (a reconnecting client gets them)", () => {
  const h = m.handlers, nk = { binaryToString: (d) => d }, logger = { info() {} };
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [{ userId: "a", sessionId: "sa" }]).state;
  const sent = [];
  state.reactor.T = m.REACTOR_K.tWarn + 5;
  state = h.matchLoop({}, logger, nk, { broadcastMessage: (op, d) => sent.push(JSON.parse(d)) }, 1, state, []).state;
  assert.ok(sent[sent.length - 1].al.includes("rx_temp_warn"));
});
