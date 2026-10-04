// Acceptance tests for the reactor model (E3-02). Design: docs/design/reactor-dependency-tree.md
// Run: node --test server/test/reactor.test.js
const test = require("node:test");
const assert = require("node:assert");
const m = require("../modules/index.js");

const K = m.REACTOR_K;
const DT = m.DT;
const seconds = (s) => Math.round(s / DT);

function run(r, secs) { for (let i = 0; i < seconds(secs); i++) m.reactorStep(r); }

// Run until predicate is true; returns elapsed seconds (or null after limit).
function until(r, pred, limitSecs) {
  const t0 = r.t;
  for (let i = 0; i < seconds(limitSecs); i++) { m.reactorStep(r); if (pred(r)) return r.t - t0; }
  return null;
}

test("deterministic: same seed and same commands give the same state; another seed drifts differently", () => {
  const a = m.newReactor(7, "croisiere"), b = m.newReactor(7, "croisiere"), c = m.newReactor(8, "croisiere");
  for (const r of [a, b, c]) { m.reactorSetRegime(r, "pleine"); run(r, 120); }
  assert.deepStrictEqual(a, b);
  assert.notDeepStrictEqual(a.valves, c.valves);
});

test("veille is stable: temperature settles, pumps are electrically starved (eta < 1), no alarm", () => {
  const r = m.newReactor(1, "veille");
  run(r, 300);
  const T1 = r.T; run(r, 300);
  assert.ok(r.T > K.tIn && r.T < K.tWarn - 30, "T=" + r.T);
  assert.ok(Math.abs(r.T - T1) < 0.5, "not settled: " + (r.T - T1));
  assert.ok(r.E > 0 && r.eta < 1, "E=" + r.E + " eta=" + r.eta + " (veille only covers the essential bus partially)");
  assert.ok(!r.scram && !r.leak);
});

test("full power without any intervention reaches the critical temperature in 4 min +/- 40 s (6 seeds)", () => {
  for (const seed of [1, 2, 3, 4, 5, 6]) {
    const r = m.newReactor(seed, "croisiere");
    m.reactorSetRegime(r, "pleine");
    const tCrit = until(r, (x) => x.T >= K.tCrit, 600);
    assert.ok(tCrit !== null && tCrit >= 200 && tCrit <= 280, "seed " + seed + ": critical at " + tCrit + " s");
  }
});

test("cruise drifts slowly: no alarm in the first 5 minutes without intervention", () => {
  for (const seed of [1, 2, 3]) {
    const r = m.newReactor(seed, "croisiere");
    run(r, 300);
    assert.ok(r.T < K.tWarn, "seed " + seed + ": T=" + r.T);
  }
});

test("SCRAM kills the electricity at once and the core stays safe (natural circulation only)", () => {
  const r = m.newReactor(3, "pleine");
  r.driftEnabled = false;                                   // an attentive crew: valves fully open
  run(r, 120);
  m.reactorScram(r);
  assert.strictEqual(r.E, 0);
  m.reactorStep(r);
  assert.strictEqual(r.eta, 0);
  assert.ok(Math.abs(r.flow - K.flowNat) < 1e-9);          // pumps dead: only natural circulation is left
  let maxT = 0; const t0 = r.t;
  for (let i = 0; i < seconds(1800); i++) { m.reactorStep(r); maxT = Math.max(maxT, r.T); }
  assert.ok(r.R < 0.01, "rods should be fully in, R=" + r.R);
  assert.ok(maxT < K.tCrit - 15, "core must stay safe after a SCRAM, maxT=" + maxT);
  assert.ok(!r.leak && !r.autoScram);
});

// Valves are closed progressively until the core reaches startT, then a player SCRAMs: the hard power cut costs a few
// degrees of overshoot (pumps stop at once) but the SCRAM must still save the core before the critical threshold.
function scramFrom(startT) {
  const r = m.newReactor(1, "pleine"); r.driftEnabled = false;
  let v = 1;
  while (r.T < startT && v > 0.05) { v -= 0.002; for (let i = 0; i < 4; i++) m.reactorSetValve(r, i, v); run(r, 2); }
  r.driftEnabled = true;
  const T0 = r.T; m.reactorScram(r);
  let maxT = r.T; for (let i = 0; i < seconds(900); i++) { m.reactorStep(r); maxT = Math.max(maxT, r.T); }
  return { T0, maxT, leak: r.leak, auto: r.autoScram };
}

