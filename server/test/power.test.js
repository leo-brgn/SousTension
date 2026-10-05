// Acceptance tests for the electrical network: bus voltage, emergency battery, the 20 breakers (E3-07).
// Run: node --test server/test/power.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });
const close = (a, b, eps, msg) => assert.ok(Math.abs(a - b) <= eps, (msg || "") + " " + a + " vs " + b);

function setup(regime) {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  state.leaks.nextTick = 1e9;
  state.reactor = m.newReactor(1234, regime || "croisiere");
  const sent = [];
  return { state, d: { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) }, sent, tick: 0, seq: 0 };
}
function tick(c, msg) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msg ? [msg] : []).state; return c.sent[c.sent.length - 1]; }
function ticks(c, n) { for (let i = 0; i < n; i++) tick(c); }
function press(c, extra) { c.seq++; return { opCode: m.OP_INPUT, sender: presence("a"), data: JSON.stringify(Object.assign({ seq: c.seq, mx: 0, mz: 0, act: true }, extra || {})) }; }
function idx(id) { return m.BREAKERS.findIndex((b) => b.id === id); }
// a bare state for the unit tests of powerStep: the real reactor is replaced by a fixed electricity output
function bare(E) { return { power: m.newPower(), reactor: { E, pumps: [true, true], scram: false } }; }
const run = (s, secs) => { for (let i = 0; i < Math.round(secs * m.TICK_RATE); i++) m.powerStep(s); };

test("20 breakers, each with its own consumer and a demand; the whole boat asks for less than the plant offers at cruise, more than it offers in Veille", () => {
  assert.strictEqual(m.BREAKERS.length, 20);
  assert.strictEqual(new Set(m.BREAKERS.map((b) => b.id)).size, 20);
  const demand = m.gridDemand(m.newPower());
  const surplus = (reg) => m.gridSurplus(m.newReactor(1234, reg));
  assert.ok(demand > 0 && demand < surplus("croisiere"), "cruise feeds every breaker: " + demand + " vs " + surplus("croisiere"));
  assert.ok(demand < surplus("pleine"));
  assert.strictEqual(surplus("veille"), 0, "in Veille the primary pumps take everything: no surplus");
  assert.strictEqual(m.BREAKERS.filter((b) => b.use === "light").length, 6, "one lights breaker per compartment");
});

test("cruise and full power hold the voltage at 1; Veille makes it decline slowly, never instantly", () => {
  for (const reg of ["croisiere", "pleine"]) { const c = setup(reg); ticks(c, 600); assert.ok(c.state.power.V > 0.99, reg + ": " + c.state.power.V); }
  const c = setup("veille");
  ticks(c, 100);
  assert.ok(c.state.power.V > 0.9, "10 s in Veille: still bright, " + c.state.power.V);
  let prev = c.state.power.V;
  for (let i = 0; i < 20; i++) { ticks(c, 50); assert.ok(c.state.power.V < prev, "declining"); prev = c.state.power.V; }
  assert.ok(prev < 0.45, "after 100 s more it has really dimmed: " + prev);
  assert.ok(Math.abs(prev - Math.exp(-110 / m.TAU_DOWN)) < 0.06, "close to exp(-t/TAU_DOWN): " + prev);
});

test("a SCRAM tears the whole grid down on the very tick: voltage 0, every light dark, emergency lights on", () => {
  const c = setup("croisiere");
  ticks(c, 50);
  assert.deepStrictEqual(tick(c).pw.lt, [3, 3, 3, 3, 3, 3]);
  m.reactorScram(c.state.reactor);
  const pw = tick(c).pw;
  assert.strictEqual(pw.v, 0);
  assert.deepStrictEqual(pw.lt, [0, 0, 0, 0, 0, 0]);
  assert.strictEqual(pw.em, 1);
});

test("the battery carries the emergency lights for ten minutes of darkness, then they go out", () => {
  const c = setup("croisiere");
  m.reactorScram(c.state.reactor);
  ticks(c, 600);                                                               // 60 s
  close(c.state.power.B, 1 - 60 / m.BATTERY_DRAIN_S, 0.01, "battery after 60 s");
  assert.strictEqual(tick(c).pw.em, 1);
  ticks(c, 5500);
  assert.strictEqual(c.state.power.B, 0);
  assert.strictEqual(tick(c).pw.em, 0);
});

test("the battery charges while the voltage is high, and does not drain while the grid is up", () => {
  const c = setup("croisiere");
  c.state.power.B = 0.5;
  ticks(c, 1000);                                                              // 100 s
  close(c.state.power.B, 0.5 + 100 / m.BATTERY_CHARGE_S, 0.02);
  const d = setup("croisiere");
  ticks(d, 600);
  assert.strictEqual(d.state.power.B, 1);
});

test("the voltage rises with the time constant TAU_UP when the plant gives enough surplus", () => {
  const s = bare(30);
  s.power.V = 0;
  run(s, m.TAU_UP);
  close(s.power.V, 1 - Math.exp(-1), 0.02, "one time constant");
});

