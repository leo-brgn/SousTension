// ---- Fault generator with an attention budget (E3-09d, docs/design/alarm-system.md) -------------------------------------------------------------
// "Plus de systèmes que de joueurs": the game keeps the crew busy by letting faults pile up to a BUDGET. A fault is an entry of FAULTS: how many of its
// instances are currently unresolved (count), and how to start one (start). Every fault listed here can be resolved by the players TODAY (a hull
// patch, re-arming a breaker, opening a valve); faults that need a repair mechanic (a broken pump) join when that mechanic exists (E5 / E9).
// The generator is deterministic: a seeded generator stored in the state, never Math.random. It only runs when the match asks for it (state.faults.enabled:
// match parameter faults:true, which the real server sets, or the debug command `faults on`), so tests and tools that script their own faults are unaffected.
//   budget(tick) = min(players + 3, 1 + floor(seconds / FAULT_RAMP_SECONDS)): one fault at a time at first, one more every minute, up to the number of
//   players plus three ("plus de systèmes que de joueurs"). Tuned with the scripted crew (tools/playtest-bot.js): a crew that needs 90 s to react to each
//   problem still finishes the 1500 m, but only with 1 to 3 stars.
//   A new fault starts when the unresolved ones are under the budget and the pause since the last start is over; it comes from a system different
//   from the last one whenever another is possible.
var FAULT_FIRST_SECONDS = 60;      // grace period at the start of a patrol
var FAULT_RAMP_SECONDS = 60;       // one more simultaneous fault every minute
var FAULT_GAP_MIN = 10, FAULT_GAP_MAX = 30;   // seconds between two starts (when under the budget)

function faultRand(f) {
  f.rng = (f.rng + 0x6D2B79F5) | 0;
  var t = Math.imul(f.rng ^ (f.rng >>> 15), 1 | f.rng);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

var FAULTS = [
  { id: "leak", system: "water",
    count: function (s) { return s.leaks.list.length; },
    can: function (s) { return s.leaks.list.length < 3; },
    start: function (s, f) {
      var c = Math.min(WATER_COMPARTMENTS.length - 1, Math.floor(faultRand(f) * WATER_COMPARTMENTS.length)), comp = WATER_COMPARTMENTS[c];
      var z = comp.z0 + 0.5 + faultRand(f) * (comp.z1 - comp.z0 - 1);
      var sizeRoll = faultRand(f);
      leakCreate(s.leaks, z, faultRand(f) < 0.5 ? -1 : 1, sizeRoll < 0.4 ? 1 : (sizeRoll < 0.85 ? 2 : 3));
    } },
  { id: "breaker", system: "power",
    count: function (s) { var n = 0; for (var i = 0; i < s.power.br.length; i++) if (s.power.br[i].tripped) n++; return n; },
    can: function (s) { return faultClosedBreakers(s).length > 0; },
    start: function (s, f) { var list = faultClosedBreakers(s); breakerTrip(s.power, list[Math.floor(faultRand(f) * list.length)]); } },
  { id: "valve", system: "reactor",
    count: function (s) { var n = 0; for (var i = 0; i < 4; i++) if (s.reactor.valves[i] < 0.5) n++; return n; },
    can: function (s) { if (s.reactor.scram) return false; var open = []; for (var i = 0; i < 4; i++) if (s.reactor.valves[i] >= 0.5) open.push(i); return open.length > 0; },
    start: function (s, f) { var open = []; for (var i = 0; i < 4; i++) if (s.reactor.valves[i] >= 0.5) open.push(i); s.reactor.valves[open[Math.floor(faultRand(f) * open.length)]] = 0.15; } }
];

// The breakers that can trip without taking the lights of a compartment away (a fault, not a blackout): everything but the light breakers.
function faultClosedBreakers(s) {
  var out = [];
  for (var i = 0; i < s.power.br.length; i++) if (s.power.br[i].closed && BREAKERS[i].use !== "light") out.push(i);
  return out;
}

function newFaults(enabled) {
  return { enabled: !!enabled, rng: (REACTOR_SEED ^ 0x9E3779B9) | 0, nextTick: FAULT_FIRST_SECONDS * TICK_RATE, last: "", started: 0 };
}

function faultUnresolved(state) {
  var n = 0;
  for (var i = 0; i < FAULTS.length; i++) n += FAULTS[i].count(state);
  return n;
}

function faultBudget(state, tick) {
  var players = Math.max(1, state.order.length);
  return Math.min(players + 3, 1 + Math.floor(tick / TICK_RATE / FAULT_RAMP_SECONDS));
}

// The generator keeps its own clock: the ticks of the RUN (state.mission.runTicks), so the lobby and the briefing do not use up the grace period.
function faultsStep(state, tick) {
  var f = state.faults;
  tick = state.mission.runTicks;
  if (!f.enabled || tick < f.nextTick || state.order.length === 0) return;
  if (faultUnresolved(state) >= faultBudget(state, tick)) return;                         // the crew is busy enough: try again next tick
  var usable = [], other = [];
  for (var i = 0; i < FAULTS.length; i++) if (FAULTS[i].can(state)) { usable.push(FAULTS[i]); if (FAULTS[i].system !== f.last) other.push(FAULTS[i]); }
  if (usable.length === 0) return;
  var pool = other.length > 0 ? other : usable;
  var fault = pool[Math.floor(faultRand(f) * pool.length)];
  fault.start(state, f);
  f.last = fault.system; f.started += 1;
  f.nextTick = tick + Math.round((FAULT_GAP_MIN + faultRand(f) * (FAULT_GAP_MAX - FAULT_GAP_MIN)) * TICK_RATE);
}
