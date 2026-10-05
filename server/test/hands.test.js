// Acceptance tests for "one hand = one thing": two hands and a chest pocket, two-handed items that lock every other action (E2-03).
// Run: node --test server/test/hands.test.js
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
function hands(c, pid) { const p = c.state.players[pid || "a"].hands; return [p.l, p.r, p.p]; }
function take(c, pid, itemId) { const it = cargo(c, itemId); at(c, pid, it.x, it.z); tick(c, [send(c, pid, { take: true })]); }
function ctl(id) { return m.CONTROLS.find((k) => k.id === id); }

test("items are described in data: crates and the fuel flask need two hands, patch and bucket one, the torch one hand or the pocket", () => {
  assert.strictEqual(m.ITEM_KINDS.crate.hands, 2); assert.strictEqual(m.ITEM_KINDS.fuel.hands, 2);
  assert.strictEqual(m.ITEM_KINDS.patch.hands, 1); assert.strictEqual(m.ITEM_KINDS.bucket.hands, 1);
  assert.strictEqual(m.ITEM_KINDS.flashlight.hands, 1); assert.strictEqual(m.ITEM_KINDS.flashlight.pocket, true);
  const c = setup();
  assert.ok(cargo(c, "flashlight"), "the torch is in the toolbox");
  assert.ok(c.state.cargo.every((it) => m.ITEM_KINDS[it.kind]), "every cargo item has a kind with a hand count");
});

test("a player starts with empty hands and an empty pocket, and everyone sees them in the broadcast", () => {
  const c = setup(["a", "b"]);
  const v = tick(c);
  assert.deepStrictEqual(v.players.map((p) => p.hd), [["", "", ""], ["", "", ""]]);
});

test("one-hand items: a patch and the bucket can be carried together, one per hand, and a third item is refused until a hand is free", () => {
  const c = setup();
  take(c, "a", "patch1");
  take(c, "a", "bucket");
  assert.deepStrictEqual(hands(c).slice(0, 2).sort(), ["bucket", "patch1"]);
  take(c, "a", "flashlight");
  assert.strictEqual(cargo(c, "flashlight").carriers.length, 0, "no free hand");
  tick(c, [send(c, "a", { drop: "L" })]);
  take(c, "a", "flashlight");
  assert.deepStrictEqual(cargo(c, "flashlight").carriers, ["a"]);
});

test("a two-handed item takes both hands: nothing else can be taken and a crate is refused if a hand is busy", () => {
  const c = setup();
  take(c, "a", "crate1");
  assert.deepStrictEqual(hands(c).slice(0, 2), ["crate1", "crate1"]);
  take(c, "a", "patch1");
  assert.strictEqual(cargo(c, "patch1").carriers.length, 0);
  const d = setup();
  take(d, "a", "patch1");
  take(d, "a", "crate2");
  assert.strictEqual(cargo(d, "crate2").carriers.length, 0, "a crate needs both hands free");
});

test("a two-handed item locks every control: no press, no held valve wheel, no command of the Rule of Two Players, aimed or by position", () => {
  const c = setup();
  c.state.reactor.valves = [0, 0, 0, 0];
  take(c, "a", "crate1");
  const up = ctl("tele_up");
  at(c, "a", up.x, up.z);
  tick(c, [send(c, "a", { act: true })]);
  tick(c, [send(c, "a", { act: true, use: "tele_up" })]);
  assert.strictEqual(c.state.prop.pos, 1, "the telegraph did not move");
  at(c, "a", ctl("valve1").x - 0.5, ctl("valve1").z);
  for (let i = 0; i < 10; i++) tick(c, [send(c, "a", { hold: true, use: "valve1" })]);
  for (let i = 0; i < 10; i++) tick(c, [send(c, "a", { hold: true })]);
  assert.strictEqual(c.state.reactor.valves[1], 0);
  const act = m.COUPLED_ACTIONS.find((x) => x.id === "demo2"), idx = m.COUPLED_ACTIONS.indexOf(act);
  at(c, "a", act.a.x, act.a.z);
  tick(c, [send(c, "a", { act: true })]); tick(c, [send(c, "a", { act: true, use: "cc:demo2:0" })]);
  assert.strictEqual(c.state.cp.acts[idx].st[0].by, "");
  tick(c, [send(c, "a", { drop: true })]);                                       // put the crate down: everything works again
  at(c, "a", up.x, up.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.prop.pos, 2);
});

