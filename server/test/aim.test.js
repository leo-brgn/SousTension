// Acceptance tests for the aimed interaction: the client sends the id of the object it looks at (input field `use`) (E2-02).
// Run: node --test server/test/aim.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });

function setup(ids) {
  let { state } = h.matchInit({}, logger, nk, {});
  (ids || ["a"]).forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state; });
  state.leaks.nextTick = 1e9;
  state.reactor = m.newReactor(1234, "croisiere");
  return { state, d: { broadcastMessage() {} }, tick: 0, seq: {} };
}
function send(c, id, extra) {
  c.seq[id] = (c.seq[id] || 0) + 1;
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify(Object.assign({ seq: c.seq[id], mx: 0, mz: 0 }, extra || {})) };
}
function tick(c, msgs) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msgs || []).state; }
function ticks(c, n) { for (let i = 0; i < n; i++) tick(c); }
function ctl(id) { return m.CONTROLS.find((k) => k.id === id); }
function standAt(c, id, x, z) { c.state.players[id].x = x; c.state.players[id].z = z; }
function standNear(c, controlId, dist, pid) { const k = ctl(controlId); standAt(c, pid || "a", k.x + dist, k.z); }
function use(c, target, extra, pid) { tick(c, [send(c, pid || "a", Object.assign({ act: true, use: target }, extra || {}))]); }

test("the ids the server accepts: every control, every command of the coupled actions, and 'item'", () => {
  const ids = m.interactableIds();
  assert.strictEqual(new Set(ids).size, ids.length, "unique");
  assert.ok(ids.includes("item"));
  for (const k of m.CONTROLS) assert.ok(ids.includes(k.id), k.id);
  for (const a of m.COUPLED_ACTIONS) { assert.ok(ids.includes("cc:" + a.id + ":0")); assert.ok(ids.includes("cc:" + a.id + ":1")); }
  for (const id of ids) assert.ok(id.length <= 40, "fits the input field: " + id);
});

test("an aimed press works from arm's reach (2 m) even on a control whose position-based reach is 0.3 m, and the position path would not", () => {
  const c = setup();
  const i = m.BREAKERS.findIndex((b) => b.id === "galley");
  standNear(c, "breaker_galley", 1.8);
  tick(c, [send(c, "a", { act: true })]);                                       // no `use`: position path, out of its 0.3 m reach
  assert.strictEqual(c.state.power.br[i].closed, true);
  use(c, "breaker_galley");
  assert.strictEqual(c.state.power.br[i].closed, false);
});

test("beyond arm's reach, for an unknown id, a wrong type or an id that is too long, the press is ignored", () => {
  const c = setup();
  const i = m.BREAKERS.findIndex((b) => b.id === "galley");
  standNear(c, "breaker_galley", m.AIM_REACH + 0.3);
  use(c, "breaker_galley");
  standNear(c, "breaker_galley", 0.5);
  use(c, "no_such_control");
  use(c, "x".repeat(60));
  tick(c, [send(c, "a", { act: true, use: 42 })]);                              // not a string: treated as no target (position path, 0.5 m > reach)
  use(c, "cc:demo:7"); use(c, "cc:demo"); use(c, "leak:abc"); use(c, "leak:99");
  assert.strictEqual(c.state.power.br[i].closed, true);
  assert.ok(c.state.power.br.every((b) => b.closed));
});

test("the aim picks the exact breaker even when standing between two tiles", () => {
  const c = setup();
  const a = m.BREAKERS.findIndex((b) => b.id === "galley"), b = m.BREAKERS.findIndex((x) => x.id === "samovar");
  const ka = m.BREAKERS[a], kb = m.BREAKERS[b];
  standAt(c, "a", (ka.x + kb.x) / 2, (ka.z + kb.z) / 2);
  use(c, "breaker_samovar");
  assert.strictEqual(c.state.power.br[b].closed, false);
  assert.strictEqual(c.state.power.br[a].closed, true);
});

test("every kind of control answers an aimed press: selector, SCRAM lever (two presses), pumps, bilge pumps, telegraph", () => {
  const c = setup();
  standNear(c, "regime", 1.5); use(c, "regime");
  assert.strictEqual(c.state.reactor.regime, "pleine");                         // croisiere -> pleine
  standNear(c, "scram", 1.5); use(c, "scram");
  assert.ok(c.state.lever.cover > 0 && !c.state.reactor.scram, "first press lifts the cover");
  use(c, "scram");
  assert.strictEqual(c.state.reactor.scram, true, "second press pulls it");
  const c2 = setup();
  standNear(c2, "pump0", 1.5); use(c2, "pump0");
  assert.strictEqual(c2.state.reactor.pumps[0], false);
  standNear(c2, "bilge1", 1.5); use(c2, "bilge1");
  assert.strictEqual(c2.state.bilge.pumps[1].on, true);
  standNear(c2, "tele_up", 1.5); use(c2, "tele_up");
  assert.strictEqual(c2.state.prop.pos, 2);
});

