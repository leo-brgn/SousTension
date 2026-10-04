// ---- Interactive controls (boat-local) ---------------------------------------------------------------------
// One interaction key ("act") serves every control: the server picks the nearest interactable within reach, so the
// client never has to say what it is pressing (and cannot cheat about it). Interlock stations live in interlock.js;
// the other controls are listed here.
//   regime : the RK-1 three-position selector (compartment 4, left wall). One press turns it to the next position
//            (Veille -> Croisiere -> Pleine -> Veille). A single player is enough: the Rule of Two Players covers
//            starting/stopping the reactor, not choosing a regime (GDD 3.3/3.4).
var CONTROLS = [
  { id: "regime", x: -2.5, z: -1.7, reach: 2.0 }
];
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
    return;
  }
  if (bestStationD < Infinity) tryActivate(state.il, id, pl, tick);
}

