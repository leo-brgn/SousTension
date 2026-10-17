// Balance tool (E3-10): runs the authoritative simulation headless, with scripted scenarios and several reactor seeds, and reports what happened.
// No Nakama, no network, no clock: the same code as the match (simStep), so a result here is a result in the game.
//
//   node tools/balance.js                    all scenarios, seeds 1..5, table
//   node tools/balance.js --seeds 20         more seeds
//   node tools/balance.js --scenario cruise  one scenario
//   node tools/balance.js --minutes 20       longer runs (default 10)
//   node tools/balance.js --json             machine-readable report
//
// Exit code 1 when an invariant is broken (a NaN, water above a compartment's capacity, a player out of the boat) or when two runs of the same
// scenario and seed disagree (determinism, architecture rule 1).
const { createMatch, simulate, m } = require("../server/test/sim-harness.js");

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const SEEDS = parseInt(opt("seeds", "5"), 10);
const MINUTES = parseFloat(opt("minutes", "10"));
const ONLY = opt("scenario", null);
const JSON_OUT = args.includes("--json");
const TICKS = Math.round(MINUTES * 60 * m.TICK_RATE);

// A scenario prepares the state (once) and may act at given ticks. Nobody walks: this tool measures the plant and the hull, not the players.
const SCENARIOS = {
  "idle-veille": {
    about: "veille, nobody touches anything",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "veille"); s.leaks.nextTick = 1e9; },
  },
  "cruise": {
    about: "croisiere, nobody touches anything (the reactor drifts, the automatic protection may trip)",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "croisiere"); s.leaks.nextTick = 1e9; },
  },
  "full-power": {
    about: "pleine puissance, nobody touches anything",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "pleine"); s.leaks.nextTick = 1e9; },
  },
  "unattended-leak": {
    about: "croisiere, a medium leak opens at 1 min in the central post and nobody patches it, the bilge pumps stay off",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "croisiere"); s.leaks.nextTick = 1e9; },
    events: { 600: (s) => m.leakCreate(s.leaks, 7, 1, 2) },
  },
  "scram-restart": {
    about: "croisiere, SCRAM at 1 min, restart (valves open, pumps on) at 3 min",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "croisiere"); s.leaks.nextTick = 1e9; },
    events: {
      600: (s) => m.reactorScram(s.reactor),
      1800: (s) => { for (let i = 0; i < 4; i++) m.reactorSetValve(s.reactor, i, 0.6); m.reactorSetPump(s.reactor, 0, true); m.reactorSetPump(s.reactor, 1, true); m.reactorRestart(s.reactor); },
    },
  },
  "scheduled-leaks": {
    about: "croisiere with the game's own leak schedule and nobody repairing: how long before the boat is in trouble",
    setup: (s, seed) => { s.reactor = m.newReactor(seed, "croisiere"); },
  },
};

function finite(v, where, bad) {
  if (typeof v === "number") { if (!isFinite(v)) bad.push("non-finite number at " + where); }
  else if (v && typeof v === "object") for (const k of Object.keys(v)) if (k !== "presence") finite(v[k], where + "." + k, bad);
}

