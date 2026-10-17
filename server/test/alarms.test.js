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

// ---- E3-09b: every alarm of the catalogue can be raised and lowered by driving the state (a table: id -> [raise, lower]) -----------------------------
const K = m.REACTOR_K;
const comp = (i) => m.WATER_COMPARTMENTS[i];
const DRIVE = {
  rx_temp_warn: [(s) => { s.reactor.T = K.tWarn + 5; }, (s) => { s.reactor.T = 300; }],
  rx_temp_crit: [(s) => { s.reactor.T = K.tCrit + 5; }, (s) => { s.reactor.T = 300; }],
  rx_temp_low: [(s) => { s.reactor.T = K.tIn + 2; }, (s) => { s.reactor.T = 300; }],
  rx_valve_low: [(s) => { s.reactor.valves = [0.2, 0.2, 0.2, 0.2]; }, (s) => { s.reactor.valves = [1, 1, 1, 1]; }],
  rx_flow_low: [(s) => { s.reactor.flow = 0.3; }, (s) => { s.reactor.flow = 1.1; }],
  rx_pump0_broken: [(s) => { s.reactor.pumpBroken[0] = true; }, (s) => { s.reactor.pumpBroken[0] = false; }],
  rx_pump1_broken: [(s) => { s.reactor.pumpBroken[1] = true; }, (s) => { s.reactor.pumpBroken[1] = false; }],
  rx_scram_auto: [(s) => { s.reactor.autoScram = true; }, (s) => { s.reactor.autoScram = false; }],
  pw_v_low: [(s) => { s.power.V = 0.3; }, (s) => { s.power.V = 1; }],
  pw_v_dark: [(s) => { s.power.V = 0.05; }, (s) => { s.power.V = 1; }],
  pw_emergency_bus: [(s) => { s.power.V = 0.05; s.power.B = 0.5; }, (s) => { s.power.V = 1; }],
  pw_battery_low: [(s) => { s.power.B = 0.1; }, (s) => { s.power.B = 1; }],
  pw_breaker_tripped: [(s) => { s.power.br[3].tripped = true; }, (s) => { s.power.br[3].tripped = false; }],
  pw_overload: [(s) => { s.power.V = 0.6; s.reactor.E = 6; }, (s) => { s.power.V = 1; }],
  pw_essential_short: [(s) => { s.reactor.eta = 0.5; }, (s) => { s.reactor.eta = 1; }],
  pw_surplus_high: [(s) => { s.power.V = 1; s.reactor.E = 40; }, (s) => { s.reactor.E = 12; }],
  wt_trim_high: [(s) => { m.waterAdd(s.water, 0, 30, 0); }, (s) => { s.water.comps[0].w = 0; }],
  wt_list_high: [(s) => { m.waterAdd(s.water, 2, 20, 1); }, (s) => { s.water.comps[2].w = 0; s.water.comps[2].side = 0; }],
  wt_bilge0_broken: [(s) => { m.bilgeBreak(s.bilge, 0); }, (s) => { m.bilgeRepair(s.bilge, 0); }],
  wt_bilge1_broken: [(s) => { m.bilgeBreak(s.bilge, 1); }, (s) => { m.bilgeRepair(s.bilge, 1); }],
};
for (let i = 0; i < 6; i++) {
  DRIVE["wt_leak_" + (i + 1)] = [(s) => { m.leakCreate(s.leaks, (comp(i).z0 + comp(i).z1) / 2, 1, 2); }, (s) => { s.leaks.list.length = 0; }];
  DRIVE["wt_level_" + (i + 1)] = [(s) => { s.water.comps[i].w = comp(i).volume * 0.3; }, (s) => { s.water.comps[i].w = 0; }];
  DRIVE["wt_flood_" + (i + 1)] = [(s) => { s.water.comps[i].w = comp(i).volume * 0.95; }, (s) => { s.water.comps[i].w = 0; }];
}

test("the catalogue has about forty alarms and the table below drives every one of them", () => {
  assert.ok(m.ALARMS.length >= 36 && m.ALARMS.length <= 45, "alarms: " + m.ALARMS.length);
  assert.deepStrictEqual(Object.keys(DRIVE).sort(), m.ALARMS.map((a) => a.id).sort(), "the test table and the catalogue list the same alarms");
});

test("every alarm rings when its condition holds and is lifted when it ends (healthy state: nothing rings)", () => {
  for (const def of m.ALARMS) {
    const s = quiet("croisiere");
    s.power.V = 1; s.power.B = 1; s.reactor.E = 12; s.reactor.eta = 1; s.reactor.flow = 1.1;
    m.alarmsStep(s, 1);
    assert.deepStrictEqual(on(s).filter((x) => x === def.id), [], def.id + " rings on a healthy boat");
    DRIVE[def.id][0](s); m.alarmsStep(s, 2);
    assert.ok(on(s).includes(def.id), def.id + " does not ring when its condition holds");
    DRIVE[def.id][1](s); m.alarmsStep(s, 3);
    assert.ok(!on(s).includes(def.id), def.id + " is not lifted");
  }
});

test("the red alarms are the real emergencies: critical temperature, automatic SCRAM, dark grid and a flooded compartment", () => {
  const red = m.ALARMS.filter((a) => a.severity === "crit").map((a) => a.id).sort();
  assert.deepStrictEqual(red, ["pw_v_dark", "rx_scram_auto", "rx_temp_crit", ...[1, 2, 3, 4, 5, 6].map((i) => "wt_flood_" + i)].sort());
});

test("a leak left alone rings, then the level, then the flood, in that order, and the bilge pump breakdown rings with the water", () => {
  const s = quiet("croisiere");
  m.waterSetDoor(s.water, 0, false); m.waterSetDoor(s.water, 1, false);               // keep the water in the central post
  m.leakCreate(s.leaks, 7, 1, 3);
  const order = [];
  for (let t = 1; t <= 4000 && order.length < 3; t++) {
    simulate(s, 1, {});
    for (const id of on(s)) if (/^wt_(leak|level|flood)_2$/.test(id) && !order.includes(id)) order.push(id);
  }
  assert.deepStrictEqual(order, ["wt_leak_2", "wt_level_2", "wt_flood_2"]);
});
