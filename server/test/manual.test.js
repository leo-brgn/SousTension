// Acceptance tests for the OK-114 Operating Manual: one copy, two hands, turnable pages (E5-02).
// Run: node --test server/test/manual.test.js
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
function cargo(c, id) { return c.state.cargo.find((x) => x.id === id); }
function at(c, pid, x, z) { c.state.players[pid].x = x; c.state.players[pid].z = z; }
function take(c, pid, itemId) { const it = cargo(c, itemId); at(c, pid, it.x, it.z); tick(c, [send(c, pid, { take: true })]); }
function page(c) { return c.state.manual.page; }

test("the manual: a contents page and procedures of at most 5 steps each, with unique ids, all described in data", () => {
  assert.ok(m.MANUAL_PAGES.length >= 6);
  assert.strictEqual(m.MANUAL_PAGES[0].id, "index");
  assert.strictEqual(new Set(m.MANUAL_PAGES.map((p) => p.id)).size, m.MANUAL_PAGES.length);
  for (const p of m.MANUAL_PAGES) assert.ok(p.steps >= 0 && p.steps <= m.MANUAL_MAX_STEPS, p.id + " has " + p.steps + " steps");
  for (const id of ["scram", "restart", "leak", "power", "propulsion"]) assert.ok(m.manualPageIndex(id) > 0, id);
  assert.strictEqual(m.MANUAL_MAX_STEPS, 5);
});

test("there is exactly one manual in the game: an 8 kg binder that needs both hands", () => {
  const c = setup();
  assert.strictEqual(c.state.cargo.filter((x) => x.kind === "manual").length, 1);
  assert.strictEqual(m.ITEM_KINDS.manual.hands, 2);
  assert.strictEqual(m.ITEM_KINDS.manual.mass, 8);
  take(c, "a", "manual");
  assert.deepStrictEqual(c.state.players.a.hands.l, "manual"); assert.deepStrictEqual(c.state.players.a.hands.r, "manual");
  close(m.carrySpeedFactor(c.state, "a"), 1 - 0.012 * 8, 1e-9);
});

test("whoever reads cannot act: holding the binder locks every control", () => {
  const c = setup();
  take(c, "a", "manual");
  const up = m.CONTROLS.find((k) => k.id === "tele_up");
  at(c, "a", up.x, up.z);
  tick(c, [send(c, "a", { act: true })]);
  tick(c, [send(c, "a", { act: true, use: "tele_up" })]);
  assert.strictEqual(c.state.prop.pos, 1);
  tick(c, [send(c, "a", { drop: true })]);
  tick(c, [send(c, "a", { act: true })]);
  assert.strictEqual(c.state.prop.pos, 2, "once the binder is down the hands are free again");
});

test("pages turn one at a time while holding the binder, with the keys' tokens or the flip field, and stop at both ends", () => {
  const c = setup();
  take(c, "a", "manual");
  assert.strictEqual(page(c), 0);
  tick(c, [send(c, "a", { hand: "next" })]);
  assert.strictEqual(page(c), 1);
  tick(c, [send(c, "a", { flip: 1 })]);
  assert.strictEqual(page(c), 2);
  tick(c, [send(c, "a", { hand: "prev" })]);
  assert.strictEqual(page(c), 1);
  tick(c, [send(c, "a", { flip: -1 })]); tick(c, [send(c, "a", { flip: -1 })]);
  assert.strictEqual(page(c), 0, "stops on the first page");
  for (let i = 0; i < 20; i++) tick(c, [send(c, "a", { flip: 1 })]);
  assert.strictEqual(page(c), m.MANUAL_PAGES.length - 1, "stops on the last page");
  tick(c, [send(c, "a", { flip: 7 })]);                                           // only +1 / -1 are meaningful
  assert.strictEqual(page(c), m.MANUAL_PAGES.length - 1);
});

