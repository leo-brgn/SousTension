// Voice relay (E8-07): packets pass through the match untouched, outside the simulation. Run: node --test server/test/voice.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers, nk = { binaryToString: (d) => d }, logger = { info() {} };
const pr = (id) => ({ userId: id, sessionId: "s" + id });

function room() {
  let { state } = h.matchInit({}, logger, nk, {});
  for (const id of ["a", "b", "c"]) state = h.matchJoin({}, logger, nk, null, 0, state, [pr(id)]).state;
  const sent = [];
  const d = { broadcastMessage: (op, data, presences, sender, reliable) => sent.push({ op, data, presences, sender, reliable }) };
  return { state, sent, d, tick: 0 };
}
const loop = (r, msgs) => { r.state = h.matchLoop({}, logger, nk, r.d, ++r.tick, r.state, msgs).state; };
const voices = (r) => r.sent.filter((s) => s.op === m.OP_VOICE);

test("a voice packet is relayed to everyone, unchanged, with its sender, unreliably", () => {
  const r = room();
  loop(r, [{ opCode: m.OP_VOICE, sender: pr("a"), data: "opus-bytes" }]);
  const v = voices(r);
  assert.strictEqual(v.length, 1);
  assert.strictEqual(v[0].data, "opus-bytes");
  assert.strictEqual(v[0].presences, null, "to the whole match");
  assert.strictEqual(v[0].sender.userId, "a");
  assert.strictEqual(v[0].reliable, false, "a late voice packet is useless: no retransmission");
});

test("a sender that floods is capped per tick, the others are not affected", () => {
  const r = room();
  const flood = []; for (let i = 0; i < 50; i++) flood.push({ opCode: m.OP_VOICE, sender: pr("a"), data: "x" + i });
  flood.push({ opCode: m.OP_VOICE, sender: pr("b"), data: "b1" });
  loop(r, flood);
  const v = voices(r);
  assert.strictEqual(v.filter((x) => x.sender.userId === "a").length, m.VOICE_MAX_PER_TICK);
  assert.strictEqual(v.filter((x) => x.sender.userId === "b").length, 1);
});

test("someone who is not in the match cannot talk into it", () => {
  const r = room();
  loop(r, [{ opCode: m.OP_VOICE, sender: pr("stranger"), data: "x" }]);
  assert.strictEqual(voices(r).length, 0);
});

test("voice leaves the game state alone: the state after a talkative tick equals the state after a silent one", () => {
  const quiet = room(), loud = room();
  for (let i = 0; i < 20; i++) {
    loop(quiet, []);
    loop(loud, [{ opCode: m.OP_VOICE, sender: pr("a"), data: "v" + i }, { opCode: m.OP_VOICE, sender: pr("b"), data: "w" + i }]);
  }
  assert.deepStrictEqual(JSON.parse(JSON.stringify(loud.state)), JSON.parse(JSON.stringify(quiet.state)));
});