function run(name, seed) {
  const sc = SCENARIOS[name];
  const state = createMatch(["a", "b"]);
  sc.setup(state, seed);
  const r = { scenario: name, seed, minutes: MINUTES, maxT: 0, minV: 1, minE: Infinity, maxWaterM3: 0, maxTrimDeg: 0, maxDepth: 0, firstWarnS: null, firstScramS: null, firstFloodS: null, violations: [] };
  const script = {
    events: sc.events || {},
    onTick: (s, t) => {
      const sec = t / m.TICK_RATE;
      r.maxT = Math.max(r.maxT, s.reactor.T);
      r.minV = Math.min(r.minV, s.power.V);
      r.minE = Math.min(r.minE, s.reactor.E);
      const water = m.waterTotal(s.water);
      r.maxWaterM3 = Math.max(r.maxWaterM3, water);
      r.maxTrimDeg = Math.max(r.maxTrimDeg, Math.abs(m.waterTilt(s.water).trim));
      r.maxDepth = Math.max(r.maxDepth, s.boat.depth);
      if (r.firstWarnS === null && s.reactor.T >= m.REACTOR_WARN) r.firstWarnS = sec;
      if (r.firstScramS === null && s.reactor.scram) r.firstScramS = sec;
      if (r.firstFloodS === null && s.water.comps.some((c, i) => c.w >= m.WATER_COMPARTMENTS[i].volume - 1e-9)) r.firstFloodS = sec;
      for (let i = 0; i < s.water.comps.length; i++) if (s.water.comps[i].w > m.WATER_COMPARTMENTS[i].volume + 1e-6) r.violations.push("water above capacity in compartment " + (i + 1) + " at " + sec + " s");
      for (const id of s.order) { const p = s.players[id]; if (Math.abs(p.x) > m.HALF_X + 1e-9 || Math.abs(p.z) > m.HALF_Z + 1e-9) r.violations.push("player " + id + " out of the boat at " + sec + " s"); }
    },
  };
  simulate(state, TICKS, script);
  finite(state, "state", r.violations);
  r.digest = m.stateDigest(state);
  r.finalT = state.reactor.T; r.finalV = state.power.V; r.scram = !!state.reactor.scram;
  return r;
}

if (!m.REACTOR_WARN) m.REACTOR_WARN = (m.REACTOR_K && m.REACTOR_K.tWarn) || Infinity;

const names = Object.keys(SCENARIOS).filter((n) => !ONLY || n === ONLY);
if (names.length === 0) { console.error("unknown scenario: " + ONLY + " (" + Object.keys(SCENARIOS).join(", ") + ")"); process.exit(2); }
const report = [];
let failed = false;
for (const name of names) {
  for (let seed = 1; seed <= SEEDS; seed++) {
    const a = run(name, seed), b = run(name, seed);
    if (a.digest !== b.digest) { a.violations.push("NOT DETERMINISTIC: two runs end in different states (" + a.digest + " vs " + b.digest + ")"); }
    if (a.violations.length) failed = true;
    report.push(a);
  }
}

if (JSON_OUT) console.log(JSON.stringify(report, null, 1));
else {
  const f = (v, d) => (v === null || v === undefined ? "-" : typeof v === "number" ? (isFinite(v) ? v.toFixed(d === undefined ? 1 : d) : "inf") : String(v));
  for (const name of names) {
    const rows = report.filter((r) => r.scenario === name);
    console.log("\n== " + name + " — " + SCENARIOS[name].about + " (" + MINUTES + " min, " + SEEDS + " seeds)");
    console.log("seed  maxT  minV  minE(MWe)  water(m3)  trim(deg)  depth(m)  warn@s  scram@s  flooded@s  end");
    for (const r of rows)
      console.log([r.seed, f(r.maxT, 0), f(r.minV, 2), f(r.minE, 1), f(r.maxWaterM3, 1), f(r.maxTrimDeg, 1), f(r.maxDepth, 1), f(r.firstWarnS, 0), f(r.firstScramS, 0), f(r.firstFloodS, 0), r.scram ? "SCRAM" : "running"].map((x, i) => String(x).padStart(i === 0 ? 4 : i === 10 ? 8 : 7)).join(" "));
    const scrams = rows.filter((r) => r.firstScramS !== null).length;
    console.log("-> " + scrams + "/" + rows.length + " runs went to SCRAM; max temperature " + f(Math.max(...rows.map((r) => r.maxT)), 0) + " C; max water " + f(Math.max(...rows.map((r) => r.maxWaterM3)), 1) + " m3");
  }
  const bad = report.flatMap((r) => r.violations.map((v) => r.scenario + " seed " + r.seed + ": " + v));
  console.log(bad.length ? "\nINVARIANTS BROKEN:\n  " + bad.slice(0, 20).join("\n  ") : "\nAll invariants hold, every run is deterministic.");
}
process.exit(failed ? 1 : 0);
