// ---- Water by volume per compartment and the boat's trim / list (E6-01) -----------------------------------------------------
// GDD 8: water is simulated as a VOLUME per compartment plus a centre of mass (no fluid). Six compartments in a line from the bow (+z)
// to the stern (-z) over the 20 m of the spike. Water never appears or disappears except through the explicit API (waterAdd for
// leaks, waterRemove for bilge pumps: E6-02 / E6-03); it flows between neighbouring compartments through OPEN bulkhead openings in
// proportion to the level difference. Its weight shifts the centre of mass, which adds a trim (pitch) and a list (roll) to the scripted
// swell of the boat (BoatMotion). Deterministic: pure arithmetic on the tick, no randomness.
var WATER_COMPARTMENTS = [
  { id: "torpilles", z0: 20 / 3, z1: 10, volume: 40 },            // 1 bow
  { id: "central", z0: 10 / 3, z1: 20 / 3, volume: 40 },          // 2
  { id: "radio", z0: 0, z1: 10 / 3, volume: 40 },                 // 3
  { id: "reacteur", z0: -10 / 3, z1: 0, volume: 40 },             // 4
  { id: "machines", z0: -20 / 3, z1: -10 / 3, volume: 40 },       // 5
  { id: "vie", z0: -10, z1: -20 / 3, volume: 40 }                 // 6 stern
];
var WATER_HEIGHT = 2.0;            // m: a full compartment is 2 m deep, so level = volume / (capacity / 2)
var WATER_FLOW = 2.0;              // m3/s per metre of level difference through an open bulkhead opening (time constant ~5 s)
var WATER_DENSITY = 1000;          // kg/m3
var TRIM_PER_TM = 0.036;           // degrees of pitch per tonne-metre of longitudinal moment (40 m3 at the bow ~ 12 deg)
var LIST_PER_TM = 0.2;             // degrees of roll per tonne-metre of lateral moment
var MAX_TRIM_DEG = 12, MAX_LIST_DEG = 15;
var HALF_BEAM = 1.5;               // m: lateral position of water that stands against a side

function newWater() {
  var comps = [], doors = [];
  for (var i = 0; i < WATER_COMPARTMENTS.length; i++) comps.push({ w: 0, side: 0 });   // w = m3, side = -1 (left) .. +1 (right) bias of the water
  for (var d = 0; d < WATER_COMPARTMENTS.length - 1; d++) doors.push({ open: true });      // door d joins compartment d and d+1
  return { comps: comps, doors: doors, rejected: 0 };
}

function waterLevel(w, i) { return w.comps[i].w / (WATER_COMPARTMENTS[i].volume / WATER_HEIGHT); }   // m

// Which compartment contains boat-local z (clamped to the ends).
function compartmentAt(z) {
  for (var i = 0; i < WATER_COMPARTMENTS.length; i++) if (z >= WATER_COMPARTMENTS[i].z0 && z < WATER_COMPARTMENTS[i].z1) return i;
  return z < 0 ? WATER_COMPARTMENTS.length - 1 : 0;
}

// Pour `volume` m3 into compartment i (a leak). `side` (-1 left, 0 centre, +1 right) is where it stands. Returns the volume accepted:
// what does not fit in a full compartment is counted in water.rejected (the boat is flooded there).
function waterAdd(w, i, volume, side) {
  var c = w.comps[i], cap = WATER_COMPARTMENTS[i].volume;
  var ok = Math.max(0, Math.min(volume, cap - c.w));
  var tot = c.w + ok;
  if (tot > 0) c.side = (c.side * c.w + (side || 0) * ok) / tot;
  c.w = tot;
  w.rejected += volume - ok;
  return ok;
}

// Take up to `volume` m3 out of compartment i (a bilge pump). Returns the volume removed.
function waterRemove(w, i, volume) {
  var c = w.comps[i];
  var ok = Math.max(0, Math.min(volume, c.w));
  c.w -= ok;
  if (c.w < 1e-12) { c.w = 0; c.side = 0; }
  return ok;
}

function waterSetDoor(w, d, open) {
  if (d < 0 || d >= w.doors.length) return false;
  w.doors[d].open = !!open;
  return true;
}

function waterTotal(w) {
  var t = 0;
  for (var i = 0; i < w.comps.length; i++) t += w.comps[i].w;
  return t;
}

// One 10 Hz step: flow through open openings (computed from the levels at the start of the step, applied in order, never more than the
// source holds nor than the destination can take), so the total is conserved to the last bit of rounding.
function waterStep(w) {
  var n = w.doors.length, q = new Array(n);
  for (var d = 0; d < n; d++) q[d] = w.doors[d].open ? WATER_FLOW * (waterLevel(w, d) - waterLevel(w, d + 1)) * DT : 0;
  for (var k = 0; k < n; k++) {
    var from = q[k] >= 0 ? k : k + 1, to = q[k] >= 0 ? k + 1 : k, vol = Math.abs(q[k]);
    vol = Math.min(vol, w.comps[from].w, WATER_COMPARTMENTS[to].volume - w.comps[to].w);
    if (vol <= 0) continue;
    // moved water arrives centred: it dilutes the lateral bias of the destination
    var dest = w.comps[to], tot = dest.w + vol;
    dest.side = (dest.side * dest.w) / tot;
    dest.w = tot;
    w.comps[from].w -= vol;
    if (w.comps[from].w < 1e-12) { w.comps[from].w = 0; w.comps[from].side = 0; }
  }
}

function clampDeg(v, lim) { return v < -lim ? -lim : (v > lim ? lim : v); }

// Centre-of-mass effect: trim (pitch, + = bow down) from the longitudinal moment, list (roll) from the lateral one. Degrees.
function waterTilt(w) {
  var mz = 0, mx = 0;                                              // tonne-metres
  for (var i = 0; i < w.comps.length; i++) {
    var c = w.comps[i], mass = c.w * WATER_DENSITY / 1000;         // tonnes
    var zc = (WATER_COMPARTMENTS[i].z0 + WATER_COMPARTMENTS[i].z1) / 2;
    var fill = c.w / WATER_COMPARTMENTS[i].volume;
    mz += mass * zc;
    mx += mass * c.side * HALF_BEAM * (1 - fill);                  // standing against a side, it spreads to the middle as the compartment fills
  }
  return { trim: clampDeg(TRIM_PER_TM * mz, MAX_TRIM_DEG) || 0, list: clampDeg(-LIST_PER_TM * mx, MAX_LIST_DEG) || 0 };   // || 0: never -0
}

// Broadcast (and resync): l = fill fraction 0..1 per compartment, m = total mass in tonnes, tr / li = trim and list offsets (deg),
// dr = bulkhead openings (1 open), ov = volume rejected by full compartments.
function waterView(w) {
  function q(x, k) { return Math.round(x * k) / k; }
  var l = [], dr = [], t = waterTilt(w);
  for (var i = 0; i < w.comps.length; i++) l.push(q(w.comps[i].w / WATER_COMPARTMENTS[i].volume, 1000));
  for (var d = 0; d < w.doors.length; d++) dr.push(w.doors[d].open ? 1 : 0);
  return { l: l, m: q(waterTotal(w) * WATER_DENSITY / 1000, 10), tr: q(t.trim, 100), li: q(t.list, 100), dr: dr, ov: q(w.rejected, 100) };
}