test("a SCRAM at the alert level (355 degC) or just below critical (365 degC) saves the core: no leak", () => {
  for (const startT of [355, 365]) {
    const s = scramFrom(startT);
    assert.ok(!s.leak && !s.auto, "SCRAM at " + s.T0.toFixed(1) + " degC: peak " + s.maxT.toFixed(1) + ", leak=" + s.leak);
  }
});

test("chain delays: heat first, steam ~90 s, electricity ~2-3 min (step cruise -> full power)", () => {
  const r = m.newReactor(1, "croisiere");
  const T0 = r.T, S0 = r.S, E0 = r.E;
  r.driftEnabled = false;                                   // isolate the delay chain from the random drift
  m.reactorSetRegime(r, "pleine");
  const tT = until(r, (x) => x.T >= T0 + 5, 300);
  const r2 = m.newReactor(1, "croisiere"); r2.driftEnabled = false; m.reactorSetRegime(r2, "pleine");
  const tS = until(r2, (x) => x.S >= S0 * 1.25, 600);
  const r3 = m.newReactor(1, "croisiere"); r3.driftEnabled = false; m.reactorSetRegime(r3, "pleine");
  const tE = until(r3, (x) => x.E >= E0 * 1.25, 600);
  assert.ok(tT < tS && tS < tE, "order must be heat < steam < electricity: " + [tT, tS, tE]);
  assert.ok(tS >= 80 && tS <= 130, "steam +25% at " + tS + " s");
  assert.ok(tE >= 110 && tE <= 180, "electricity +25% at " + tE + " s");
});

test("boucle diabolique: raising power from veille heats the core while the pumps are still starved", () => {
  const r = m.newReactor(1, "veille");
  r.driftEnabled = false;
  const etaIdle = r.eta;
  m.reactorSetRegime(r, "pleine");
  let minEta = 1, ticksStarvedAndHeating = 0;
  for (let i = 0; i < seconds(120); i++) {
    const T = r.T; m.reactorStep(r);
    minEta = Math.min(minEta, r.eta);
    if (r.eta < 0.8 && r.T > T) ticksStarvedAndHeating++;
  }
  assert.ok(minEta < 0.8 && etaIdle < 1, "pumps should be starved: min eta " + minEta);
  assert.ok(ticksStarvedAndHeating > seconds(30), "core must heat while the pumps are starved: " + ticksStarvedAndHeating + " ticks");
});

test("valves drift faster at higher power (full > cruise > veille)", () => {
  const closed = (regime) => { let sum = 0; for (const seed of [1, 2, 3, 4]) { const r = m.newReactor(seed, regime); r.driftEnabled = true; run(r, 300); sum += 4 - r.valves.reduce((a, b) => a + b, 0); } return sum; };
  const veille = closed("veille"), croisiere = closed("croisiere"), pleine = closed("pleine");
  assert.ok(pleine > croisiere && croisiere > veille, "closed totals " + [veille, croisiere, pleine]);
});

test("active monitoring works: reopening the valves keeps full power below critical for 15 min", () => {
  for (const seed of [1, 2, 3]) {
    const r = m.newReactor(seed, "croisiere");
    m.reactorSetRegime(r, "pleine");
    let maxT = 0;
    for (let i = 0; i < seconds(900); i++) {
      if (i % seconds(10) === 0) for (let v = 0; v < 4; v++) m.reactorSetValve(r, v, 1);   // an attentive crew
      m.reactorStep(r); maxT = Math.max(maxT, r.T);
    }
    assert.ok(maxT < K.tCrit && !r.leak, "seed " + seed + ": maxT=" + maxT);
  }
});

