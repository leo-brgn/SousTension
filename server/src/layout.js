// ---- Boat layout: bulkheads, hatches, privacy baffles (E10-01) ------------------------------------------------------------------------
// The geometry of the 3D boat (blender/kit_environment.py -> Assets/_Project/Art/Models/Environment/boat_layout.json), in BOAT-LOCAL space
// (x = right, z = forward, bow = +z): 28 m x 6 m, six compartments 4/6/4/4/4/6 m. Five bulkheads separate them; each has ONE hatch 0.9 m wide,
// staggered left / right, and a privacy baffle 0.95 m behind it (towards the stern) that hides the next post. A hatch is the bulkhead opening that
// lets WATER through (water.doors[d].open); when it is closed it blocks the water and the players.
// Collisions are circle (radius PLAYER_RADIUS) against axis-aligned rectangles. Mirrored in Assets/_Project/Scripts/Sim/BoatLayout.cs.
var PLAYER_RADIUS = 0.25;
var BULKHEAD_Z = [10, 4, 0, -4, -8];          // door d joins compartment d and d+1 (0 = torpedo room, 5 = living quarters)
var HATCH_X = [-1.45, 1.45, -1.45, 1.45, -1.45];
var HATCH_HALF = 0.45;                         // half width of the opening
var WALL_HALF = 0.07;                          // half thickness of a bulkhead
var BAFFLE_HALF_X = 0.8, BAFFLE_HALF_Z = 0.06, BAFFLE_OFFSET = 0.95;

// Static obstacles [x0, x1, z0, z1]: the bulkhead walls on each side of the hatch and the baffles.
var LAYOUT_OBSTACLES = (function () {
  var out = [];
  for (var d = 0; d < BULKHEAD_Z.length; d++) {
    var z = BULKHEAD_Z[d], cx = HATCH_X[d];
    out.push([-HALF_X, cx - HATCH_HALF, z - WALL_HALF, z + WALL_HALF]);
    out.push([cx + HATCH_HALF, HALF_X, z - WALL_HALF, z + WALL_HALF]);
    out.push([cx - BAFFLE_HALF_X, cx + BAFFLE_HALF_X, z - BAFFLE_OFFSET - BAFFLE_HALF_Z, z - BAFFLE_OFFSET + BAFFLE_HALF_Z]);
  }
  return out;
})();

function circleHitsRect(x, z, r) {
  var cx = x < r[0] ? r[0] : (x > r[1] ? r[1] : x), cz = z < r[2] ? r[2] : (z > r[3] ? r[3] : z);
  var dx = x - cx, dz = z - cz;
  return dx * dx + dz * dz < PLAYER_RADIUS * PLAYER_RADIUS;
}

// Is a player's circle at (x, z) inside a wall? `doors` = water.doors: a closed hatch is a wall too.
function layoutBlocked(x, z, doors) {
  for (var i = 0; i < LAYOUT_OBSTACLES.length; i++) if (circleHitsRect(x, z, LAYOUT_OBSTACLES[i])) return true;
  if (doors) {
    for (var d = 0; d < BULKHEAD_Z.length && d < doors.length; d++) {
      if (doors[d].open) continue;
      if (circleHitsRect(x, z, [HATCH_X[d] - HATCH_HALF, HATCH_X[d] + HATCH_HALF, BULKHEAD_Z[d] - WALL_HALF, BULKHEAD_Z[d] + WALL_HALF])) return true;
    }
  }
  return false;
}

// Is somebody standing in (or touching) the opening of hatch d? Closing it on them is refused.
function hatchOccupied(state, d) {
  var cx = HATCH_X[d], z = BULKHEAD_Z[d];
  var r = [cx - HATCH_HALF, cx + HATCH_HALF, z - WALL_HALF, z + WALL_HALF];
  for (var k = 0; k < state.order.length; k++) {
    var pl = state.players[state.order[k]];
    if (circleHitsRect(pl.x, pl.z, r)) return true;
  }
  return false;
}

// One press on the wheel of hatch d: open <-> closed. Closing is refused while a player is in the opening. Returns true when it changed.
function hatchToggle(state, d) {
  if (d < 0 || d >= state.water.doors.length) return false;
  if (state.water.doors[d].open) {
    if (hatchOccupied(state, d)) return false;
    return waterSetDoor(state.water, d, false);
  }
  return waterSetDoor(state.water, d, true);
}

// Loose cargo (E2-01): a point of radius CARGO_RADIUS against the same walls, baffles and closed hatches as the players.
var CARGO_RADIUS = 0.15;
function cargoBlocked(x, z, doors) {
  var r = CARGO_RADIUS;
  for (var i = 0; i < LAYOUT_OBSTACLES.length; i++) {
    var o = LAYOUT_OBSTACLES[i];
    if (x > o[0] - r && x < o[1] + r && z > o[2] - r && z < o[3] + r) return true;
  }
  if (doors) {
    for (var d = 0; d < BULKHEAD_Z.length && d < doors.length; d++) {
      if (doors[d].open) continue;
      if (x > HATCH_X[d] - HATCH_HALF - r && x < HATCH_X[d] + HATCH_HALF + r && z > BULKHEAD_Z[d] - WALL_HALF - r && z < BULKHEAD_Z[d] + WALL_HALF + r) return true;
    }
  }
  return false;
}
