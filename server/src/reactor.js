// ---- Reactor RK-1 "Petit Soleil" (E3-02) -------------------------------------------------------------------
// Authoritative, deterministic model (fixed step = 1 tick = 0.1 s, seeded PRNG, no wall-clock). Design and
// constants: docs/design/reactor-dependency-tree.md. All constants are starting hypotheses to be tuned by playtests.
//
// Chain:  regime -> rods R -> fission power Pf -> (+ decay heat) -> heat P -> core temperature T
//         T -> steam pressure S -> turbine electricity E -> essential bus (primary pumps) -> cooling flow F -> T
// The cooling loop is fed by the electricity the reactor itself produces ("boucle diabolique"). The SCRAM drops
// every control rod and kills all electricity at once; only natural circulation is left.

var REACTOR_K = {
  rodRate: 0.02,           // rods travel per second (SCRAM: x20)
  scramRodFactor: 20,
  regimes: { veille: 0.10, croisiere: 0.45, pleine: 0.90 },
  pMax: 100,               // MWth with rods fully out
  tauP: 10,                // s, fission power lag
  decayFrac: 0.07,         // decay heat = 7 % of the smoothed power
  tauDecay: 180,           // s
  coreC: 160,              // MJ per degC (core thermal inertia)
  kCool: 1.3,              // MW per degC per unit of coolant flow
  tIn: 280,                // degC, coolant inlet
  flowPump: 0.5,           // flow per primary pump at full electric supply
  flowNat: 0.15,           // natural circulation (always there, only source after a SCRAM)
  tauS: 40,                // s, steam pressure lag
  barPerDeg: 100 / 30,     // steam pressure target per degC above the inlet
  tauE: 70,                // s, turbine/electrical lag
  eCoef: 0.13,             // MWe per bar of steam pressure
  pumpDemand: 4,           // MWe per running primary pump (essential bus)
  tWarn: 350,              // degC, alert threshold
  tCrit: 370,              // degC, critical threshold
  critHoldSeconds: 30,     // at T >= tCrit for this long: automatic protection (SCRAM + primary leak)
  firstDriftSeconds: 30,
  driftMeanSeconds: 5.0,   // mean delay between coolant-valve drifts at R = 1 (scales as 1 / R^2)
  driftMin: 0.05, driftMax: 0.15,
  settleSeconds: 900       // initial steady-state run (drift disabled)
};

