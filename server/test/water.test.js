// Acceptance tests for the water-by-compartment model and the trim / list it produces (E6-01).
// Run: node --test server/test/water.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const NC = m.WATER_COMPARTMENTS.length;
const BOW = 0, STERN = NC - 1;
const run = (w, secs) => { for (let i = 0; i < Math.round(secs * m.TICK_RATE); i++) m.waterStep(w); };
const close = (a, b, eps, msg) => assert.ok(Math.abs(a - b) <= eps, (msg || "") + " " + a + " vs " + b);

test("six compartments in a line, from the bow (+z) to the stern (-z), covering the 20 m of the boat without gaps", () => {
  assert.strictEqual(NC, 6);
  for (let i = 0; i < NC; i++) assert.ok(m.WATER_COMPARTMENTS[i].z0 < m.WATER_COMPARTMENTS[i].z1 && m.WATER_COMPARTMENTS[i].volume > 0);
  for (let i = 0; i < NC - 1; i++) close(m.WATER_COMPARTMENTS[i].z0, m.WATER_COMPARTMENTS[i + 1].z1, 1e-9, "contiguous");
  close(m.WATER_COMPARTMENTS[0].z1, m.HALF_Z, 1e-9); close(m.WATER_COMPARTMENTS[NC - 1].z0, -m.HALF_Z, 1e-9);
  assert.strictEqual(m.compartmentAt(9), 0); assert.strictEqual(m.compartmentAt(-1.7), 3);   // the RK-1 panel is in the reactor compartment
  assert.strictEqual(m.compartmentAt(-9), NC - 1);
});

test("an empty boat has no trim and no list", () => {
  const t = m.waterTilt(m.newWater());
  assert.strictEqual(t.trim, 0); assert.strictEqual(t.list, 0);
});

test("water poured in a compartment stays there while the bulkhead openings are closed (nothing is created or lost)", () => {
  const w = m.newWater();
  for (let d = 0; d < NC - 1; d++) m.waterSetDoor(w, d, false);
  assert.strictEqual(m.waterAdd(w, 3, 10, 0), 10);
  run(w, 60);
  assert.strictEqual(w.comps[3].w, 10);
  assert.strictEqual(m.waterTotal(w), 10);
});

test("through an open opening the levels equalise and the volume is conserved to rounding error", () => {
  const w = m.newWater();
  for (let d = 0; d < NC - 1; d++) m.waterSetDoor(w, d, d === 2);                 // only the opening between compartments 3 and 4
  m.waterAdd(w, 2, 20, 0);
  run(w, 60);
  close(m.waterTotal(w), 20, 1e-9, "total");
  close(w.comps[2].w, w.comps[3].w, 0.05, "levels equal after a minute");
  close(w.comps[2].w, 10, 0.05);
  assert.strictEqual(w.comps[0].w + w.comps[1].w + w.comps[4].w + w.comps[5].w, 0, "closed openings: the others stay dry");
});

test("with every opening open the water spreads along the whole boat", () => {
  const w = m.newWater();
  m.waterAdd(w, BOW, 36, 0);
  run(w, 600);
  for (let i = 0; i < NC; i++) close(w.comps[i].w, 6, 0.05, "compartment " + i);
  close(m.waterTotal(w), 36, 1e-9);
});

test("closing an opening during the flood contains it: the water stops at that bulkhead", () => {
  const w = m.newWater();
  m.waterAdd(w, BOW, 30, 0);
  run(w, 5);
  m.waterSetDoor(w, 0, false);                                                   // between the torpedo room and the central post
  const before = w.comps[BOW].w;
  run(w, 120);
  assert.strictEqual(w.comps[BOW].w, before, "nothing flows out any more");
  assert.ok(w.comps[1].w > 0 && w.comps[1].w < 30);
  close(m.waterTotal(w), 30, 1e-9);
});

test("a full compartment rejects what does not fit and counts it, it never exceeds its capacity", () => {
  const w = m.newWater();
  for (let d = 0; d < NC - 1; d++) m.waterSetDoor(w, d, false);
  const cap = m.WATER_COMPARTMENTS[2].volume;
  assert.strictEqual(m.waterAdd(w, 2, cap + 7, 0), cap);
  assert.strictEqual(w.rejected, 7);
  assert.strictEqual(w.comps[2].w, cap);
  assert.strictEqual(m.waterAdd(w, 2, 1, 0), 0);
  run(w, 10);
  assert.strictEqual(w.comps[2].w, cap);
});

test("a bilge pump (waterRemove) takes out at most what is there", () => {
  const w = m.newWater();
  m.waterAdd(w, 4, 3, 0);
  assert.strictEqual(m.waterRemove(w, 4, 2), 2);
  assert.strictEqual(m.waterRemove(w, 4, 5), 1);
  assert.strictEqual(m.waterTotal(w), 0);
});

test("trim: water at the bow puts the bow down, at the stern puts it up, and the effect grows with the mass", () => {
  const a = m.newWater(), b = m.newWater(), c = m.newWater();
  m.waterAdd(a, BOW, 10, 0); m.waterAdd(b, STERN, 10, 0); m.waterAdd(c, BOW, 20, 0);
  assert.ok(m.waterTilt(a).trim > 1, "bow down is positive pitch: " + m.waterTilt(a).trim);
  assert.ok(m.waterTilt(b).trim < -1);
  assert.ok(m.waterTilt(c).trim > m.waterTilt(a).trim);
  close(m.waterTilt(a).trim, -m.waterTilt(b).trim, 1e-9, "symmetric");
});

