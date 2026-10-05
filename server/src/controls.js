// ---- Interactive controls (boat-local) ---------------------------------------------------------------------
// One interaction key ("act") serves every control: the server picks the nearest interactable within reach, so the
// client never has to say what it is pressing (and cannot cheat about it). Coupled-action commands live in coupled.js;
// the other controls are listed here.
//   regime : the RK-1 three-position selector (compartment 4, left wall). One press turns it to the next position
//            (Veille -> Croisiere -> Pleine -> Veille). A single player is enough: the Rule of Two Players covers
//            starting/stopping the reactor, not choosing a regime (GDD 3.3/3.4).
//   scram  : the SCRAM lever under its sealed cover (E3-04), 1 m from the selector. Two presses, one player, no vote: the first lifts
//            the cover (it falls shut again after LEVER_COVER_TICKS), the second, cover open, pulls the lever. This is the one critical
//            action that is deliberately NOT under the Rule of Two Players (GDD 3.3). Pulling it again does nothing; restarting is E3-05.
//   valve0..3 : the four primary-circuit valve handwheels (E3-06), right wall. HOLD the interaction key to turn one open (VALVE_TURN_RATE per
//            second); drift closes them, only players open them. One player is enough.
//   pump0..1  : the two primary pumps' switches (E3-06). One press toggles run/stop; a broken pump cannot be started (repair is E5/E9).
var CONTROLS = [
  { id: "regime", x: -2.5, z: -1.7, reach: 2.0 },
  { id: "scram", x: -2.5, z: -2.7, reach: 1.5 },
  { id: "valve0", x: 2.5, z: -3.0, reach: 1.0, valve: 0 },
  { id: "valve1", x: 2.5, z: -1.5, reach: 1.0, valve: 1 },
  { id: "valve2", x: 2.5, z: 0.0, reach: 1.0, valve: 2 },
  { id: "valve3", x: 2.5, z: 1.5, reach: 1.0, valve: 3 },
  { id: "pump0", x: 2.5, z: 3.5, reach: 1.2, pump: 0 },
  { id: "pump1", x: 2.5, z: 5.0, reach: 1.2, pump: 1 }
];
var VALVE_TURN_RATE = 0.25;        // valve opening per second while the wheel is held (4 s from closed to open)
var LEVER_COVER_TICKS = 60;        // 6 s at 10 Hz
var REGIME_ORDER = ["veille", "croisiere", "pleine"];

function controlDistance(pl, c) {
  var dx = pl.x - c.x, dz = pl.z - c.z;
  return Math.sqrt(dx * dx + dz * dz);
}

// Turn the selector one position. Ignored while the reactor is SCRAMmed (restarting is the E3-05 procedure).
function useRegimeSelector(reactor) {
  if (reactor.scram) return false;
  var next = REGIME_ORDER[(REGIME_ORDER.indexOf(reactor.regime) + 1) % REGIME_ORDER.length];
  return reactorSetRegime(reactor, next);
}

// Hold the interaction key near a valve wheel: it turns open by VALVE_TURN_RATE * DT per applied input (one input per tick).
function holdValve(reactor, index) {
  return reactorSetValve(reactor, index, reactor.valves[index] + VALVE_TURN_RATE * DT);
}

function togglePump(reactor, index) {
  return reactorSetPump(reactor, index, !reactor.pumps[index]);
}

// The nearest control within its reach (or null).
function nearestControl(pl) {
  var best = null, bestD = Infinity;
  for (var i = 0; i < CONTROLS.length; i++) {
    var d = controlDistance(pl, CONTROLS[i]);
    if (d <= CONTROLS[i].reach && d < bestD) { best = CONTROLS[i]; bestD = d; }
  }
  return best;
}

// The interaction key is HELD (input flag `hold`, sent every tick while the key is down): only valve wheels use it.
function tryHold(state, pl) {
  var c = nearestControl(pl);
  if (c && c.valve !== undefined) holdValve(state.reactor, c.valve);
}

// cover = ticks left before the cover falls shut (0 = closed); reset = the lever was put back after a SCRAM (restart step 1, E3-05)
function newLever() { return { cover: 0, reset: false }; }

// First press lifts the cover, second press (cover open) pulls the lever. Returns true when the SCRAM was triggered.
function useScramLever(lever, reactor) {
  if (reactor.scram) { lever.reset = true; return false; }   // a press on a pulled lever puts it back (restart step 1); the latch stays until the restart
  if (lever.cover <= 0) { lever.cover = LEVER_COVER_TICKS; return false; }
  reactorScram(reactor);
  lever.cover = 0;
  return true;
}

function leverStep(lever) { if (lever.cover > 0) lever.cover--; }

// Broadcast: cv = cover open (stays open while the lever is down), pl = lever pulled (the reactor latch, so a restart resets both).
function leverView(lever, reactor) {
  return { cv: (lever.cover > 0 || reactor.scram) ? 1 : 0, pl: reactor.scram ? 1 : 0 };
}

// The interaction key was pressed by player `id`: dispatch to the nearest interactable in reach.
function tryAct(state, id, pl, tick) {
  if (tryRepair(state, id, pl, tick)) return;           // carrying a hull patch next to a leak: the press applies it (E6-02)
  var bestControl = null, bestControlD = Infinity;
  for (var i = 0; i < CONTROLS.length; i++) {
    var d = controlDistance(pl, CONTROLS[i]);
    if (d <= CONTROLS[i].reach && d < bestControlD) { bestControl = CONTROLS[i]; bestControlD = d; }
  }
  var cmd = nearestCommand(pl);
  var bestStationD = cmd ? cmd.d : Infinity;
  if (bestControl && bestControlD < bestStationD) {
    if (bestControl.id === "regime") useRegimeSelector(state.reactor);
    else if (bestControl.id === "scram") useScramLever(state.lever, state.reactor);
    else if (bestControl.pump !== undefined) togglePump(state.reactor, bestControl.pump);
    return;
  }
  if (cmd) tryActivate(state.cp, id, pl, tick);
}

