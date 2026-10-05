// Nakama authoritative match for the moving-frame spike (E1-01).
// Plain JavaScript (Nakama JS runtime, ES5 style) — no build step.
//
// Model: the boat moves deterministically as a function of time (see Assets/_Project/Scripts/Sim/BoatMotion.cs),
// so the server only simulates character positions in BOAT-LOCAL space (x = right, z = forward).
// Clients send one input per fixed input-tick (10 Hz); the server applies them in order and
// broadcasts the authoritative state each tick together with the last processed input sequence.

var MATCH_NAME = "moving_frame";
var TICK_RATE = 10;
var DT = 1 / TICK_RATE;
var OP_INPUT = 1;      // client -> server : {seq, mx, mz}
var OP_STATE = 2;      // server -> clients: {tick, t, players:[{id, x, z, seq}]}
var MOVE_SPEED = 3.0;  // m/s
var HALF_X = 3.0;      // boat interior half width (m)
var HALF_Z = 10.0;     // boat interior half length (m)
var MAX_QUEUED_INPUTS = 6;
var STATIONS = [{ x: 0, z: -9 }, { x: 0, z: 9 }]; // two-player interlock keys (boat-local), one at each end
var STATION_REACH = 2.0;       // m
var INTERLOCK_WINDOW_TICKS = 30; // 3 s at 10 Hz (GDD: Rule of Two Players)
var MAX_ALLOWANCE = 4; // max inputs a player may apply in one tick to catch up after a network stall
var MAX_PLAYERS = 4;
var REACTOR_SEED = 1234; // TODO(E3-05/run start): derive from the run so every patrol drifts differently

function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }

// One fixed-step integration, shared by server and (identically re-implemented) by the client prediction.
// `factor` (E2-04, default 1) scales the walking speed: what a player carries slows them down (hands.js carrySpeedFactor, mirrored in
// CharacterMotion.cs for the client's prediction).
function stepPlayer(p, mx, mz, factor) {
  var len = Math.sqrt(mx * mx + mz * mz);
  if (len > 1) { mx /= len; mz /= len; }
  var f = factor === undefined ? 1 : factor;
  p.x = clamp(p.x + mx * MOVE_SPEED * f * DT, -HALF_X, HALF_X);
  p.z = clamp(p.z + mz * MOVE_SPEED * f * DT, -HALF_Z, HALF_Z);
}