test("with both hands full of small items a control does not answer; with one hand free it does", () => {
  const c = setup();
  take(c, "a", "patch1"); take(c, "a", "bucket");
  const up = ctl("tele_up");
  at(c, "a", up.x, up.z);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.prop.pos, 1);
  tick(c, [send(c, "a", { drop: "L" })]);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.prop.pos, 2);
});

test("using what you hold needs no free hand: a patch on a leak and the bucket in the water work with both hands full", () => {
  const c = setup();
  for (let d = 0; d < c.state.water.doors.length; d++) m.waterSetDoor(c.state.water, d, false);
  take(c, "a", "patch1"); take(c, "a", "bucket");
  const leak = m.leakCreate(c.state.leaks, 5.5, 1, 2);
  at(c, "a", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true, use: "leak:" + leak.id })]);
  assert.strictEqual(leak.size, 1, "the patch worked with the bucket in the other hand");
  const comp = m.compartmentAt(c.state.players.a.z);
  m.waterAdd(c.state.water, comp, 1, 0);
  tick(c, [send(c, "a", { act: true, use: "item" })]);
  assert.ok(c.state.water.comps[comp].w < 1, "the bucket scooped");
});

test("a used patch leaves the hand it was in", () => {
  const c = setup();
  take(c, "a", "patch1");
  const leak = m.leakCreate(c.state.leaks, 5.5, 1, 1);
  at(c, "a", leak.x - 0.5, leak.z);
  tick(c, [send(c, "a", { act: true, use: "item" })]);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
});

test("drop: the last item taken by default, a given slot (L / R / P) on request, a two-handed item frees both hands", () => {
  const c = setup();
  take(c, "a", "patch1"); take(c, "a", "bucket");
  tick(c, [send(c, "a", { drop: true })]);
  assert.strictEqual(cargo(c, "bucket").carriers.length, 0, "the bucket was taken last");
  assert.deepStrictEqual(cargo(c, "patch1").carriers, ["a"]);
  const slot = c.state.players.a.hands.l === "patch1" ? "L" : "R";
  tick(c, [send(c, "a", { drop: slot })]);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
  take(c, "a", "crate1");
  tick(c, [send(c, "a", { drop: "R" })]);
  assert.deepStrictEqual(hands(c), ["", "", ""], "the crate went from both hands");
});

test("pocket: the torch goes hand -> pocket (freeing the hand) and back, only a pocketable one-hand item fits, one thing at a time", () => {
  const c = setup();
  take(c, "a", "patch1");
  tick(c, [send(c, "a", { stow: true })]);
  assert.strictEqual(c.state.players.a.hands.p, "", "a patch does not fit the pocket");
  take(c, "a", "flashlight");
  tick(c, [send(c, "a", { stow: true })]);
  assert.strictEqual(c.state.players.a.hands.p, "flashlight");
  assert.strictEqual(cargo(c, "flashlight").carriers[0], "a", "still carried, it follows the player");
  // a second pocketable thing is refused while the pocket is full
  c.state.cargo.push({ id: "torch2", kind: "flashlight", heavy: false, x: 0, z: 0, vx: 0, vz: 0, carriers: [], pend: "", pendTick: 0, active: true, respawnTick: 0, homeX: 0, homeZ: 0 });
  c.state.players.a.hands.l = ""; c.state.players.a.hands.r = "";
  c.state.players.a.hands.order = ["flashlight"]; cargo(c, "patch1").carriers = [];
  take(c, "a", "torch2");
  tick(c, [send(c, "a", { stow: true })]);                                        // pocket full: this press brings the pocketed torch OUT instead
  assert.strictEqual(c.state.players.a.hands.p, "");
  assert.ok(hands(c).includes("flashlight") && hands(c).includes("torch2"));
});

test("pocket -> hand needs a free hand", () => {
  const c = setup();
  take(c, "a", "flashlight"); tick(c, [send(c, "a", { stow: true })]);
  take(c, "a", "crate1");
  tick(c, [send(c, "a", { stow: true })]);
  assert.strictEqual(c.state.players.a.hands.p, "flashlight", "two hands full of crate: stays in the pocket");
});