// Seeded PRNG (mulberry32): the state is a single uint32 stored in the reactor, so runs are reproducible.
function reactorRand(r) {
  r.rng = (r.rng + 0x6D2B79F5) | 0;
  var t = Math.imul(r.rng ^ (r.rng >>> 15), 1 | r.rng);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

function newReactor(seed, regime) {
  regime = regime || "veille";
  var K = REACTOR_K;
  var R = K.regimes[regime];
  var r = {
    t: 0, seed: seed, rng: seed | 0, regime: regime,
    R: R, Pf: K.pMax * R, A: K.pMax * R, T: K.tIn + 20, S: 30, E: 0,
    valves: [1, 1, 1, 1], pumps: [true, true],
    scram: false, autoScram: false, leak: false, critTimer: 0,
    nextDrift: K.firstDriftSeconds, driftEnabled: false,
    eta: 0, flow: 0, P: 0
  };
  for (var i = 0; i < K.settleSeconds / DT; i++) reactorStep(r);   // reach the steady state of the chosen regime
  r.t = 0; r.driftEnabled = true; r.nextDrift = K.firstDriftSeconds;
  return r;
}

function reactorSetRegime(r, regime) {
  if (!REACTOR_K.regimes.hasOwnProperty(regime)) return false;
  r.regime = regime;
  return true;
}

function reactorSetValve(r, index, position) {
  if (index < 0 || index > 3) return false;
  r.valves[index] = clamp(position, 0, 1);
  return true;
}

function reactorSetPump(r, index, on) {
  if (index < 0 || index > 1) return false;
  r.pumps[index] = !!on;
  return true;
}

// SCRAM: any player, any time, no vote. All electricity is lost immediately.
function reactorScram(r) {
  r.scram = true;
  r.E = 0;
}

// Clears the SCRAM latch. The two-player procedure that guards it is E3-05; this is only the model side.
function reactorRestart(r) {
  r.scram = false; r.autoScram = false; r.critTimer = 0;
}

function reactorStep(r) {
  var K = REACTOR_K;
  var dt = DT;
  // 1. Rods follow the regime set-point (or drop on SCRAM)
  var target = r.scram ? 0 : K.regimes[r.regime];
  var rate = K.rodRate * dt * (r.scram ? K.scramRodFactor : 1);
  r.R += clamp(target - r.R, -rate, rate);
  // 2. Heat
  r.Pf += (K.pMax * r.R - r.Pf) * dt / K.tauP;
  r.A += (r.Pf - r.A) * dt / K.tauDecay;
  r.P = r.Pf + K.decayFrac * r.A;
  // 3. Electricity: the turbine is driven by steam only; pumps are on the essential bus (first priority)
  var pumpsOn = (r.pumps[0] ? 1 : 0) + (r.pumps[1] ? 1 : 0);
  var essential = pumpsOn * K.pumpDemand;
  var eTarget = r.scram ? 0 : K.eCoef * r.S;
  r.E += (eTarget - r.E) * dt / K.tauE;
  r.eta = essential > 0 ? Math.min(1, r.E / essential) : 0;
  // 4. Cooling flow and core temperature
  var valveFactor = (r.valves[0] + r.valves[1] + r.valves[2] + r.valves[3]) / 4;
  r.flow = pumpsOn * K.flowPump * r.eta * valveFactor + K.flowNat;
  r.T += (r.P - K.kCool * r.flow * (r.T - K.tIn)) / K.coreC * dt;
  // 5. Steam pressure follows the core temperature
  var sTarget = Math.max(0, (r.T - K.tIn) * K.barPerDeg);
  r.S += (sTarget - r.S) * dt / K.tauS;
  // 6. Random drift of the cooling valves ("active monitoring"): faster at higher rod position
  if (r.driftEnabled && !r.scram) {
    r.nextDrift -= dt;
    if (r.nextDrift <= 0) {
      var i = Math.floor(reactorRand(r) * 4);
      r.valves[i] = Math.max(0, r.valves[i] - (K.driftMin + reactorRand(r) * (K.driftMax - K.driftMin)));
      r.nextDrift = K.driftMeanSeconds / Math.max(0.01, r.R * r.R) * (0.5 + reactorRand(r));
    }
  }
  // 7. Automatic protection: critical temperature held too long -> SCRAM and a primary-circuit leak (never an explosion)
  if (r.driftEnabled && r.T >= K.tCrit) {
    r.critTimer += dt;
    if (!r.autoScram && r.critTimer >= K.critHoldSeconds) { r.autoScram = true; r.leak = true; reactorScram(r); }
  } else if (r.driftEnabled) {
    r.critTimer = 0;
  }
  r.t += dt;
}

// Noise made by the plant, 0 (quiet, Veille) to 4 (loud, Pleine puissance): follows the REAL rod position, so it lags the
// regime selector like everything else. The boat's total noise (E8-01) will add pumps, impacts and voices on top.
function reactorNoise(r) {
  var lo = REACTOR_K.regimes.veille, hi = REACTOR_K.regimes.pleine;
  return 4 * clamp((r.R - lo) / (hi - lo), 0, 1);
}

// Gauge values broadcast to clients (rounded: keeps the payload small and the display stable).
function reactorView(r) {
  function q(x) { return Math.round(x * 100) / 100; }
  return {
    reg: r.regime, R: q(r.R), nz: q(reactorNoise(r)), P: q(r.P), T: q(r.T), S: q(r.S), E: q(r.E), eta: q(r.eta), F: q(r.flow),
    v: [q(r.valves[0]), q(r.valves[1]), q(r.valves[2]), q(r.valves[3])],
    pu: [r.pumps[0] ? 1 : 0, r.pumps[1] ? 1 : 0],
    scram: r.scram ? 1 : 0, auto: r.autoScram ? 1 : 0, leak: r.leak ? 1 : 0,
    warn: r.T >= REACTOR_K.tWarn ? 1 : 0, crit: r.T >= REACTOR_K.tCrit ? 1 : 0
  };
}

