// ---- Carried & sliding cargo (boat-local space, no physics engine) -----------------------------------------
// Loose cargo slides on the tilted floor: the boat tilt is a deterministic function of server time (same formulas
// as BoatMotion.cs), so gravity can be projected into boat-local axes. Carried cargo follows its carrier(s).
// Light crates need one carrier; the heavy fuel flask needs two players who both grab within the 3 s window.
var GRAB_REACH = 1.5;       // m
var GRAVITY = 9.81;         // m/s^2
var FRICTION = 0.25;        // Coulomb coefficient: loose cargo starts sliding past ~14 deg of tilt
var CARGO_DEFS = [
  { id: "crate1", heavy: false, x: 2.0, z: -3.0 },
  { id: "crate2", heavy: false, x: -2.0, z: 4.0 },
  { id: "fuel", kind: "fuel", heavy: true, x: -2.0, z: -5.0 },
  // Hull patches (E6-02): the toolbox of compartment 2. Light, used up on a leak, back in the toolbox 30 s later.
  { id: "patch1", kind: "patch", heavy: false, x: 0.6, z: 5.5 },
  { id: "patch2", kind: "patch", heavy: false, x: 1.0, z: 5.5 },
  { id: "patch3", kind: "patch", heavy: false, x: 1.4, z: 5.5 },
  // Bucket (E6-03): same toolbox, never used up.
  { id: "bucket", kind: "bucket", heavy: false, x: 1.8, z: 5.5 },
  // Torch (E2-03): the restart "à la lampe torche" (GDD 3.3); one hand, or the chest pocket.
  { id: "flashlight", kind: "flashlight", heavy: false, x: 0.2, z: 5.5 },
  // The OK-114 Operating Manual (E5-02): one copy, on the table of the central post.
  { id: "manual", kind: "manual", heavy: false, x: -2.0, z: 6.0 }
];
// Boat tilt (must match BoatMotion.cs defaults: pitch 15 deg / 7 s, roll 20 deg / 5 s + 1 rad phase).
var PITCH_AMP = 15 * Math.PI / 180, PITCH_PERIOD = 7;
var ROLL_AMP = 20 * Math.PI / 180, ROLL_PERIOD = 5;

// Horizontal part of the world "up" vector expressed in boat-local axes: up_local = (cos p sin r, cos p cos r, -sin p).
// trimDeg / listDeg (E6-01) are the offsets the water's weight adds to the scripted swell (same as BoatMotion.Evaluate with offsets).
function boatUpHorizontal(t, trimDeg, listDeg) {
  var tau = 2 * Math.PI;
  var p = PITCH_AMP * Math.sin(tau * t / PITCH_PERIOD) + (trimDeg || 0) * Math.PI / 180;
  var r = ROLL_AMP * Math.sin(tau * t / ROLL_PERIOD + 1.0) + (listDeg || 0) * Math.PI / 180;
  return { x: Math.cos(p) * Math.sin(r), z: -Math.sin(p) };
}

function newCargo() {
  var out = [];
  for (var i = 0; i < CARGO_DEFS.length; i++) {
    var d = CARGO_DEFS[i];
    out.push({ id: d.id, kind: d.kind || "crate", heavy: d.heavy, x: d.x, z: d.z, vx: 0, vz: 0, carriers: [], pend: "", pendTick: 0,
               active: true, respawnTick: 0, homeX: d.x, homeZ: d.z, y: 0, vy: 0, fly: false });
  }
  return out;
}

