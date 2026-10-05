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
  { id: "pump1", x: 2.5, z: 5.0, reach: 1.2, pump: 1 },
  { id: "bilge0", x: -2.5, z: 4.5, reach: 1.0, bilge: 0 },
  { id: "bilge1", x: 2.5, z: -5.0, reach: 0.9, bilge: 1 }
];
var VALVE_TURN_RATE = 0.25;        // valve opening per second while the wheel is held (4 s from closed to open)
(function addBreakerControls() {                        // the 20 breakers of the main panel (E3-07): floor tiles at the stern, left wall
  for (var i = 0; i < BREAKERS.length; i++)
    CONTROLS.push({ id: "breaker_" + BREAKERS[i].id, x: BREAKERS[i].x, z: BREAKERS[i].z, reach: BREAKERS[i].reach, breaker: i });
})();
CONTROLS.push({ id: "tele_up", x: -0.5, z: 4.2, reach: 0.3, tele: 1 }, { id: "tele_down", x: -0.5, z: 3.6, reach: 0.3, tele: -1 });   // machine telegraph (E3-08)
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
  if (!canUseHands(pl)) return;                        // E2-03: a hand must be free
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

// Press a control (the effect of one press, whoever chose it: the nearest-in-reach path or the aimed one).
function activateControl(state, c) {
  if (c.id === "regime") useRegimeSelector(state.reactor);
  else if (c.id === "scram") useScramLever(state.lever, state.reactor);
  else if (c.pump !== undefined) togglePump(state.reactor, c.pump);
  else if (c.bilge !== undefined) bilgeToggle(state.bilge, c.bilge);
  else if (c.breaker !== undefined) breakerToggle(state.power, c.breaker);
  else if (c.tele !== undefined) telegraphStep(state.prop, c.tele);
}

// The interaction key was pressed by player `id`: dispatch to the nearest interactable in reach (the position-based path: bots, tests, the
// key E). The aimed path is useTarget.
function tryAct(state, id, pl, tick) {
  if (tryRepair(state, id, pl, tick)) return;           // carrying a hull patch next to a leak: the press applies it (E6-02)
  if (tryScoop(state, id, pl, tick)) return;            // carrying the bucket in a flooded compartment: the press scoops (E6-03)
  var bestControl = null, bestControlD = Infinity;
  for (var i = 0; i < CONTROLS.length; i++) {
    var d = controlDistance(pl, CONTROLS[i]);
    if (d <= CONTROLS[i].reach && d < bestControlD) { bestControl = CONTROLS[i]; bestControlD = d; }
  }
  var cmd = nearestCommand(pl);
  var bestStationD = cmd ? cmd.d : Infinity;
  if (!canUseHands(pl)) return;                         // E2-03: pressing a control needs a free hand and no two-handed item
  if (bestControl && bestControlD < bestStationD) { activateControl(state, bestControl); return; }
  if (cmd) tryActivate(state.cp, id, pl, tick);
}

// ---- Aimed interaction (E2-02) ------------------------------------------------------------------------------------------------
// The client looks at an object and sends its id in the input field `use`; the server checks that the id exists and that the player is within
// arm's reach, then does what a press on that object does. Ids: a control id of CONTROLS ("regime", "scram", "valve0".."pump1", "bilge0",
// "breaker_<id>", "tele_up"...), "cc:<action>:<0|1>" for a command of the Rule of Two Players, "leak:<n>" for a hull leak, and "item" for
// "use what I hold" (a patch on a leak, the bucket in the water). Anything else, or out of reach, is ignored.
var AIM_REACH = 2.0;                 // m: arm's reach for every aimed control (the aim removes the ambiguity the small tiles had to solve by position)

function controlById(id) {
  for (var i = 0; i < CONTROLS.length; i++) if (CONTROLS[i].id === id) return CONTROLS[i];
  return null;
}

// Every id the server accepts, except the dynamic "leak:<n>": views and tests use it to stay in step with the server.
function interactableIds() {
  var ids = ["item"];
  for (var i = 0; i < CONTROLS.length; i++) ids.push(CONTROLS[i].id);
  for (var a = 0; a < COUPLED_ACTIONS.length; a++) { ids.push("cc:" + COUPLED_ACTIONS[a].id + ":0"); ids.push("cc:" + COUPLED_ACTIONS[a].id + ":1"); }
  return ids;
}

// A press (input flag act) on the object with id `target`.
function useTarget(state, id, pl, tick, target) {
  if (target === "item") { if (!tryRepair(state, id, pl, tick)) tryScoop(state, id, pl, tick); return; }
  if (target.indexOf("leak:") === 0) {
    var n = +target.slice(5);
    for (var l = 0; l < state.leaks.list.length; l++) if (state.leaks.list[l].id === n) { tryRepair(state, id, pl, tick, state.leaks.list[l]); return; }
    return;
  }
  if (!canUseHands(pl)) return;                         // E2-03: controls and commands need a free hand; the item uses above do not
  if (target.indexOf("cc:") === 0) {
    var parts = target.split(":");
    var side = +parts[2];
    if (parts.length !== 3 || (side !== 0 && side !== 1)) return;
    for (var a = 0; a < COUPLED_ACTIONS.length; a++) {
      if (COUPLED_ACTIONS[a].id !== parts[1]) continue;
      var cmd = side === 0 ? COUPLED_ACTIONS[a].a : COUPLED_ACTIONS[a].b;
      var dx = pl.x - cmd.x, dz = pl.z - cmd.z;
      if (Math.sqrt(dx * dx + dz * dz) <= Math.max(cmd.reach, AIM_REACH)) armCommand(state.cp, id, tick, a, side);
      return;
    }
    return;
  }
  var c = controlById(target);
  if (c && controlDistance(pl, c) <= AIM_REACH) activateControl(state, c);
}

// The key is HELD on the object with id `target`: only the valve wheels use it.
function holdTarget(state, pl, target) {
  if (!canUseHands(pl)) return;
  var c = controlById(target);
  if (c && c.valve !== undefined && controlDistance(pl, c) <= AIM_REACH) holdValve(state.reactor, c.valve);
}

