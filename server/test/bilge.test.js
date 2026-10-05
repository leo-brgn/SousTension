// Acceptance tests for the bilge pumps and the bucket (E6-03).
// Run: node --test server/test/bilge.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });
const close = (a, b, eps, msg) => assert.ok(Math.abs(a - b) <= eps, (msg || "") + " " + a + " vs " + b);

function setup(ids) {
  let { state } = h.matchInit({}, logger, nk, {});
  (ids || ["a"]).forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state; });
  const sent = [];
  state.leaks.nextTick = 1e9;                                                  // no scheduled leak unless a test creates one
  state.reactor = m.newReactor(1234, "croisiere");                             // cruise regime: the plant feeds the whole grid (V = 1), as in normal play
  return { state, d: { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) }, sent, tick: 0, seq: {} };
}
function send(c, id, extra) {
  c.seq[id] = (c.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) };
}
function tick(c, msgs) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; return c.sent[c.sent.length - 1]; }
function ticks(c, n) { for (let i = 0; i < n; i++) tick(c); }
function place(c, id, x, z) { c.state.players[id].x = x; c.state.players[id].z = z; }
function closeAllDoors(c) { for (let d = 0; d < c.state.water.doors.length; d++) m.waterSetDoor(c.state.water, d, false); }
function pumpOn(c, i) { c.state.bilge.pumps[i].on = true; }
function cargoOf(c, id) { return c.state.cargo.find((x) => x.id === id); }

test("two bilge pumps, each in its own compartment, off until a player switches them on", () => {
  assert.strictEqual(m.BILGE_PUMPS.length, 2);
  assert.notStrictEqual(m.BILGE_PUMPS[0].comp, m.BILGE_PUMPS[1].comp);
  const c = setup();
  assert.deepStrictEqual(tick(c).bp, [{ s: 0, r: 0 }, { s: 0, r: 0 }]);
  assert.ok(c.state.power.V > m.BILGE_MIN_V, "the grid is up at the start");
});

test("a running pump takes water out of its own compartment at its capacity, nothing is lost or created", () => {
  const c = setup();
  closeAllDoors(c);
  m.waterAdd(c.state.water, m.BILGE_PUMPS[0].comp, 10, 0);
  pumpOn(c, 0);
  ticks(c, 100);                                                               // 10 s
  close(c.state.water.comps[m.BILGE_PUMPS[0].comp].w, 10 - 10 * m.BILGE_CAPACITY, 1e-9);
  assert.strictEqual(tick(c).bp[0].r, 1);
});

test("a pump never takes out more than the compartment holds, and reports that it is no longer moving water", () => {
  const c = setup();
  closeAllDoors(c);
  m.waterAdd(c.state.water, m.BILGE_PUMPS[1].comp, 0.2, 0);
  pumpOn(c, 1);
  ticks(c, 50);
  assert.strictEqual(c.state.water.comps[m.BILGE_PUMPS[1].comp].w, 0);
  assert.deepStrictEqual(tick(c).bp[1], { s: 1, r: 0 });
});

test("the pump only works on its own compartment: water elsewhere stays when the openings are closed", () => {
  const c = setup();
  closeAllDoors(c);
  m.waterAdd(c.state.water, 0, 5, 0);
  pumpOn(c, 0); pumpOn(c, 1);
  ticks(c, 200);
  assert.strictEqual(c.state.water.comps[0].w, 5);
});

test("through the open bulkhead openings a pump also drains its neighbours", () => {
  const c = setup();
  m.waterAdd(c.state.water, m.BILGE_PUMPS[0].comp + 1, 10, 0);                 // the next compartment
  pumpOn(c, 0);
  ticks(c, 600);                                                               // 60 s
  assert.ok(m.waterTotal(c.state.water) < 10 - 0.15 * 30, "total fell: " + m.waterTotal(c.state.water));
});

test("without voltage (SCRAM) the pumps stop, and run again once the grid is back after the restart", () => {
  const c = setup();
  closeAllDoors(c);
  m.waterAdd(c.state.water, m.BILGE_PUMPS[0].comp, 10, 0);
  pumpOn(c, 0);
  m.reactorScram(c.state.reactor);
  tick(c);                                                                     // the pump still saw the old voltage during this one tick
  const dark = c.state.water.comps[m.BILGE_PUMPS[0].comp].w;
  ticks(c, 50);
  assert.strictEqual(c.state.water.comps[m.BILGE_PUMPS[0].comp].w, dark, "no voltage, no pumping");
  assert.strictEqual(tick(c).bp[0].r, 0);
  m.reactorRestart(c.state.reactor);
  for (let i = 0; i < 3000 && c.state.power.V <= m.BILGE_MIN_V; i++) tick(c);    // wait for the plant to feed the grid again
  const before = c.state.water.comps[m.BILGE_PUMPS[0].comp].w;
  ticks(c, 50);
  assert.ok(c.state.water.comps[m.BILGE_PUMPS[0].comp].w < before);
});