// A thrown item (E2-04): ballistic in the boat's frame (gravity only, no boat inertia), bounces on the walls, lands on the floor and then
// slides like any loose cargo. Deterministic: plain arithmetic on the tick.
function flyStep(c) {
  c.x += c.vx * DT; c.z += c.vz * DT;
  c.vy -= GRAVITY * DT;
  c.y += c.vy * DT;
  if (c.x < -HALF_X || c.x > HALF_X) { c.x = clamp(c.x, -HALF_X, HALF_X); c.vx = -c.vx * THROW_BOUNCE; }
  if (c.z < -HALF_Z || c.z > HALF_Z) { c.z = clamp(c.z, -HALF_Z, HALF_Z); c.vz = -c.vz * THROW_BOUNCE; }
  if (c.y <= 0) { c.y = 0; c.vy = 0; c.fly = false; c.vx *= THROW_LAND_KEEP; c.vz *= THROW_LAND_KEEP; }
}

function slideStep(c, up) {
  var ax = -GRAVITY * up.x, az = -GRAVITY * up.z, amag = Math.sqrt(ax * ax + az * az);
  var vmag = Math.sqrt(c.vx * c.vx + c.vz * c.vz);
  var dirx, dirz;
  if (vmag < 1e-6) {
    if (amag <= FRICTION * GRAVITY) { c.vx = 0; c.vz = 0; return; } // static friction holds it
    dirx = ax / amag; dirz = az / amag;
  } else { dirx = c.vx / vmag; dirz = c.vz / vmag; }
  var nvx = c.vx + (ax - FRICTION * GRAVITY * dirx) * DT;
  var nvz = c.vz + (az - FRICTION * GRAVITY * dirz) * DT;
  if (vmag >= 1e-6 && (nvx * c.vx + nvz * c.vz) <= 0) { nvx = 0; nvz = 0; } // friction stopped it
  c.vx = nvx; c.vz = nvz;
  c.x += c.vx * DT; c.z += c.vz * DT;
  if (c.x < -HALF_X || c.x > HALF_X) { c.x = clamp(c.x, -HALF_X, HALF_X); c.vx = 0; }
  if (c.z < -HALF_Z || c.z > HALF_Z) { c.z = clamp(c.z, -HALF_Z, HALF_Z); c.vz = 0; }
}

function updateCargo(state, tick) {
  var tilt = waterTilt(state.water);
  var up = boatUpHorizontal(tick * DT, tilt.trim, tilt.list);
  for (var i = 0; i < state.cargo.length; i++) {
    var c = state.cargo[i];
    if (!c.active) {                                               // used patch: back in the toolbox after PATCH_RESPAWN_TICKS
      if (tick >= c.respawnTick) { c.active = true; c.x = c.homeX; c.z = c.homeZ; c.vx = 0; c.vz = 0; }
      continue;
    }
    if (c.fly) { flyStep(c); continue; }                          // thrown: in the air until it lands
    if (c.pend !== "" && tick - c.pendTick >= INTERLOCK_WINDOW_TICKS) c.pend = ""; // second carrier too late
    // A carrier that left the match releases the cargo
    var alive = [];
    for (var k = 0; k < c.carriers.length; k++) if (state.players[c.carriers[k]]) alive.push(c.carriers[k]);
    if (alive.length !== c.carriers.length) { c.carriers = []; c.vx = 0; c.vz = 0; }
    if (c.pend !== "" && !state.players[c.pend]) c.pend = "";
    if (c.carriers.length === 1) {
      var p1 = state.players[c.carriers[0]]; c.x = p1.x; c.z = p1.z; c.vx = 0; c.vz = 0;
    } else if (c.carriers.length === 2) {
      var pa = state.players[c.carriers[0]], pb = state.players[c.carriers[1]];
      c.x = (pa.x + pb.x) / 2; c.z = (pa.z + pb.z) / 2; c.vx = 0; c.vz = 0;
    } else {
      slideStep(c, up);
    }
  }
}

function cargoView(cargo) {
  var out = [];
  for (var i = 0; i < cargo.length; i++) {
    var c = cargo[i];
    out.push({ id: c.id, x: c.x, z: c.z, h: c.heavy ? 1 : 0, c: c.carriers, p: c.pend, k: c.kind, a: c.active ? 1 : 0, y: Math.round(c.y * 100) / 100 });
  }
  return out;
}

