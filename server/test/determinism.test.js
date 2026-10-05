// Determinism guards for the fixed-step simulation (E1-06): the whole game state must be a pure function of the inputs.
// Run: node --test server/test/determinism.test.js
// If the GOLDEN digest test fails after a change you MEANT to make to the gameplay or the balance, copy the new digest printed in the failure
// message into GOLDEN_DIGEST below (one line) and say so in the commit: that line is the signature of "the game as designed today".
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const { createMatch, simulate, m } = require("./sim-harness.js");

const IDS = ["a", "b", "c"];
const TICKS = 3000;                         // 5 minutes of play
const GOLDEN_DIGEST = "0df9d13e3c7f2184";

// A small deterministic generator for the scripted "monkey" players (never Math.random).
function lcg(seed) { let s = seed >>> 0; return () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; }; }

// Where the monkeys go: every command, pump, valve, lever, station, toolbox and restart key of the boat.
function waypoints() {
  const pts = [];
  for (const c of m.CONTROLS) pts.push({ x: c.x - 0.4, z: c.z });
  for (const a of m.COUPLED_ACTIONS) { pts.push({ x: a.a.x, z: a.a.z }); pts.push({ x: a.b.x, z: a.b.z }); }
  for (const p of m.BILGE_PUMPS) pts.push({ x: p.x * 0.8, z: p.z });
  pts.push({ x: 1.0, z: 5.5 }, { x: 0, z: 0 }, { x: -2, z: -5 }, { x: 2, z: 3 });                // toolbox, centre, cargo areas
  return pts;
}

// Three scripted players: each picks a waypoint every 8 s, walks to it, and presses / holds / grabs at pseudo-random moments.
function monkeyScript(seed) {
  const pts = waypoints();
  const rnd = lcg(seed);
  const target = {}, seq = {};
  IDS.forEach((id) => { target[id] = pts[Math.floor(rnd() * pts.length)]; seq[id] = 0; });
  return {
    inputs(tick, state) {
      const out = [];
      for (const id of IDS) {
        if (tick % 80 === 0) target[id] = pts[Math.floor(rnd() * pts.length)];
        const p = state.players[id], dx = target[id].x - p.x, dz = target[id].z - p.z, d = Math.hypot(dx, dz);
        const near = d < 0.3;
        const r = rnd();
        out.push({ id, input: { seq: ++seq[id], mx: near ? 0 : dx / d, mz: near ? 0 : dz / d, act: near && r < 0.25, hold: near && r > 0.6, grab: r > 0.97 } });
      }
      return out;
    },
    events: {
      800: (s) => m.reactorBreakPump(s.reactor, 1),
      900: (s) => m.bilgeBreak(s.bilge, 0),
      1200: (s) => { m.reactorRepairPump(s.reactor, 1); m.bilgeRepair(s.bilge, 0); },
      1500: (s) => m.reactorScram(s.reactor),
      2000: (s) => m.reactorRestart(s.reactor)
    }
  };
}

function run(seed, onSecond) {
  const state = createMatch(IDS);
  const script = monkeyScript(seed);
  script.onTick = (s, t) => { if (t % 10 === 0 && onSecond) onSecond(s, t); };
  simulate(state, TICKS, script);
  return state;
}

function assertFinite(v, where) {
  if (typeof v === "number") assert.ok(isFinite(v), "non-finite number at " + where);
  else if (v && typeof v === "object") for (const k of Object.keys(v)) if (k !== "presence") assertFinite(v[k], where + "." + k);
}

test("two runs of the same 5-minute script give the identical full-state digest every single second", () => {
  const a = [], b = [];
  run(42, (s) => a.push(m.stateDigest(s)));
  run(42, (s) => b.push(m.stateDigest(s)));
  assert.strictEqual(a.length, TICKS / 10);
  assert.deepStrictEqual(a, b);
  assert.strictEqual(new Set(a).size > TICKS / 10 / 2, true, "the state really evolves (digests are not constant)");
});

test("the digest is sensitive: another script ends in another state", () => {
  assert.notStrictEqual(m.stateDigest(run(42)), m.stateDigest(run(43)));
});

test("the digest does not depend on key order and ignores the network presence handles", () => {
  assert.strictEqual(m.stateDigest({ a: 1, b: { c: [1, 2], d: "x" } }), m.stateDigest({ b: { d: "x", c: [1, 2] }, a: 1 }));
  assert.strictEqual(m.stateDigest({ p: { presence: { sessionId: "s1" }, x: 1 } }), m.stateDigest({ p: { presence: { sessionId: "other" }, x: 1 } }));
  assert.notStrictEqual(m.stateDigest({ a: 1 }), m.stateDigest({ a: 2 }));
});

test("monkey players for 5 minutes (breakdowns, a SCRAM and a restart included): no NaN / Infinity, everyone stays inside the boat, water within capacity", () => {
  run(7, (s, t) => {
    assertFinite(s, "state@" + t);
    for (const id of IDS) {
      const p = s.players[id];
      assert.ok(Math.abs(p.x) <= m.HALF_X + 1e-9 && Math.abs(p.z) <= m.HALF_Z + 1e-9, "player out of the boat at tick " + t);
    }
    s.water.comps.forEach((c, i) => assert.ok(c.w >= 0 && c.w <= m.WATER_COMPARTMENTS[i].volume + 1e-9, "water out of range in " + i + " at tick " + t));
  });
});

test("GOLDEN: the final digest of the reference script is the one recorded (the game as designed today)", () => {
  const digest = m.stateDigest(run(2024));
  assert.strictEqual(digest, GOLDEN_DIGEST,
    "The simulation no longer ends in the recorded state. If the change is intended, set GOLDEN_DIGEST = \"" + digest + "\" in server/test/determinism.test.js. " +
    "If it is NOT intended, something made the simulation non-deterministic or changed it by accident.");
});

// The simulation must not read the clock, a timer or an unseeded random source (architecture rule 1). Comments are ignored.
test("no simulation source uses Math.random, Date, performance.now or timers", () => {
  const dir = path.join(__dirname, "..", "src");
  const forbidden = /Math\.random|Date\.now|new Date|performance\.now|setTimeout|setInterval|process\.hrtime/;
  for (const file of fs.readdirSync(dir).filter((f) => f.endsWith(".js"))) {
    const code = fs.readFileSync(path.join(dir, file), "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    assert.ok(!forbidden.test(code), file + " uses a non-deterministic API");
  }
});

test("simStep alone advances the match: the tick, the reactor clock and the players follow the inputs, with no Nakama object involved", () => {
  const s = createMatch(["a"]);
  const x0 = s.players.a.x;
  simulate(s, 10, { inputs: (tick) => [{ id: "a", input: { seq: tick, mx: 1, mz: 0 } }] });
  assert.strictEqual(s.tick, 10);
  assert.ok(Math.abs(s.players.a.x - (x0 + 10 * m.MOVE_SPEED * m.DT)) < 1e-9);
  assert.ok(s.reactor.t > 0.99 && s.reactor.t < 1.01);
});