test("the heavy fuel flask needs two players with both hands free each; one dropping lets both go", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "patch1");
  take(c, "a", "fuel");
  assert.strictEqual(cargo(c, "fuel").pend, "", "a hand is busy: refused");
  tick(c, [send(c, "a", { drop: true })]);
  take(c, "a", "fuel");
  assert.strictEqual(cargo(c, "fuel").pend, "a");
  take(c, "b", "fuel");
  assert.deepStrictEqual(cargo(c, "fuel").carriers, ["a", "b"]);
  assert.deepStrictEqual(hands(c, "a").slice(0, 2), ["fuel", "fuel"]);
  assert.deepStrictEqual(hands(c, "b").slice(0, 2), ["fuel", "fuel"]);
  tick(c, [send(c, "a", { drop: true })]);
  assert.deepStrictEqual(hands(c, "a"), ["", "", ""]);
  assert.deepStrictEqual(hands(c, "b"), ["", "", ""], "the partner let go too");
  assert.deepStrictEqual(cargo(c, "fuel").carriers, []);
});

test("a first carrier of the flask whose partner does not come in time gets their hands back", () => {
  const c = setup(["a"]);
  take(c, "a", "fuel");
  assert.deepStrictEqual(hands(c).slice(0, 2), ["fuel", "fuel"]);
  ticks(c, m.INTERLOCK_WINDOW_TICKS + 2);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
});

test("a player who leaves the match lets everything go, including a shared carry for the partner", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "fuel"); take(c, "b", "fuel");
  take(c, "b", "flashlight");                                                    // refused: b's hands are full of the flask
  c.state = h.matchLeave({}, logger, nk, null, c.tick, c.state, [presence("a")]).state;
  tick(c);
  assert.deepStrictEqual(hands(c, "b"), ["", "", ""]);
  assert.deepStrictEqual(cargo(c, "fuel").carriers, []);
});

test("the legacy grab key still works: it drops what you hold, otherwise it takes the nearest item", () => {
  const c = setup();
  const crate = cargo(c, "crate1");
  at(c, "a", crate.x, crate.z);
  tick(c, [send(c, "a", { grab: true })]);
  assert.deepStrictEqual(crate.carriers, ["a"]);
  tick(c, [send(c, "a", { grab: true })]);
  assert.deepStrictEqual(crate.carriers, []);
});

test("take: nothing happens out of reach, and an item that is not in the world (used patch) cannot be taken", () => {
  const c = setup();
  const p = cargo(c, "patch1");
  at(c, "a", p.x, p.z - m.GRAB_REACH - 1.5);                                  // well away from the whole toolbox
  tick(c, [send(c, "a", { take: true })]);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
  p.active = false;
  at(c, "a", p.x, p.z);
  tick(c, [send(c, "a", { take: true })]);
  assert.ok(!hands(c).includes("patch1"));
});

test("what each player holds is in the broadcast (reconnection resync)", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "bucket");
  take(c, "b", "crate1");
  const v = tick(c).players;
  assert.ok(v.find((p) => p.id === "a").hd.includes("bucket"));
  assert.deepStrictEqual(v.find((p) => p.id === "b").hd.slice(0, 2), ["crate1", "crate1"]);
});

test("the client's single `hand` token drives the same commands: take, drop, drop:L/R/P, stow", () => {
  const c = setup();
  const p = cargo(c, "flashlight");
  at(c, "a", p.x, p.z);
  tick(c, [send(c, "a", { hand: "take" })]);
  assert.deepStrictEqual(p.carriers, ["a"]);
  tick(c, [send(c, "a", { hand: "stow" })]);
  assert.strictEqual(c.state.players.a.hands.p, "flashlight");
  tick(c, [send(c, "a", { hand: "drop:P" })]);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
  tick(c, [send(c, "a", { hand: "take" })]);
  tick(c, [send(c, "a", { hand: "drop" })]);
  assert.deepStrictEqual(p.carriers, []);
  tick(c, [send(c, "a", { hand: "bogus" })]);                                    // unknown tokens are ignored
  tick(c, [send(c, "a", { hand: "drop:X" })]);
  assert.deepStrictEqual(hands(c), ["", "", ""]);
});
