// ---- Bilge pumps and the bucket (E6-03) -------------------------------------------------------------------------------------
// Two electric bilge pumps each pump the water out of THEIR OWN compartment at BILGE_CAPACITY (water in neighbouring compartments reaches it
// through the open bulkhead openings, E6-01). A pump runs only while it is switched on, not broken and the plant produces electricity
// (reactor.E above BILGE_MIN_E): a SCRAM stops them and the boat is bailed by hand. That electricity rule is provisional until the real
// electrical network (E3-07). Bucket: a light cargo item (the toolbox of compartment 2); carried into a flooded compartment, one press
// scoops BUCKET_VOLUME out, at most once per BUCKET_COOLDOWN_TICKS per player. The mop waits for the spills of E6-05.
var BILGE_PUMPS = [
  { id: "bilge0", comp: 1, x: -2.5, z: 4.5, reach: 1.0 },     // compartment 2, left wall
  { id: "bilge1", comp: 4, x: 2.5, z: -5.0, reach: 0.9 }      // compartment 5, right wall
];
var BILGE_CAPACITY = 0.15;         // m3/s per pump
var BILGE_MIN_E = 0.5;             // MWe the plant must produce for the pumps to turn
var BUCKET_VOLUME = 0.015;         // m3 (15 L) per scoop
var BUCKET_COOLDOWN_TICKS = 15;    // 1.5 s per player between two scoops

function newBilge() {
  var pumps = [];
  for (var i = 0; i < BILGE_PUMPS.length; i++) pumps.push({ on: false, broken: false, run: false });
  return { pumps: pumps, scoopTick: {} };           // scoopTick: player id -> tick of their last scoop
}

// A press at the pump's switch: toggles run/stop. A broken pump cannot be started.
function bilgeToggle(b, i) {
  var p = b.pumps[i];
  if (!p.on && p.broken) return false;
  p.on = !p.on;
  return true;
}

// Breakdown / repair (nothing triggers a breakdown yet: E3-09; the spare part is E5/E9). A repaired pump stays stopped.
function bilgeBreak(b, i) { b.pumps[i].broken = true; b.pumps[i].on = false; b.pumps[i].run = false; return true; }
function bilgeRepair(b, i) { b.pumps[i].broken = false; return true; }

// One 10 Hz step: every running pump takes up to BILGE_CAPACITY * DT out of its compartment.
function bilgeStep(state) {
  var powered = state.reactor.E > BILGE_MIN_E;
  for (var i = 0; i < BILGE_PUMPS.length; i++) {
    var p = state.bilge.pumps[i];
    p.run = false;
    if (!p.on || p.broken || !powered) continue;
    p.run = waterRemove(state.water, BILGE_PUMPS[i].comp, BILGE_CAPACITY * DT) > 0;
  }
}

// The interaction key pressed by a player carrying the bucket: scoop water out of the compartment they stand in. Returns true when the press
// was used by the bucket (a scoop, or a scoop refused by the cooldown); false lets the press go to whatever else is in reach.
function tryScoop(state, id, pl, tick) {
  var held = heldBy(state.cargo, id);
  if (!held || held.kind !== "bucket" || held.carriers.indexOf(id) < 0) return false;
  var comp = compartmentAt(pl.z);
  if (state.water.comps[comp].w <= 0) return false;
  var last = state.bilge.scoopTick[id];
  if (last !== undefined && tick - last < BUCKET_COOLDOWN_TICKS) return true;
  waterRemove(state.water, comp, BUCKET_VOLUME);
  state.bilge.scoopTick[id] = tick;
  return true;
}

// Broadcast (and resync): s = 0 stopped, 1 on, 2 broken; r = 1 when the pump is actually moving water this tick.
function bilgeView(b) {
  var out = [];
  for (var i = 0; i < b.pumps.length; i++) out.push({ s: b.pumps[i].broken ? 2 : (b.pumps[i].on ? 1 : 0), r: b.pumps[i].run ? 1 : 0 });
  return out;
}