test("pages cannot be turned without holding the binder, nor while carrying something else", () => {
  const c = setup();
  tick(c, [send(c, "a", { flip: 1 })]);
  assert.strictEqual(page(c), 0, "empty hands");
  take(c, "a", "bucket");
  tick(c, [send(c, "a", { flip: 1 })]);
  assert.strictEqual(page(c), 0, "a bucket is not a manual");
  const d = setup(["a", "b"]);
  take(d, "a", "manual");
  tick(d, [send(d, "b", { flip: 1 })]);
  assert.strictEqual(page(d), 0, "another player cannot turn the pages of a book they do not hold");
});

test("one copy: nobody else can take the binder while it is carried; once put down it stays where it was left", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "manual");
  take(c, "b", "manual");
  assert.deepStrictEqual(cargo(c, "manual").carriers, ["a"]);
  tick(c, [send(c, "a", { drop: true })]);
  assert.deepStrictEqual(cargo(c, "manual").carriers, []);
  take(c, "b", "manual");
  assert.deepStrictEqual(cargo(c, "manual").carriers, ["b"]);
});

test("the binder cannot be thrown (two hands), and a pocket cannot hold it", () => {
  const c = setup();
  take(c, "a", "manual");
  tick(c, [send(c, "a", { hand: "throw" })]);
  assert.strictEqual(cargo(c, "manual").fly, false);
  assert.deepStrictEqual(cargo(c, "manual").carriers, ["a"]);
  tick(c, [send(c, "a", { stow: true })]);
  assert.strictEqual(c.state.players.a.hands.p, "");
});

test("the page is shared state: it stays when the reader puts the binder down, and the next reader finds it open there", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "manual");
  tick(c, [send(c, "a", { flip: 1 })]); tick(c, [send(c, "a", { flip: 1 })]);
  tick(c, [send(c, "a", { drop: true })]);
  assert.strictEqual(page(c), 2);
  take(c, "b", "manual");
  tick(c, [send(c, "b", { flip: 1 })]);
  assert.strictEqual(page(c), 3);
});

test("manualGoto opens the manual on a page by id or index (for the automatic opening on an alarm, E5-04), unknown pages are refused", () => {
  const c = setup();
  assert.strictEqual(m.manualGoto(c.state, "restart"), true);
  assert.strictEqual(page(c), m.manualPageIndex("restart"));
  assert.strictEqual(m.manualGoto(c.state, 0), true);
  assert.strictEqual(page(c), 0);
  assert.strictEqual(m.manualGoto(c.state, "no_such_page"), false);
  assert.strictEqual(m.manualGoto(c.state, 99), false);
  assert.strictEqual(m.manualGoto(c.state, -1), false);
  assert.strictEqual(page(c), 0);
});

test("a reader who leaves the match drops the binder, and the page is kept", () => {
  const c = setup(["a", "b"]);
  take(c, "a", "manual");
  tick(c, [send(c, "a", { flip: 1 })]);
  c.state = h.matchLeave({}, logger, nk, null, c.tick, c.state, [presence("a")]).state;
  tick(c);
  assert.deepStrictEqual(cargo(c, "manual").carriers, []);
  assert.strictEqual(page(c), 1);
});

test("the current page and the page count are always in the broadcast (reconnection resync)", () => {
  const c = setup();
  assert.deepStrictEqual(tick(c).mn, { p: 0, n: m.MANUAL_PAGES.length });
  take(c, "a", "manual");
  tick(c, [send(c, "a", { flip: 1 })]);
  assert.strictEqual(tick(c).mn.p, 1);
});

// The client names the pages (Assets/_Spikes/MovingFrame/Views/ManualView.cs, ManualPages): that list must be the server's, in order.
test("the page ids listed by the client are exactly the server's, in the same order", () => {
  const fs = require("node:fs");
  const path = require("node:path");
  const src = fs.readFileSync(path.join(__dirname, "..", "..", "Assets", "_Spikes", "MovingFrame", "Views", "ManualView.cs"), "utf8");
  const block = src.slice(src.indexOf("PAGES-BEGIN") + 11, src.indexOf("PAGES-END"));
  const client = [...block.matchAll(/\("([a-z_]+)",\s*(\d+)\)/g)].map((x) => [x[1], +x[2]]);
  assert.deepStrictEqual(client, m.MANUAL_PAGES.map((p) => [p.id, p.steps]));
});
