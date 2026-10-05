// Acceptance tests for hull leaks, their scheduling and their repair with hull patches (E6-02).
// Run: node --test server/test/leaks.test.js
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
function cargoOf(c, id) { return c.state.cargo.find((x) => x.id === id); }
// the player picks the patch up (grab key) next to it, then stands at (x, z)
function takePatch(c, id, patchId, x, z) {
  const p = cargoOf(c, patchId);
  place(c, id, p.x, p.z);
  tick(c, [send(c, id, { grab: true })]);
  assert.deepStrictEqual(p.carriers, [id], "the patch is carried");
  place(c, id, x, z);
}

test("a leak is on the hull wall of the compartment that contains it, left or right", () => {
  const L = { list: [], nextId: 1, rng: 1, nextTick: 0 };
  const a = m.leakCreate(L, -1.7, 1, 2), b = m.leakCreate(L, 8, -1, 1);
  assert.strictEqual(a.comp, 3); assert.strictEqual(a.x, m.HALF_X); assert.strictEqual(a.side, 1);
  assert.strictEqual(b.comp, 0); assert.strictEqual(b.x, -m.HALF_X); assert.strictEqual(b.side, -1);
  assert.strictEqual(a.type, "plate"); assert.strictEqual(b.type, "rivet");
});

test("each size pours at its flow rate: the boat takes in exactly rate x time of water (nothing lost, nothing created)", () => {
  for (const size of [1, 2, 3]) {
    const c = setup();
    c.state.leaks.nextTick = 1e9;                                              // no scheduled leak in this test
    m.leakCreate(c.state.leaks, 2, 1, size);
    ticks(c, 100);                                                             // 10 s
    close(m.waterTotal(c.state.water), m.LEAK_RATES[size] * 10, 1e-9, "size " + size);
  }
  assert.ok(m.LEAK_RATES[1] < m.LEAK_RATES[2] && m.LEAK_RATES[2] < m.LEAK_RATES[3]);
});

test("a leak on the right wall lists the boat to the right (negative roll), on the left the other way", () => {
  const side = (s) => { const c = setup(); c.state.leaks.nextTick = 1e9; closeAllDoors(c); m.leakCreate(c.state.leaks, 2, s, 3); ticks(c, 100); return m.waterTilt(c.state.water).list; };
  assert.ok(side(1) < -0.3 && side(-1) > 0.3);
  close(side(1), -side(-1), 1e-9);
});

test("PROTO: no leak before 90 s, then exactly one, and only one at a time however long it is left alone", () => {
  const c = setup();
  ticks(c, m.LEAK_FIRST_TICK - 1);
  assert.strictEqual(c.state.leaks.list.length, 0);
  assert.deepStrictEqual(c.sent[c.sent.length - 1].lk, []);
  tick(c);
  assert.strictEqual(c.state.leaks.list.length, 1);
  ticks(c, 3000);
  assert.strictEqual(c.state.leaks.list.length, 1);
});

test("the scheduled leak is deterministic from the seed: same compartment, wall, position and size every run", () => {
  const first = () => { const c = setup(); ticks(c, m.LEAK_FIRST_TICK); return JSON.stringify(c.sent[c.sent.length - 1].lk); };
  assert.strictEqual(first(), first());
  assert.ok(first().length > 10);
});

test("repair: carry a patch to the leak and press the interaction key; one patch lowers the leak one size and is used up", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const leak = m.leakCreate(c.state.leaks, 2, 1, 3);
  takePatch(c, "a", "patch1", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(leak.size, 2);
  const p = cargoOf(c, "patch1");
  assert.strictEqual(p.active, false); assert.deepStrictEqual(p.carriers, []);
  assert.strictEqual(c.sent[c.sent.length - 1].cargo.find((x) => x.id === "patch1").a, 0);
});