test("a press at the switch toggles the pump, out of reach does nothing, one player is enough", () => {
  const c = setup();
  const p0 = m.BILGE_PUMPS[0];
  place(c, "a", p0.x + p0.reach + 0.3, p0.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.bilge.pumps[0].on, false);
  place(c, "a", p0.x + 0.5, p0.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.bilge.pumps[0].on, true);
  assert.strictEqual(tick(c).bp[0].s, 1);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.bilge.pumps[0].on, false);
});

test("a broken pump cannot be started, shows broken, and a repaired one stays off until switched on", () => {
  const c = setup();
  const p1 = m.BILGE_PUMPS[1];
  m.bilgeBreak(c.state.bilge, 1);
  assert.strictEqual(tick(c).bp[1].s, 2);
  place(c, "a", p1.x - 0.5, p1.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.bilge.pumps[1].on, false);
  m.bilgeRepair(c.state.bilge, 1);
  assert.strictEqual(tick(c).bp[1].s, 0);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.bilge.pumps[1].on, true);
});

test("one pump holds a medium leak (and bails the water already aboard), two pumps in two compartments still cannot hold a large one", () => {
  const medium = setup();
  closeAllDoors(medium);
  m.leakCreate(medium.state.leaks, m.WATER_COMPARTMENTS[1].z0 + 1, 1, 2);          // 0.08 m3/s in the compartment of pump 0
  m.waterAdd(medium.state.water, 1, 5, 0);
  pumpOn(medium, 0);
  ticks(medium, 1500);                                                          // 150 s
  assert.ok(medium.state.water.comps[1].w < 0.05, "the pump (0.15) outruns the medium leak (0.08): " + medium.state.water.comps[1].w);

  const large = setup();
  closeAllDoors(large);
  m.leakCreate(large.state.leaks, m.WATER_COMPARTMENTS[1].z0 + 1, 1, 3);           // 0.25 m3/s against one 0.15 m3/s pump
  pumpOn(large, 0); pumpOn(large, 1);
  ticks(large, 300);                                                            // 30 s
  close(large.state.water.comps[1].w, 30 * (m.LEAK_RATES[3] - m.BILGE_CAPACITY), 1e-6, "net +0.1 m3/s");
});

test("bucket: a player carrying it scoops 15 L from the flooded compartment per press, not faster than the cooldown", () => {
  const c = setup();
  closeAllDoors(c);
  const bucket = cargoOf(c, "bucket");
  place(c, "a", bucket.x, bucket.z);
  tick(c, [send(c, "a", { grab: true })]);
  assert.deepStrictEqual(bucket.carriers, ["a"]);
  const comp = m.compartmentAt(c.state.players.a.z);
  m.waterAdd(c.state.water, comp, 2, 0);
  tick(c, [send(c, "a", { act: true })]);
  close(c.state.water.comps[comp].w, 2 - m.BUCKET_VOLUME, 1e-9, "first scoop");
  tick(c, [send(c, "a", { act: true })]);                                       // too soon: refused
  close(c.state.water.comps[comp].w, 2 - m.BUCKET_VOLUME, 1e-9, "cooldown");
  ticks(c, m.BUCKET_COOLDOWN_TICKS);
  tick(c, [send(c, "a", { act: true })]);
  close(c.state.water.comps[comp].w, 2 - 2 * m.BUCKET_VOLUME, 1e-9, "second scoop after the cooldown");
});

test("bucket: nothing to scoop in a dry compartment, and a player without the bucket scoops nothing", () => {
  const c = setup();
  closeAllDoors(c);
  const bucket = cargoOf(c, "bucket");
  place(c, "a", bucket.x, bucket.z);
  const comp = m.compartmentAt(bucket.z);
  m.waterAdd(c.state.water, comp, 1, 0);
  tick(c, [send(c, "a", { act: true })]);                                       // not carrying it
  assert.strictEqual(c.state.water.comps[comp].w, 1);
  tick(c, [send(c, "a", { grab: true })]);
  m.waterRemove(c.state.water, comp, 1);                                         // now dry
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.water.comps[comp].w, 0);
});

test("bucket: the cooldown is per player, two players bail twice as fast (the bucket is shared by dropping and passing it)", () => {
  const c = setup(["a", "b"]);
  closeAllDoors(c);
  const bucket = cargoOf(c, "bucket");
  place(c, "a", bucket.x, bucket.z); place(c, "b", bucket.x, bucket.z);
  tick(c, [send(c, "a", { grab: true })]);
  const comp = m.compartmentAt(bucket.z);
  m.waterAdd(c.state.water, comp, 2, 0);
  tick(c, [send(c, "a", { act: true })]);                                       // a scoops
  tick(c, [send(c, "a", { grab: true })]);                                       // a drops it
  tick(c, [send(c, "b", { grab: true })]);                                       // b takes it
  tick(c, [send(c, "b", { act: true })]);                                        // b scoops at once: its own cooldown
  close(c.state.water.comps[comp].w, 2 - 2 * m.BUCKET_VOLUME, 1e-9);
});

test("the pumps' state is always in the broadcast (reconnection resync)", () => {
  const c = setup();
  pumpOn(c, 0);
  m.waterAdd(c.state.water, m.BILGE_PUMPS[0].comp, 1, 0);
  const v = tick(c).bp;
  assert.deepStrictEqual(v, [{ s: 1, r: 1 }, { s: 0, r: 0 }]);
});
