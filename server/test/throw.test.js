// Acceptance tests for the mass of carried items (walking slowdown) and for throwing items (E2-04).
// Run: node --test server/test/throw.test.js
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
  state.leaks.nextTick = 1e9;
  state.reactor = m.newReactor(1234, "croisiere");
  const sent = [];
  return { state, d: { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) }, sent, tick: 0, seq: {} };
}
function send(c, id, extra) {
  c.seq[id] = (c.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) };
}
function tick(c, msgs) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; return c.sent[c.sent.length - 1]; }
function ticks(c, n) { for (let i = 0; i < n; i++) tick(c); }
function cargo(c, id) { return c.state.cargo.find((x) => x.id === id); }
function at(c, pid, x, z) { c.state.players[pid].x = x; c.state.players[pid].z = z; }
function take(c, pid, itemId) { const it = cargo(c, itemId); at(c, pid, it.x, it.z); tick(c, [send(c, pid, { take: true })]); }
function walkDistance(c, pid, ticksN) { const p = c.state.players[pid]; const x0 = p.x; for (let i = 0; i < ticksN; i++) tick(c, [send(c, pid, { mx: 1, mz: 0 })]); return p.x - x0; }

// ---- mass and walking speed ----
test("every item has a mass in data, and an empty-handed player walks at full speed", () => {
  for (const k of Object.keys(m.ITEM_KINDS)) assert.ok(m.ITEM_KINDS[k].mass > 0, k);
  const c = setup();
  assert.strictEqual(m.carrySpeedFactor(c.state, "a"), 1);
  at(c, "a", -2, 0);
  close(walkDistance(c, "a", 10), 10 * m.MOVE_SPEED * m.DT, 1e-9);
});

test("carrying slows you down: 1 - 0.012 per kg, a crate costs 14 %, a patch plus the bucket 3.6 %, the pocket counts too", () => {
  const c = setup();
  take(c, "a", "crate1");
  close(m.carrySpeedFactor(c.state, "a"), 1 - 0.012 * m.ITEM_KINDS.crate.mass, 1e-9);
  const d = setup();
  take(d, "a", "patch1"); take(d, "a", "bucket");
  close(m.carrySpeedFactor(d.state, "a"), 1 - 0.012 * (m.ITEM_KINDS.patch.mass + m.ITEM_KINDS.bucket.mass), 1e-9);
  const e = setup();
  take(e, "a", "flashlight"); tick(e, [send(e, "a", { stow: true })]);
  assert.strictEqual(e.state.players.a.hands.p, "flashlight");
  close(m.carrySpeedFactor(e.state, "a"), 1 - 0.012 * m.ITEM_KINDS.flashlight.mass, 1e-9, "a pocketed torch still weighs");
});

test("the distance walked per tick follows the factor on the server (what the client predicts)", () => {
  const c = setup();
  take(c, "a", "crate1");
  at(c, "a", -2.5, 0);
  const f = m.carrySpeedFactor(c.state, "a");
  close(walkDistance(c, "a", 10), 10 * m.MOVE_SPEED * f * m.DT, 1e-9);
  assert.ok(f < 1);
});

test("the heavy flask is carried half each: two players share its 40 kg", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "fuel"); take(c, "b", "fuel");
  close(m.carriedMass(c.state, "a"), m.ITEM_KINDS.fuel.mass / 2, 1e-9);
  close(m.carrySpeedFactor(c.state, "b"), 1 - 0.012 * 20, 1e-9);
});

test("the slowdown is capped at half speed and stepPlayer without a factor is unchanged", () => {
  const c = setup();
  take(c, "a", "crate1");
  c.state.cargo.find((x) => x.id === "crate1").kind = "fuel";                    // pretend a very heavy thing
  c.state.cargo.find((x) => x.id === "crate1").heavy = false;
  c.state.cargo.find((x) => x.id === "crate1").carriers = ["a"];
  m.ITEM_KINDS.fuel.mass = 500;
  try { assert.strictEqual(m.carrySpeedFactor(c.state, "a"), 0.5); } finally { m.ITEM_KINDS.fuel.mass = 40; }
  const p = { x: 0, z: 0 };
  m.stepPlayer(p, 1, 0);
  close(p.x, m.MOVE_SPEED * m.DT, 1e-9);
});

// ---- throwing ----
function holdAndFace(c, itemId, yaw, x, z) {
  take(c, "a", itemId);
  at(c, "a", x, z);
  tick(c, [send(c, "a", { ry: yaw })]);                                          // the heading reaches the server with the input
}
function throwIt(c) { tick(c, [send(c, "a", { hand: "throw" })]); }

test("a thrown one-hand item leaves the hand, flies along the heading in a small arc, lands within a second and then slides to rest", () => {
  const c = setup();
  holdAndFace(c, "bucket", 0, 0, -8);                                            // heading 0 = towards +z (the bow)
  const b = cargo(c, "bucket");
  throwIt(c);
  assert.deepStrictEqual(c.state.players.a.hands.l + c.state.players.a.hands.r, "", "hands are empty");
  assert.strictEqual(b.fly, true); assert.deepStrictEqual(b.carriers, []);
  assert.ok(b.y > 0 && b.vy > 0, "going up");
  let peak = b.y;
  let landedTick = -1;
  for (let i = 0; i < 20 && landedTick < 0; i++) { tick(c); peak = Math.max(peak, b.y); if (!b.fly) landedTick = i; }
  assert.ok(landedTick >= 5 && landedTick <= 9, "lands after ~0.75 s, tick " + landedTick);
  assert.ok(peak > m.THROW_HEIGHT && peak < 1.5, "a small arc, peak " + peak);
  assert.strictEqual(b.y, 0);
  const landedZ = b.z;
  assert.ok(landedZ - (-8) > 3.5 && landedZ - (-8) < 5.5, "flew about 4-5 m: " + (landedZ + 8));
  assert.ok(Math.abs(b.x) < 1e-9, "straight along the heading");
  ticks(c, 100);                                                                 // afterwards it behaves like any loose cargo (it also follows the swell)
  assert.ok(Math.hypot(b.vx, b.vz) <= m.THROW_SPEED * 0.5 + 1e-9, "never faster than its landing speed on the floor");
  assert.ok(Math.abs(b.x) <= m.HALF_X + 1e-9 && Math.abs(b.z) <= m.HALF_Z + 1e-9, "still in the boat");
});