test("trim: water amidships (symmetric around the centre) does not trim the boat", () => {
  const w = m.newWater();
  m.waterAdd(w, 2, 10, 0); m.waterAdd(w, 3, 10, 0);                                  // the two middle compartments are mirror images
  close(m.waterTilt(w).trim, 0, 1e-9);
});

test("the trim is capped at 12 degrees and the list at 15 degrees, however much water", () => {
  const w = m.newWater();
  for (let i = 0; i < 3; i++) m.waterAdd(w, i, 40, 1);                               // the whole bow half full
  assert.ok(m.waterTilt(w).trim <= m.MAX_TRIM_DEG + 1e-9);
  const l = m.newWater();
  m.waterAdd(l, 2, 12, 1); m.waterAdd(l, 3, 12, 1); m.waterAdd(l, 4, 12, 1); m.waterAdd(l, 1, 12, 1);
  assert.ok(Math.abs(m.waterTilt(l).list) <= m.MAX_LIST_DEG + 1e-9);
});

test("list: a leak on the right leans the boat to the right (negative roll), on the left the other way, and it eases as the compartment fills", () => {
  const r = m.newWater(), l = m.newWater();
  m.waterAdd(r, 3, 8, 1); m.waterAdd(l, 3, 8, -1);
  assert.ok(m.waterTilt(r).list < -0.5);
  assert.ok(m.waterTilt(l).list > 0.5);
  close(m.waterTilt(r).list, -m.waterTilt(l).list, 1e-9);
  const fuller = m.newWater();
  m.waterAdd(fuller, 3, 36, 1);
  const perTonneFull = Math.abs(m.waterTilt(fuller).list) / 36, perTonneLow = Math.abs(m.waterTilt(r).list) / 8;
  assert.ok(perTonneFull < perTonneLow, "water spreads to the middle as the compartment fills");
});

test("list: water that flowed in from a neighbour arrives centred and dilutes the lateral bias", () => {
  const w = m.newWater();
  m.waterAdd(w, 2, 10, 1);
  const before = Math.abs(m.waterTilt(w).list);
  for (let d = 0; d < NC - 1; d++) m.waterSetDoor(w, d, d === 2);
  run(w, 60);
  assert.ok(w.comps[3].side === 0 && w.comps[3].w > 0);
  assert.ok(Math.abs(m.waterTilt(w).list) < before);
});

test("the broadcast view carries every compartment, the tilt offsets and the openings (reconnection resync)", () => {
  const w = m.newWater();
  m.waterAdd(w, BOW, 20, 1);
  m.waterSetDoor(w, 1, false);
  const v = m.waterView(w);
  assert.strictEqual(v.l.length, NC); assert.strictEqual(v.dr.length, NC - 1);
  close(v.l[BOW], 0.5, 1e-9);
  close(v.m, 20, 0.05);
  assert.ok(v.tr > 0 && v.li < 0);
  assert.deepStrictEqual(v.dr, [1, 0, 1, 1, 1]);
  assert.strictEqual(v.ov, 0);
});

test("determinism: the same leaks and the same doors give the same state tick for tick", () => {
  const run1 = () => {
    const w = m.newWater();
    for (let t = 0; t < 900; t++) {
      if (t % 10 === 0) m.waterAdd(w, t % 6, 0.05 * (t % 7), (t % 3) - 1);
      if (t === 300) m.waterSetDoor(w, 2, false);
      if (t === 600) m.waterSetDoor(w, 2, true);
      m.waterStep(w);
    }
    return JSON.stringify(w);
  };
  assert.strictEqual(run1(), run1());
});

// ---- inside the match: the water follows the same loop and changes the cargo's tilt ----
const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });
function setup() {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const sent = [];
  const d = { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) };
  return { state, d, sent, tick: 0 };
}
const tick = (c) => { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, []).state; return c.sent[c.sent.length - 1]; };

test("match: the water state is broadcast every tick and evolves with the match loop", () => {
  const c = setup();
  assert.deepStrictEqual(tick(c).bw.l, [0, 0, 0, 0, 0, 0]);
  m.waterAdd(c.state.water, BOW, 20, 0);
  const first = tick(c).bw;
  for (let i = 0; i < 100; i++) tick(c);
  const later = tick(c).bw;
  assert.ok(later.l[BOW] < first.l[BOW], "the water flows out of the flooded compartment");
  assert.ok(later.l[1] > first.l[1]);
});

test("match: the water's tilt offsets are added to the boat's swell for the cargo (boatUpHorizontal)", () => {
  const base = m.boatUpHorizontal(12.3, 0, 0), tilted = m.boatUpHorizontal(12.3, 5, 0);
  assert.ok(Math.abs(base.z - tilted.z) > 0.05, "5 degrees of trim changes the horizontal part of 'up'");
  assert.strictEqual(m.boatUpHorizontal(12.3).x, base.x);
});