test("automatic protection: critical temperature held 30 s -> SCRAM + primary leak, never an explosion", () => {
  const r = m.newReactor(1, "pleine");
  r.driftEnabled = true;
  for (let v = 0; v < 4; v++) m.reactorSetValve(r, v, 0.15);       // nobody cares: cooling collapses
  const t = until(r, (x) => x.autoScram, 900);
  assert.ok(t !== null, "protection never triggered");
  assert.ok(r.leak && r.scram && r.E === 0);
});

test("a short excursion above critical does not trigger the protection", () => {
  const r = m.newReactor(1, "pleine");
  for (let v = 0; v < 4; v++) m.reactorSetValve(r, v, 0.2);
  until(r, (x) => x.T >= K.tCrit, 900);
  assert.ok(r.T >= K.tCrit);
  run(r, 10);
  for (let v = 0; v < 4; v++) m.reactorSetValve(r, v, 1);          // crew reacts within 10 s
  run(r, 120);
  assert.ok(!r.autoScram && !r.leak);
});

test("losing a pump halves the electrical cooling capacity; restart clears the SCRAM latch", () => {
  const r = m.newReactor(1, "croisiere");
  r.driftEnabled = false;
  run(r, 60);
  const both = r.flow;
  m.reactorSetPump(r, 0, false);
  run(r, 5);
  assert.ok(r.flow < both - 0.2, "flow " + both + " -> " + r.flow);
  m.reactorScram(r);
  assert.ok(r.scram);
  m.reactorRestart(r);
  assert.ok(!r.scram && !r.autoScram);
});

test("the view exposes rounded gauges for the clients", () => {
  const r = m.newReactor(1, "croisiere");
  const v = m.reactorView(r);
  for (const k of ["reg", "R", "P", "T", "S", "E", "eta", "F", "v", "pu", "scram", "auto", "leak", "warn", "crit"]) assert.ok(k in v, "missing " + k);
  assert.strictEqual(v.v.length, 4);
  assert.strictEqual(v.pu.length, 2);
  assert.ok(Math.abs(v.T * 100 - Math.round(v.T * 100)) < 1e-6, "gauges are rounded to 0.01");
});

test("match integration: the reactor advances one tick per match tick and is broadcast as rx", () => {
  const h = m.handlers;
  const nk = { binaryToString: (d) => d };
  const logger = { info() {} };
  let { state } = h.matchInit({}, logger, nk, {});
  const sent = [];
  const d = { broadcastMessage: (op, data) => sent.push(JSON.parse(data)) };
  const t0 = state.reactor.t;
  for (let t = 1; t <= 10; t++) state = h.matchLoop({}, logger, nk, d, t, state, []).state;
  assert.ok(Math.abs(state.reactor.t - t0 - 10 * DT) < 1e-9);
  assert.ok(sent[sent.length - 1].rx && sent[sent.length - 1].rx.T > K.tIn);
  assert.strictEqual(sent[sent.length - 1].rx.reg, "veille");
});

test("noise follows the real rod position: 0 in veille, rising with the rods, 4 at full power, 0 after a SCRAM", () => {
  const r = m.newReactor(1, "veille");
  r.driftEnabled = false;
  assert.ok(m.reactorNoise(r) < 0.05, "veille noise " + m.reactorNoise(r));
  m.reactorSetRegime(r, "pleine");
  run(r, 10);
  const early = m.reactorNoise(r);
  assert.ok(early > 0.2 && early < 3, "noise follows the rods, not the selector: " + early);   // rods travel 0.02/s
  run(r, 60);
  assert.ok(m.reactorNoise(r) > 3.95, "full power noise " + m.reactorNoise(r));
  m.reactorScram(r);
  run(r, 5);
  assert.ok(m.reactorNoise(r) < 0.05, "after the SCRAM the rods are in: " + m.reactorNoise(r));
  const mid = m.newReactor(1, "croisiere");
  assert.ok(Math.abs(m.reactorNoise(mid) - 1.75) < 0.1, "cruise ~ two dots: " + m.reactorNoise(mid));
  assert.ok("nz" in m.reactorView(mid));
});