test("holding the key on a valve wheel opens it (aimed), holding it on something else does nothing", () => {
  const c = setup();
  c.state.reactor.valves = [0, 0, 0, 0];
  standNear(c, "valve1", 1.5);
  for (let i = 0; i < 20; i++) tick(c, [send(c, "a", { hold: true, use: "valve1" })]);
  assert.ok(Math.abs(c.state.reactor.valves[1] - 2 * m.VALVE_TURN_RATE) < 1e-9);
  assert.strictEqual(c.state.reactor.valves[0], 0);
  for (let i = 0; i < 20; i++) tick(c, [send(c, "a", { hold: true, use: "regime" })]);
  assert.ok(Math.abs(c.state.reactor.valves[1] - 2 * m.VALVE_TURN_RATE) < 1e-9);
  standAt(c, "a", ctl("valve1").x, ctl("valve1").z + m.AIM_REACH + 0.5);              // along the wall: inside the boat, out of arm's reach
  for (let i = 0; i < 20; i++) tick(c, [send(c, "a", { hold: true, use: "valve1" })]);
  assert.ok(Math.abs(c.state.reactor.valves[1] - 2 * m.VALVE_TURN_RATE) < 1e-9, "out of reach: still the same");
});

test("the commands of the Rule of Two Players can be aimed: two players, two commands, within the window", () => {
  const c = setup(["a", "b"]);
  const act = m.COUPLED_ACTIONS.find((x) => x.id === "demo2");
  standAt(c, "a", act.a.x + 1.5, act.a.z); standAt(c, "b", act.b.x - 1.5, act.b.z);
  use(c, "cc:demo2:0", {}, "a");
  assert.strictEqual(c.state.cp.acts[m.COUPLED_ACTIONS.indexOf(act)].st[0].by, "a");
  use(c, "cc:demo2:1", {}, "b");
  assert.strictEqual(c.state.cp.acts[m.COUPLED_ACTIONS.indexOf(act)].count, 1);
});

test("an aimed press on a command that is out of reach, or the same player on both commands, does not count", () => {
  const c = setup(["a"]);
  const act = m.COUPLED_ACTIONS.find((x) => x.id === "demo2"), idx = m.COUPLED_ACTIONS.indexOf(act);
  standAt(c, "a", act.a.x + 4, act.a.z);
  use(c, "cc:demo2:0");
  assert.strictEqual(c.state.cp.acts[idx].st[0].by, "");
  standAt(c, "a", act.a.x, act.a.z); use(c, "cc:demo2:0");
  standAt(c, "a", act.b.x, act.b.z); use(c, "cc:demo2:1");
  assert.strictEqual(c.state.cp.acts[idx].st[1].by, "", "one player cannot hold both commands");
});

test("an aimed leak is repaired with a patch in hand, from within the leak's reach; 'item' does the same; far away, nothing", () => {
  const c = setup();
  const leak = m.leakCreate(c.state.leaks, 2, 1, 3);
  const patch = c.state.cargo.find((x) => x.id === "patch1");
  standAt(c, "a", patch.x, patch.z);
  tick(c, [send(c, "a", { grab: true })]);
  standAt(c, "a", leak.x - m.LEAK_REACH - 0.5, leak.z);
  use(c, "leak:" + leak.id);
  assert.strictEqual(leak.size, 3, "too far from the hole");
  standAt(c, "a", leak.x - 0.5, leak.z);
  use(c, "leak:" + leak.id);
  assert.strictEqual(leak.size, 2);
  const p2 = c.state.cargo.find((x) => x.id === "patch2");
  standAt(c, "a", p2.x, p2.z);
  tick(c, [send(c, "a", { grab: true })]);
  standAt(c, "a", leak.x - 0.5, leak.z);
  use(c, "item");
  assert.strictEqual(leak.size, 1);
});

test("'item' with the bucket in the water scoops, and with nothing in hand does nothing", () => {
  const c = setup();
  for (let d = 0; d < c.state.water.doors.length; d++) m.waterSetDoor(c.state.water, d, false);
  const bucket = c.state.cargo.find((x) => x.id === "bucket");
  standAt(c, "a", bucket.x, bucket.z);
  const comp = m.compartmentAt(bucket.z);
  m.waterAdd(c.state.water, comp, 2, 0);
  use(c, "item");
  assert.strictEqual(c.state.water.comps[comp].w, 2, "nothing in hand");
  tick(c, [send(c, "a", { grab: true })]);
  use(c, "item");
  assert.ok(Math.abs(c.state.water.comps[comp].w - (2 - m.BUCKET_VOLUME)) < 1e-9);
});

test("without `use` (or an empty one) the position-based path still works: the old behaviour is the fallback", () => {
  const c = setup();
  standNear(c, "tele_up", 0);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.prop.pos, 2);
  tick(c, [send(c, "a", { act: true, use: "" })]);
  assert.strictEqual(c.state.prop.pos, 3);
});

// The client names the interactables it marks (Assets/_Spikes/MovingFrame/Views/InteractableTarget.cs, InteractableIds): that list must be the
// server's list exactly, otherwise a click on an object would be ignored. Checked here by reading the C# source between its markers.
test("the interactable ids listed by the client views are exactly the ids the server accepts", () => {
  const fs = require("node:fs");
  const path = require("node:path");
  const src = fs.readFileSync(path.join(__dirname, "..", "..", "Assets", "_Spikes", "MovingFrame", "Views", "InteractableTarget.cs"), "utf8");
  const between = (a, b) => src.slice(src.indexOf(a) + a.length, src.indexOf(b));
  const quoted = (text) => [...text.matchAll(/"([^"]+)"/g)].map((x) => x[1]);
  const client = quoted(between("IDS-BREAKERS-BEGIN", "IDS-BREAKERS-END")).concat(quoted(between("IDS-OTHERS-BEGIN", "IDS-OTHERS-END")));
  const server = m.interactableIds();
  assert.deepStrictEqual(client.slice().sort(), server.slice().sort());
});
