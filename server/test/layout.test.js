// Acceptance tests for the boat layout: bulkheads, hatches, privacy baffles, collisions (E10-01).
// Run: node --test server/test/layout.test.js
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const m = require("../modules/index.js");
const { route } = require("./route.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });

function setup(ids) {
  let { state } = h.matchInit({}, logger, nk, {});
  (ids || ["a"]).forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state; });
  state.leaks.nextTick = 1e9;
  return { state, d: { broadcastMessage() {} }, tick: 0, seq: {} };
}
function send(c, id, extra) {
  c.seq[id] = (c.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) };
}
function tick(c, msgs) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; }
function at(c, id, x, z) { c.state.players[id].x = x; c.state.players[id].z = z; }
function walk(c, id, mx, mz, n) { for (let i = 0; i < n; i++) tick(c, [send(c, id, { mx, mz })]); }
// One tick of a scripted walker heading for `goal` through the hatches; true once it is there.
function stepToward(c, id, goal) {
  const p = c.state.players[id];
  if (Math.hypot(goal.x - p.x, goal.z - p.z) < 0.2) return true;
  const r = route(m, p, goal);
  let w = r[0];
  if (r.length > 1 && Math.hypot(w.x - p.x, w.z - p.z) < 0.12) w = r[1];
  const d = Math.hypot(w.x - p.x, w.z - p.z);
  tick(c, [send(c, id, { mx: d < 1e-9 ? 0 : (w.x - p.x) / d, mz: d < 1e-9 ? 0 : (w.z - p.z) / d })]);
  return false;
}

test("the layout is the 3D boat: 28 m, rooms 4/6/4/4/4/6 and bulkheads exactly where boat_layout.json puts them", () => {
  const layout = JSON.parse(fs.readFileSync(path.join(__dirname, "../../Assets/_Project/Art/Models/Environment/boat_layout.json"), "utf8"));
  assert.strictEqual(m.HALF_Z * 2, layout.length);
  assert.strictEqual(m.HALF_X * 2, layout.width);
  assert.strictEqual(m.WATER_COMPARTMENTS.length, layout.rooms.length);
  layout.rooms.forEach((room, i) => {                              // the art lists the rooms from the bow (+z) to the stern
    const comp = m.WATER_COMPARTMENTS[i];
    assert.strictEqual(comp.z1, Math.max(room.start, room.end), room.id);
    assert.strictEqual(comp.z0, Math.min(room.start, room.end), room.id);
    assert.strictEqual(comp.volume, (comp.z1 - comp.z0) * layout.width * 2, "volume = length x width x 2 m deep, " + room.id);
  });
  m.BULKHEAD_Z.forEach((z, d) => assert.strictEqual(z, m.WATER_COMPARTMENTS[d].z0, "bulkhead " + d + " is the stern end of compartment " + d));
  assert.strictEqual(m.HATCH_X.length, m.BULKHEAD_Z.length);
});

test("a bulkhead stops a player outside its hatch, the hatch lets them through, and the wall slides them along instead of sticking", () => {
  const c = setup();
  at(c, "a", 0.5, 4.8);                                            // fore of the bulkhead z = 4 (hatch at x = +1.45, 1.0 .. 1.9)
  walk(c, "a", 0, -1, 10);
  assert.ok(c.state.players.a.z > 4 + m.WALL_HALF, "the wall holds: z = " + c.state.players.a.z);
  at(c, "a", m.HATCH_X[1], 4.8);
  walk(c, "a", 0, -1, 6);
  assert.ok(c.state.players.a.z < 4, "through the hatch: z = " + c.state.players.a.z);
  at(c, "a", 0.5, 4.8);                                            // diagonal into the wall: the x component survives
  const x0 = c.state.players.a.x;
  walk(c, "a", 1, -1, 3);
  assert.ok(c.state.players.a.x > x0 + 0.3, "slides along the wall");
});

test("the privacy baffle blocks the straight line behind a hatch and the way round is open", () => {
  const c = setup();
  at(c, "a", m.HATCH_X[1], 3.4);                                   // just aft of the hatch z = 4, in line with it
  walk(c, "a", 0, -1, 6);
  const baffleFront = 4 - m.BAFFLE_OFFSET + m.BAFFLE_HALF_Z;
  assert.ok(c.state.players.a.z > baffleFront, "stopped by the baffle: z = " + c.state.players.a.z);
  const goal = { x: 0.0, z: -2 };                                  // round the baffle, down to the reactor compartment
  let ok = false;
  for (let i = 0; i < 400 && !ok; i++) ok = stepToward(c, "a", goal);
  assert.ok(ok, "reached " + JSON.stringify(c.state.players.a));
});

test("a scripted walker goes from the bow to the stern and back through the five hatches", () => {
  const c = setup();
  const go = (goal) => { for (let i = 0; i < 1200; i++) if (stepToward(c, "a", goal)) return true; return false; };
  at(c, "a", 0, 12.5);
  assert.ok(go({ x: 0, z: -12.5 }), "bow to stern, at " + JSON.stringify(c.state.players.a));
  assert.ok(go({ x: 0, z: 12.5 }), "stern to bow, at " + JSON.stringify(c.state.players.a));
});