test("repair: without a patch in hand, or out of reach, the press does nothing", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const leak = m.leakCreate(c.state.leaks, 2, 1, 2);
  place(c, "a", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true })]);                                      // no patch
  assert.strictEqual(leak.size, 2);
  takePatch(c, "a", "patch1", leak.x - (m.LEAK_REACH + 0.3), leak.z);            // too far
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(leak.size, 2);
  assert.strictEqual(cargoOf(c, "patch1").active, true, "the patch is not used up by a failed attempt");
  assert.deepStrictEqual(cargoOf(c, "patch1").carriers, ["a"]);
});

test("repair: carrying something else (a crate) does not repair", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const leak = m.leakCreate(c.state.leaks, -8, -1, 1);
  const crate = cargoOf(c, "crate1");
  place(c, "a", crate.x, crate.z);
  tick(c, [send(c, "a", { grab: true })]);
  place(c, "a", leak.x + 0.5, leak.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(leak.size, 1);
});

test("a large leak needs three patches (large -> medium -> small -> sealed); once sealed the water stops and the next leak is scheduled later", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const leak = m.leakCreate(c.state.leaks, 2, 1, 3);
  for (const pid of ["patch1", "patch2", "patch3"]) {
    assert.strictEqual(c.state.leaks.list.length, 1, "still leaking before " + pid);
    takePatch(c, "a", pid, leak.x - 0.5, leak.z);
    tick(c, [send(c, "a", { act: true })]);
  }
  assert.strictEqual(c.state.leaks.list.length, 0);
  const total = m.waterTotal(c.state.water);
  ticks(c, 100);
  close(m.waterTotal(c.state.water), total, 1e-9, "no more water comes in");
  const gap = c.state.leaks.nextTick - c.tick;
  assert.ok(gap >= 590 && gap <= 1200, "next leak in " + (gap / 10) + " s");
});

test("a used patch is back in the toolbox 30 s later, and cannot be grabbed while it is away", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const leak = m.leakCreate(c.state.leaks, 2, 1, 1);
  takePatch(c, "a", "patch1", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true })]);
  const p = cargoOf(c, "patch1");
  place(c, "a", p.homeX, p.homeZ);
  tick(c, [send(c, "a", { grab: true })]);
  assert.deepStrictEqual(p.carriers, [], "cannot take a patch that is not in the world");
  tick(c, [send(c, "a", { grab: true })]);                                     // that press took the neighbouring patch2: drop it again
  ticks(c, m.PATCH_RESPAWN_TICKS);
  assert.strictEqual(p.active, true);
  close(p.x, p.homeX, 0.5); close(p.z, p.homeZ, 0.5);                     // back at the toolbox (it may start sliding on a tilted floor like any loose cargo)
  p.x = p.homeX; p.z = p.homeZ; place(c, "a", p.homeX - 0.3, p.homeZ);          // standing next to the toolbox, patch1 the nearest one
  tick(c, [send(c, "a", { grab: true })]);
  assert.deepStrictEqual(p.carriers, ["a"]);
});

test("the broadcast lists every open leak (position, size, type) so a reconnecting player sees them", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  const k = m.leakCreate(c.state.leaks, -5, -1, 2);
  const v = tick(c).lk;
  assert.strictEqual(v.length, 1);
  assert.deepStrictEqual([v[0].id, v[0].c, v[0].x, v[0].s, v[0].t], [k.id, 4, -m.HALF_X, 2, "plate"]);
});

test("flooding a compartment through an unrepaired leak ends with the compartment full and the excess rejected, never more water than fits", () => {
  const c = setup();
  c.state.leaks.nextTick = 1e9;
  closeAllDoors(c);
  m.leakCreate(c.state.leaks, 2, 1, 3);
  ticks(c, 2000);                                                              // 200 s at 0.25 m3/s = 50 m3 into a 40 m3 compartment
  close(c.state.water.comps[2].w, m.WATER_COMPARTMENTS[2].volume, 1e-9);
  assert.ok(c.state.water.rejected > 9);
});
