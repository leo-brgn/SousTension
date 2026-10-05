// Acceptance tests for propulsion and the machine telegraph (E3-08).
// Run: node --test server/test/propulsion.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };
const presence = (id) => ({ userId: id, sessionId: "s" + id });
const close = (a, b, eps, msg) => assert.ok(Math.abs(a - b) <= eps, (msg || "") + " " + a + " vs " + b);

function setup(regime) {
  let { state } = h.matchInit({}, logger, nk, {});
  state = h.matchJoin({}, logger, nk, null, 0, state, [presence("a")]).state;
  state.leaks.nextTick = 1e9;
  state.reactor = m.newReactor(1234, regime || "croisiere");
  const sent = [];
  return { state, d: { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) }, sent, tick: 0, seq: 0 };
}
function tick(c, msg) { c.state = h.matchLoop({}, logger, nk, c.d, ++c.tick, c.state, msg ? [msg] : []).state; return c.sent[c.sent.length - 1]; }
function ticks(c, n) { for (let i = 0; i < n; i++) tick(c); }
function press(c) { c.seq++; return { opCode: m.OP_INPUT, sender: presence("a"), data: JSON.stringify({ seq: c.seq, mx: 0, mz: 0, act: true }) }; }
function ctl(id) { return m.CONTROLS.find((k) => k.id === id); }
function stand(c, id) { c.state.players.a.x = ctl(id).x; c.state.players.a.z = ctl(id).z; }
function pos(c, name) { c.state.prop.pos = m.PROP_NAMES.indexOf(name); }

test("the telegraph has 5 positions (arriere, stop, lent, demi, toute), starts on stop, and each position has a demand and a speed set-point", () => {
  assert.deepStrictEqual(m.PROP_NAMES, ["arriere", "stop", "lent", "demi", "toute"]);
  assert.strictEqual(m.PROP_DEMAND.length, 5); assert.strictEqual(m.PROP_SPEED.length, 5);
  assert.strictEqual(m.newPropulsion().pos, m.PROP_NAMES.indexOf("stop"));
  assert.strictEqual(m.PROP_DEMAND[1], 0);
  assert.ok(m.PROP_DEMAND[2] < m.PROP_DEMAND[3] && m.PROP_DEMAND[3] < m.PROP_DEMAND[4]);
  assert.ok(m.PROP_SPEED[0] < 0 && m.PROP_SPEED[1] === 0 && m.PROP_SPEED[4] === 1);
});

test("the two floor tiles move the handle one notch per press, stop at the ends, one player is enough, out of reach does nothing", () => {
  const c = setup();
  const up = ctl("tele_up"), down = ctl("tele_down");
  c.state.players.a.x = up.x - 0.8; c.state.players.a.z = up.z;                // away from both tiles
  tick(c, press(c));
  assert.strictEqual(c.state.prop.pos, 1);
  stand(c, "tele_up");
  const seen = [];
  for (let i = 0; i < 5; i++) { tick(c, press(c)); seen.push(c.state.prop.pos); }
  assert.deepStrictEqual(seen, [2, 3, 4, 4, 4], "up to toute then the end stop");
  stand(c, "tele_down");
  for (let i = 0; i < 6; i++) tick(c, press(c));
  assert.strictEqual(c.state.prop.pos, 0, "down to arriere then the end stop");
});

test("the telegraph position adds its demand to the grid's, breakers untouched", () => {
  const c = setup();
  const base = m.gridDemand(c.state.power);
  for (let p = 0; p < 5; p++) { c.state.prop.pos = p; close(m.powerDemand(c.state), base + m.PROP_DEMAND[p], 1e-9); }
  assert.strictEqual(m.gridDemand(c.state.power), base);
});

test("cruise: lent and demi keep the lights bright, toute dims them to orange because the plant has no surplus left for that much propulsion", () => {
  const grid = (name) => { const c = setup("croisiere"); pos(c, name); ticks(c, 1500); return tick(c).pw; };
  assert.ok(grid("stop").v > 0.99);
  const lent = grid("lent"), demi = grid("demi"), toute = grid("toute");
  assert.ok(lent.v > 0.9 && lent.lt.every((b) => b === 3), "lent: bright, V=" + lent.v);
  assert.ok(demi.v >= 0.8 && demi.v < lent.v && demi.lt.every((b) => b === 3), "demi: still bright but dimmer, V=" + demi.v);
  assert.ok(toute.v >= 0.5 && toute.v < 0.8 && toute.lt.every((b) => b === 2), "toute: orange, V=" + toute.v);
});

test("full power: toute is sustained, the voltage stays at 1 and the boat reaches full speed", () => {
  const c = setup("pleine");
  pos(c, "toute");
  ticks(c, 1500);
  assert.ok(c.state.power.V > 0.99);
  close(c.state.prop.speed, m.PROP_VMAX, 0.05, "full speed");
});

test("the speed eases towards set-point x voltage with the inertia PROP_TAU", () => {
  const c = setup("pleine");
  pos(c, "toute");
  ticks(c, m.PROP_TAU * m.TICK_RATE);
  close(c.state.prop.speed, (1 - Math.exp(-1)) * m.PROP_VMAX, 0.1, "one time constant");
  const d = setup("pleine");
  pos(d, "lent");
  ticks(d, 1500);
  close(d.state.prop.speed, m.PROP_SPEED[2] * m.PROP_VMAX, 0.05, "lent");
});

test("astern: negative speed and the distance goes back", () => {
  const c = setup("croisiere");
  pos(c, "arriere");
  ticks(c, 600);
  assert.ok(c.state.prop.speed < -0.3 && c.state.prop.dist < 0);
});

test("the distance is the integral of the speed", () => {
  const c = setup("pleine");
  pos(c, "demi");
  let sum = 0;
  for (let i = 0; i < 400; i++) { tick(c); sum += c.state.prop.speed * m.DT; }
  close(c.state.prop.dist, sum, 1e-6);
});

test("a SCRAM drops the voltage at once, the boat coasts and slows down with its inertia, the handle stays where it was", () => {
  const c = setup("pleine");
  pos(c, "toute");
  ticks(c, 1500);
  const v0 = c.state.prop.speed;
  m.reactorScram(c.state.reactor);
  ticks(c, m.PROP_TAU * m.TICK_RATE);
  close(c.state.prop.speed, v0 * Math.exp(-1), 0.15, "one time constant after the SCRAM");
  ticks(c, 1200);
  assert.ok(c.state.prop.speed < 0.01);
  assert.strictEqual(c.state.prop.pos, m.PROP_NAMES.indexOf("toute"));
  assert.ok(c.state.prop.dist > 0);
});

test("noise: a stopped boat makes none, full speed is the maximum, and it grows with the speed", () => {
  const p = m.newPropulsion();
  assert.strictEqual(m.propulsionNoise(p), 0);
  p.speed = m.PROP_VMAX / 2;
  close(m.propulsionNoise(p), 2, 1e-9);
  p.speed = m.PROP_VMAX; close(m.propulsionNoise(p), 4, 1e-9);
  p.speed = -m.PROP_VMAX * 2; close(m.propulsionNoise(p), 4, 1e-9, "capped, and astern is as loud as ahead");
});

test("the telegraph and the speed are always in the broadcast (reconnection resync)", () => {
  const c = setup("pleine");
  pos(c, "demi");
  ticks(c, 300);
  const pr = tick(c).pr;
  assert.strictEqual(pr.p, 3);
  assert.ok(pr.s > 0 && pr.d > 0 && pr.n > 0);
});
