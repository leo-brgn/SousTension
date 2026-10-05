// ---- Boat buoyancy (E3-04) ----------------------------------------------------------------------------------
// After a SCRAM the boat has no electricity: ballast pumps and trim stop and it starts to sink, slowly (GDD 3.3: "sous-marin qui
// commence à couler doucement"). The descent speed ramps up to SINK_RATE_MAX over SINK_RAMP_SECONDS and eases back to zero when the
// reactor is restarted (E3-05). The boat does not rise by itself and nothing here destroys it: crush depth, flooding and damage are
// other epics (E6/E7). Deterministic: a function of the tick count and the reactor's SCRAM latch only.
var SINK_RATE_MAX = 0.2;       // m/s at full descent
var SINK_RAMP_SECONDS = 20;    // time to go from 0 to SINK_RATE_MAX (and back)

function newBoat() {
  return { depth: 0, vz: 0 };   // depth below the patrol depth (m, grows when sinking); vz = descent speed (m/s)
}

function boatStep(boat, reactor) {
  var dv = SINK_RATE_MAX / (SINK_RAMP_SECONDS * TICK_RATE);
  if (reactor.scram) boat.vz = Math.min(SINK_RATE_MAX, boat.vz + dv);
  else boat.vz = Math.max(0, boat.vz - dv);
  boat.depth += boat.vz * DT;
}

function boatView(boat) {
  return { d: Math.round(boat.depth * 100) / 100, vz: Math.round(boat.vz * 1000) / 1000 };
}