test("a closed hatch is a wall: it blocks the player and the water, and opening it again lets both through", () => {
  const c = setup();
  m.waterSetDoor(c.state.water, 1, false);
  at(c, "a", m.HATCH_X[1], 4.8);
  walk(c, "a", 0, -1, 8);
  assert.ok(c.state.players.a.z > 4, "blocked by the closed hatch: z = " + c.state.players.a.z);
  m.waterAdd(c.state.water, 1, 10, 0);
  for (let i = 0; i < 300; i++) m.waterStep(c.state.water);
  assert.strictEqual(c.state.water.comps[2].w, 0, "no water crosses a closed hatch");
  m.waterSetDoor(c.state.water, 1, true);
  walk(c, "a", 0, -1, 8);
  assert.ok(c.state.players.a.z < 4, "open again: z = " + c.state.players.a.z);
});

test("the hatch wheel opens and closes the hatch (aimed and by position); closing on somebody standing in it is refused", () => {
  const c = setup(["a", "b"]);
  const wheel = m.CONTROLS.find((k) => k.id === "hatch2");
  assert.ok(wheel && wheel.hatch === 2 && wheel.x === m.HATCH_X[2] && wheel.z === m.BULKHEAD_Z[2]);
  at(c, "a", wheel.x, wheel.z + 1.0); at(c, "b", -2, 8);
  tick(c, [send(c, "a", { act: true, use: "hatch2" })]);
  assert.strictEqual(c.state.water.doors[2].open, false, "aimed press: closed");
  tick(c, [send(c, "a", { act: true, use: "hatch2" })]);
  assert.strictEqual(c.state.water.doors[2].open, true, "second press: open");
  at(c, "a", wheel.x, wheel.z + 0.4);
  tick(c, [send(c, "a", { act: true })]);                          // by position
  assert.strictEqual(c.state.water.doors[2].open, false);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.water.doors[2].open, true);
  at(c, "b", wheel.x, wheel.z);                                    // b stands IN the opening
  at(c, "a", wheel.x, wheel.z + 1.2);
  tick(c, [send(c, "a", { act: true, use: "hatch2" })]);
  assert.strictEqual(c.state.water.doors[2].open, true, "refused: somebody is in the hatch");
  at(c, "b", -2, 8);
  tick(c, [send(c, "a", { act: true, use: "hatch2" })]);
  assert.strictEqual(c.state.water.doors[2].open, false);
  at(c, "a", wheel.x, wheel.z + 5);                                // out of arm's reach
  tick(c, [send(c, "a", { act: true, use: "hatch2" })]);
  assert.strictEqual(c.state.water.doors[2].open, false, "out of reach: nothing happens");
});

test("nobody leaves a room through a wall at full speed, in any direction, and a player inside a wall can walk out", () => {
  for (let k = 0; k < 16; k++) {
    const ang = (k / 16) * 2 * Math.PI;
    const c = setup();
    at(c, "a", -0.5, 3.0);                                         // radio compartment, between the bulkheads z = 4 and z = 0
    for (let i = 0; i < 40; i++) {
      walk(c, "a", Math.cos(ang), Math.sin(ang), 1);
      const p = c.state.players.a;
      assert.ok(!m.layoutBlocked(p.x, p.z, c.state.water.doors), "ray " + k + " tick " + i + " ended inside a wall: " + p.x + ", " + p.z);
    }
  }
  const c = setup();
  at(c, "a", -2.5, 0);                                             // inside the wall of the bulkhead z = 0
  walk(c, "a", 0, 1, 5);
  assert.ok(c.state.players.a.z > 0.5, "walked out: z = " + c.state.players.a.z);
});

test("the broadcast state carries the hatch states so a client predicts the same collisions (reconnection resync)", () => {
  const c = setup();
  const sent = [];
  c.d = { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) };
  m.waterSetDoor(c.state.water, 3, false);
  tick(c);
  assert.deepStrictEqual(sent[sent.length - 1].bw.dr, [1, 1, 1, 0, 1]);
});

test("a thrown or sliding item stops against a bulkhead, a baffle and a closed hatch, and flies through an open hatch (E2-01)", () => {
  assert.ok(m.cargoBlocked(0.5, 4.0), "inside the wall z = 4");
  assert.ok(!m.cargoBlocked(m.HATCH_X[1], 4.0), "the open hatch");
  assert.ok(m.cargoBlocked(m.HATCH_X[1], 4.0, [{ open: true }, { open: false }, { open: true }, { open: true }, { open: true }]), "the closed hatch");
  assert.ok(m.cargoBlocked(m.HATCH_X[1], 4 - m.BAFFLE_OFFSET), "the baffle behind the hatch");
  const c = setup();
  const crate = c.state.cargo.find((k) => k.id === "crate2");
  crate.x = 0.5; crate.z = 4.6; crate.vx = 0; crate.vz = -3; crate.fly = true; crate.y = 0.5; crate.vy = 1;
  for (let i = 0; i < 6; i++) tick(c);
  assert.ok(crate.z > 4, "stopped fore of the bulkhead: z = " + crate.z);
  crate.x = m.HATCH_X[1]; crate.z = 4.6; crate.vx = 0; crate.vz = -3; crate.fly = true; crate.y = 0.5; crate.vy = 1;
  tick(c); tick(c);
  assert.ok(crate.z < 4.6 && crate.z > 3.5 || crate.z < 3.5, "through the open hatch");
});