test("sustained overload: the biggest closed consumer trips first, then the next, until the voltage is acceptable, and the plant never trips below its surplus", () => {
  const s = bare(9.5);                                                         // surplus 1.5 MWe for 5.35 MWe of demand: target 0.28
  const sonar = idx("sonar");
  run(s, 3);
  assert.ok(s.power.br.every((b) => b.closed), "nothing trips instantly");
  s.power.V = 0.28;                                                            // the grid has settled at the overload
  run(s, 6);
  assert.strictEqual(s.power.br[sonar].tripped, true, "the biggest consumer (sonar) trips first");
  run(s, 200);
  const tripped = s.power.br.filter((b) => b.tripped).length;
  assert.ok(tripped >= 3 && tripped < 20, "load is shed step by step: " + tripped);
  assert.ok(s.power.V > m.TRIP_V, "the voltage recovers as load is shed: " + s.power.V);
  run(s, 60);
  assert.strictEqual(s.power.br.filter((b) => b.tripped).length, tripped, "it stops shedding once the voltage is acceptable");
});

test("a voltage that is low only because the plant is spinning up does not trip anything", () => {
  const s = bare(9.5);
  s.power.V = 0;
  run(s, 20);
  assert.ok(s.power.br.every((b) => b.closed));
});

test("no surplus (Veille, SCRAM): nothing to shed, so no breaker trips however dark it gets", () => {
  const s = bare(4.8);                                                         // below the primary pumps' 8 MWe
  s.power.V = 0.1;
  run(s, 120);
  assert.ok(s.power.br.every((b) => b.closed));
});

test("breakers: a press on a tile toggles that breaker only (open sheds its demand), out of reach does nothing, and a re-armed breaker works", () => {
  const c = setup("croisiere");
  const i = idx("galley"), b = m.BREAKERS[i];
  const before = m.gridDemand(c.state.power);
  c.state.players.a.x = b.x - 0.6; c.state.players.a.z = b.z;                 // 0.6 m away, no tile there: out of reach
  tick(c, press(c));
  assert.strictEqual(c.state.power.br[i].closed, true);
  c.state.players.a.x = b.x; c.state.players.a.z = b.z;
  tick(c, press(c));
  assert.strictEqual(c.state.power.br[i].closed, false);
  assert.strictEqual(tick(c).pw.br[i], 0);
  close(m.gridDemand(c.state.power), before - b.demand, 1e-9);
  assert.ok(c.state.power.br.filter((x, k) => k !== i).every((x) => x.closed), "the other breakers are untouched");
  tick(c, press(c));
  assert.strictEqual(c.state.power.br[i].closed, true);
});

test("every tile of the panel resolves to its own breaker when a player stands on it", () => {
  for (let i = 0; i < m.BREAKERS.length; i++) {
    const c = setup("croisiere");
    c.state.players.a.x = m.BREAKERS[i].x; c.state.players.a.z = m.BREAKERS[i].z;
    tick(c, press(c));
    assert.strictEqual(c.state.power.br.filter((x) => !x.closed).length, 1, "tile " + i);
    assert.strictEqual(c.state.power.br[i].closed, false, "tile " + i);
  }
});

test("a tripped breaker shows as tripped (2) until it is re-armed, and re-arming under a persistent overload trips it again", () => {
  const s = bare(9.5);
  s.power.V = 0.28;
  m.breakerTrip(s.power, idx("sonar"));
  assert.strictEqual(m.powerView(s).br[idx("sonar")], 2);
  m.breakerToggle(s.power, idx("sonar"));
  assert.strictEqual(m.powerView(s).br[idx("sonar")], 1);
  run(s, 8);
  assert.strictEqual(s.power.br[idx("sonar")].tripped, true, "the overload is still there: it trips again");
});

test("light bands follow the voltage (white, orange, red, dark) and each compartment follows its own lights breaker", () => {
  assert.deepStrictEqual([0.95, 0.6, 0.3, 0.1].map(m.lightBand), [3, 2, 1, 0]);
  const c = setup("croisiere");
  ticks(c, 10);
  m.breakerToggle(c.state.power, idx("light3"));
  assert.deepStrictEqual(tick(c).pw.lt, [3, 3, 0, 3, 3, 3]);
});

test("the bilge pumps need their own breaker closed and the voltage: open breaker, no pumping", () => {
  const c = setup("croisiere");
  closeDoors(c);
  m.waterAdd(c.state.water, m.BILGE_PUMPS[0].comp, 5, 0);
  c.state.bilge.pumps[0].on = true;
  m.breakerToggle(c.state.power, idx("bilge0"));
  ticks(c, 50);
  assert.strictEqual(c.state.water.comps[m.BILGE_PUMPS[0].comp].w, 5);
  m.breakerToggle(c.state.power, idx("bilge0"));
  ticks(c, 50);
  assert.ok(c.state.water.comps[m.BILGE_PUMPS[0].comp].w < 5);
});
function closeDoors(c) { for (let d = 0; d < c.state.water.doors.length; d++) m.waterSetDoor(c.state.water, d, false); }

test("the grid state is always in the broadcast (reconnection resync)", () => {
  const c = setup("croisiere");
  const pw = tick(c).pw;
  assert.strictEqual(pw.br.length, 20); assert.strictEqual(pw.lt.length, 6);
  assert.ok(pw.v > 0.9 && pw.b === 1 && pw.em === 0);
  assert.ok(pw.dm > 0 && pw.su > pw.dm);
});