test("the heading decides the direction: a quarter turn sends it to the right (+x), a half turn back (-z)", () => {
  const right = setup();
  holdAndFace(right, "patch1", Math.PI / 2, -2.5, 0);
  throwIt(right);
  const p = cargo(right, "patch1");
  while (p.fly) tick(right);                                                     // compare at the landing: the flight is a straight line
  
  assert.ok(p.x > -2.5 + 1 && Math.abs(p.z) < 1e-6, "went to +x: " + p.x + "," + p.z);
  const back = setup();
  holdAndFace(back, "patch1", Math.PI, 0, 0);
  throwIt(back);
  const q = cargo(back, "patch1");
  while (q.fly) tick(back);
  assert.ok(q.z < -1 && Math.abs(q.x) < 1e-6, "went to -z: " + q.z);
});

test("a thrown item bounces on the wall of the boat (30 % of its speed) and never leaves the boat", () => {
  const c = setup();
  holdAndFace(c, "patch1", Math.PI / 2, 2.5, 0);                                  // 0.5 m from the right wall, heading +x
  const p = cargo(c, "patch1");
  throwIt(c);
  let hitWall = false;
  for (let i = 0; i < 40; i++) { tick(c); assert.ok(Math.abs(p.x) <= m.HALF_X + 1e-9 && Math.abs(p.z) <= m.HALF_Z + 1e-9); if (p.vx < 0) hitWall = true; }
  assert.ok(hitWall, "bounced back");
  assert.ok(p.x <= m.HALF_X, "ended inside the boat");
});

test("nothing is thrown with empty hands, with a two-handed item, with a pocketed torch, or by a throw of the pocket", () => {
  const c = setup();
  throwIt(c);
  take(c, "a", "crate1");
  throwIt(c);
  assert.strictEqual(cargo(c, "crate1").fly, false);
  assert.deepStrictEqual(cargo(c, "crate1").carriers, ["a"], "still carried with both hands");
  tick(c, [send(c, "a", { drop: true })]);
  take(c, "a", "flashlight"); tick(c, [send(c, "a", { stow: true })]);
  throwIt(c);
  assert.strictEqual(cargo(c, "flashlight").fly, false);
  assert.strictEqual(c.state.players.a.hands.p, "flashlight");
});

test("with two small items the last one taken is thrown, the other stays in hand", () => {
  const c = setup();
  take(c, "a", "patch1"); take(c, "a", "bucket");
  throwIt(c);
  assert.strictEqual(cargo(c, "bucket").fly, true);
  assert.deepStrictEqual(cargo(c, "patch1").carriers, ["a"]);
  assert.strictEqual(m.carrySpeedFactor(c.state, "a"), 1 - 0.012 * m.ITEM_KINDS.patch.mass);
});

test("an item in the air cannot be caught, but can be taken again once it has landed", () => {
  const c = setup();
  holdAndFace(c, "bucket", 0, 0, -8);
  const b = cargo(c, "bucket");
  throwIt(c);
  tick(c);
  at(c, "a", b.x, b.z);
  tick(c, [send(c, "a", { take: true })]);
  assert.deepStrictEqual(b.carriers, [], "in flight");
  while (b.fly) tick(c);
  ticks(c, 100);
  at(c, "a", b.x, b.z);
  tick(c, [send(c, "a", { take: true })]);
  assert.deepStrictEqual(b.carriers, ["a"]);
});

test("a thrown patch cannot be used in the air, and the heading is only updated by a real number", () => {
  const c = setup();
  take(c, "a", "patch1");
  const leak = m.leakCreate(c.state.leaks, 2, 1, 2);
  tick(c, [send(c, "a", { ry: 1.25 })]);
  assert.strictEqual(c.state.players.a.yaw, 1.25);
  tick(c, [send(c, "a", { ry: "north" })]); tick(c, [send(c, "a", { ry: null })]); tick(c, [send(c, "a", { ry: Infinity })]); tick(c, [send(c, "a", { ry: NaN })]);
  assert.strictEqual(c.state.players.a.yaw, 1.25, "garbage headings are ignored");
  throwIt(c);
  at(c, "a", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true, use: "item" })]);
  assert.strictEqual(leak.size, 2);
});

test("the flight is deterministic: the same throw ends at exactly the same place", () => {
  const run = () => { const c = setup(); holdAndFace(c, "bucket", 0.7, -1, -3); throwIt(c); ticks(c, 120); const b = cargo(c, "bucket"); return [b.x, b.z, b.y].join(","); };
  assert.strictEqual(run(), run());
});

test("the broadcast carries the height of the cargo: above the floor in flight, 0 at rest", () => {
  const c = setup();
  holdAndFace(c, "patch1", 0, 0, -8);
  throwIt(c);
  const flying = tick(c).cargo.find((x) => x.id === "patch1");
  assert.ok(flying.y > 0);
  ticks(c, 100);
  assert.strictEqual(tick(c).cargo.find((x) => x.id === "patch1").y, 0);
});
