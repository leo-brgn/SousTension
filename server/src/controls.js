// ---- Interactive controls (boat-local) ---------------------------------------------------------------------
// One interaction key ("act") serves every control: the server picks the nearest interactable within reach, so the
// client never has to say what it is pressing (and cannot cheat about it). Interlock stations live in interlock.js;
// the other controls are listed here.
//   regime : the RK-1 three-position selector (compartment 4, left wall). One press turns it to the next position
//            (Veille -> Croisiere -> Pleine -> Veille). A single player is enough: the Rule of Two Players covers
//            starting/stopping the reactor, not choosing a regime (GDD 3.3/3.4).
//   scram  : the SCRAM lever under its sealed cover (E3-04), 1 m from the selector. Two presses, one player, no vote: the first lifts
//            the cover (it falls shut again after LEVER_COVER_TICKS), the second, cover open, pulls the lever. This is the one critical
//            action that is deliberately NOT under the Rule of Two Players (GDD 3.3). Pulling it again does nothing; restarting is E3-05.
var CONTROLS = [
  { id: "regime", x: -2.5, z: -1.7, reach: 2.0 },
  { id: "scram", x: -2.5, z: -2.7, reach: 1.5 }
];
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

function newLever() { return { cover: 0 }; }   // cover = ticks left before the cover falls shut (0 = closed)

// First press lifts the cover, second press (cover open) pulls the lever. Returns true when the SCRAM was triggered.
function useScramLever(lever, reactor) {
  if (reactor.scram) return false;
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
  var bestControl = null, bestControlD = Infinity;
  for (var i = 0; i < CONTROLS.length; i++) {
    var d = controlDistance(pl, CONTROLS[i]);
    if (d <= CONTROLS[i].reach && d < bestControlD) { bestControl = CONTROLS[i]; bestControlD = d; }
  }
  var bestStationD = Infinity;
  for (var s = 0; s < STATIONS.length; s++) {
    var dx = pl.x - STATIONS[s].x, dz = pl.z - STATIONS[s].z;
    var ds = Math.sqrt(dx * dx + dz * dz);
    if (ds <= STATION_REACH && ds < bestStationD) bestStationD = ds;
  }
  if (bestControl && bestControlD < bestStationD) {
    if (bestControl.id === "regime") useRegimeSelector(state.reactor);
    else if (bestControl.id === "scram") useScramLever(state.lever, state.reactor);
    return;
  }
  if (bestStationD < Infinity) tryActivate(state.il, id, pl, tick);
}

