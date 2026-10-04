// PROTOTYPE JETABLE (E3-01) — sert uniquement à vérifier que les constantes de départ du document
// reactor-dependency-tree.md respectent les critères. L'implémentation réelle est E3-02 (serveur).
// Lancer : node docs/design/reactor-prototype.js
// Throwaway prototype to sanity-check starting constants for the reactor dependency tree (E3-01).
// The real implementation is E3-02 (server/src/reactor.js). Deterministic: seeded PRNG, fixed 0.1 s step.
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

const K = {
  dt: 0.1,
  rodRate: 0.02,            // /s
  regimes: { veille: 0.10, croisiere: 0.45, pleine: 0.90 },
  pMax: 100,                // MWth at rods = 1
  tauP: 10,                 // s, fission power lag after rod move
  decayFrac: 0.07, tauDecay: 180,
  coreC: 160,               // MJ/degC
  kCool: 1.3,               // MW/degC per unit flow
  tIn: 280,                 // degC
  flowPump: 0.5,            // per pump at full electric supply
  flowNat: 0.15,            // natural circulation (SCRAM / blackout)
  tauS: 40,                 // s steam pressure lag
  sPerDeg: 100 / 30,        // bar per degC above tIn (cruise ~310 degC -> 100 bar)
  tauE: 70,                 // s turbine lag
  eCoef: 0.13,              // MWe per bar of steam pressure
  pumpDemand: 4,            // MWe per pump
  tWarn: 350, tCrit: 370,
  driftMean: 6.5, driftMin: 0.05, driftMax: 0.15,
};

function makeReactor(seed) {
  const rng = mulberry32(seed);
  const s = { t: 0, regime: "croisiere", rod: 0.45, Pf: 45, A: 45, T: 310, S: 100, E: 14, valves: [1, 1, 1, 1], pumps: [true, true], scram: false, nextDrift: 30, rng };
  return s;
}

function step(s, K, driftScale) {
  const dt = K.dt;
  const target = s.scram ? 0 : K.regimes[s.regime];
  s.rod += Math.max(-K.rodRate * dt * (s.scram ? 20 : 1), Math.min(K.rodRate * dt * (s.scram ? 20 : 1), target - s.rod));
  s.Pf += (K.pMax * s.rod - s.Pf) * dt / K.tauP;
  s.A += (s.Pf - s.A) * dt / K.tauDecay;
  const P = s.Pf + K.decayFrac * s.A;
  // electricity: pumps on the essential bus first
  const pumpsOn = s.pumps.filter(Boolean).length;
  const essential = pumpsOn * K.pumpDemand;
  const Etarget = s.scram ? 0 : K.eCoef * s.S;   // the turbine is driven by STEAM pressure only (no direct link to thermal power)
  s.E += (Etarget - s.E) * dt / K.tauE;
  const eta = essential > 0 ? Math.min(1, s.E / essential) : 0;
  const valveFactor = s.valves.reduce((a, b) => a + b, 0) / 4;
  const flow = pumpsOn * K.flowPump * eta * valveFactor + K.flowNat;
  const Q = K.kCool * flow * (s.T - K.tIn);
  s.T += (P - Q) / K.coreC * dt;
  const Starget = Math.max(0, (s.T - K.tIn) * K.sPerDeg);
  s.S += (Starget - s.S) * dt / K.tauS;
  // coolant drift: random valve closes a bit; faster at higher rod position
  if (!s.scram) {
    s.nextDrift -= dt;
    if (s.nextDrift <= 0) {
      const i = Math.floor(s.rng() * 4);
      s.valves[i] = Math.max(0, s.valves[i] - (K.driftMin + s.rng() * (K.driftMax - K.driftMin)));
      s.nextDrift = K.driftMean / (driftScale * Math.max(0.01, s.rod * s.rod)) * (0.5 + s.rng());
    }
  }
  s.t += dt;
  return { P, E: s.E, eta, flow };
}

function settle(s, seconds) { for (let i = 0; i < seconds / K.dt; i++) step(s, K, 1); }

// 1) cruise settles
let s = makeReactor(1); settle(s, 600);
console.log("cruise steady: T=%s S=%s E=%s valves=%s", s.T.toFixed(1), s.S.toFixed(1), s.E.toFixed(1), s.valves.map(v => v.toFixed(2)).join(","));

// 2) full power unattended from steady cruise; time to critical, over several seeds
for (const seed of [1, 2, 3, 4, 5, 6]) {
  const r = makeReactor(seed); settle(r, 300); r.valves = [1, 1, 1, 1]; r.regime = "pleine";
  let tWarn = null, tCrit = null; const t0 = r.t; let o;
  for (let i = 0; i < 3000 / K.dt && tCrit === null; i++) { o = step(r, K, 1); if (tWarn === null && r.T >= K.tWarn) tWarn = r.t - t0; if (r.T >= K.tCrit) tCrit = r.t - t0; }
  console.log("seed %d full power: warn at %s s, crit at %s s | T=%s E=%s eta=%s flow=%s valves=%s", seed, tWarn && tWarn.toFixed(0), tCrit && tCrit.toFixed(0), r.T.toFixed(0), r.E.toFixed(1), o.eta.toFixed(2), o.flow.toFixed(2), r.valves.map(v => v.toFixed(2)).join(","));
}

// 3) veille stable
const v = makeReactor(7); v.regime = "veille"; settle(v, 900);
const ov = step(v, K, 1);
console.log("veille: T=%s S=%s E=%s eta=%s", v.T.toFixed(1), v.S.toFixed(1), v.E.toFixed(1), ov.eta.toFixed(2));

// 4) SCRAM from full power at steady state: electricity drops, time until T crit afterwards
const c = makeReactor(3); c.regime = "pleine"; settle(c, 120); c.valves = [1, 1, 1, 1]; settle(c, 60);
const Tbefore = c.T; c.scram = true; const t1 = c.t; let tc = null; let maxT = c.T;
for (let i = 0; i < 1800 / K.dt; i++) { step(c, K, 1); maxT = Math.max(maxT, c.T); if (tc === null && c.T >= K.tCrit) tc = c.t - t1; }
console.log("scram from full power: T before=%s, E after 1 s ~ drops to %s, maxT over 30 min=%s, time to crit=%s", Tbefore.toFixed(0), c.E.toFixed(2), maxT.toFixed(0), tc && tc.toFixed(0));
