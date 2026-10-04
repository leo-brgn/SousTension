// Node tests for the Nakama match logic (no Nakama needed): node --test server/test
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");
const h = m.handlers;

const nk = { binaryToString: (d) => d };
const logger = { info() {} };
function makeDispatcher() {
  const sent = [];
  return { sent, broadcastMessage: (op, data) => sent.push({ op, data: JSON.parse(data) }) };
}
function presence(id) { return { userId: id, sessionId: "s" + id }; }
function input(id, seq, mx, mz) {
  return { opCode: m.OP_INPUT, sender: presence(id), data: JSON.stringify({ seq, mx, mz }) };
}

test("joins up to 4 players, rejects the 5th", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  for (const id of ["a", "b", "c", "d"]) {
    assert.ok(h.matchJoinAttempt({}, logger, nk, null, 0, state, presence(id), {}).accept);
    state = h.matchJoin({}, logger, nk, null, 0, state, [presence(id)]).state;
  }
  assert.strictEqual(h.matchJoinAttempt({}, logger, nk, null, 0, state, presence("e"), {}).accept, false);
});

test("applies one input per tick and acknowledges its sequence", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  const x0 = state.players.a.x;
  state = h.matchLoop({}, logger, nk, d, 1, state, [input("a", 1, 1, 0), input("a", 2, 1, 0)]).state;
  const s1 = d.sent[0].data.players[0];
  assert.strictEqual(s1.seq, 1);
  assert.ok(Math.abs(s1.x - (x0 + m.MOVE_SPEED * m.DT)) < 1e-9);
  state = h.matchLoop({}, logger, nk, d, 2, state, []).state;
  assert.strictEqual(d.sent[1].data.players[0].seq, 2);
});

test("ignores stale/duplicate sequences and clamps to the boat interior", () => {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  const d = makeDispatcher();
  for (let t = 1; t <= 200; t++) {
    state = h.matchLoop({}, logger, nk, d, t, state, [input("a", t, 1, 1), input("a", t, 5, 5)]).state;
  }
  const p = d.sent[d.sent.length - 1].data.players[0];
  assert.ok(p.x <= m.HALF_X + 1e-9 && p.z <= m.HALF_Z + 1e-9);
  assert.strictEqual(p.seq, 200);
});

test("diagonal input is normalised (no faster diagonal)", () => {
  const p = { x: 0, z: 0 };
  m.stepPlayer(p, 1, 1);
  const dist = Math.sqrt(p.x * p.x + p.z * p.z);
  assert.ok(Math.abs(dist - m.MOVE_SPEED * m.DT) < 1e-9);
});

test("module registers match + rpc", () => {
  const reg = { rpcs: [], matches: [] };
  const published = [];
  const nkFull = { binaryToString: nk.binaryToString, matchCreate: () => "match-1", storageWrite: (w) => published.push(w) };
  m.InitModule({}, logger, nkFull, {
    registerMatch: (n, hnd) => reg.matches.push(n),
    registerRpc: (n) => reg.rpcs.push(n)
  });
  assert.deepStrictEqual(reg.matches, ["moving_frame"]);
  assert.deepStrictEqual(reg.rpcs, ["get_moving_frame_match"]);
  assert.strictEqual(published.length, 1); // match id published to storage at startup
});
