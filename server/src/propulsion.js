// ---- Propulsion and the machine telegraph (E3-08) ------------------------------------------------------------------------------
// GDD 3.3: turbine -> electricity + propulsion. Decision (E3-08): propulsion is a CONSUMER of the electrical network (power.js), not a share of
// the steam, so the reactor's balance (E3-02) is untouched: the telegraph position adds its demand to the grid's, which lowers the bus voltage
// unless the plant makes enough surplus. "The faster we go, the darker it gets", and full speed needs full power. The boat's speed follows the
// telegraph set-point scaled by the bus voltage, with inertia: after a SCRAM the boat coasts and slows down. Position (distance) is accumulated
// for the future map (E9). The total noise of the boat (E8-01) will add PROPULSION noise to the reactor's; here it is only a derived value.
//
// Telegraph: 5 positions of a brass dial. Spike interaction: two floor tiles, "up" and "down", one notch per press (the aimed handle is E2-02).
var PROP_NAMES = ["arriere", "stop", "lent", "demi", "toute"];
var PROP_DEMAND = [1.0, 0, 1.0, 2.5, 5.0];                 // MWe added to the grid demand
var PROP_SPEED = [-0.3, 0, 0.3, 0.6, 1.0];                 // set-point as a fraction of PROP_VMAX
var PROP_VMAX = 6;                                         // m/s (about 12 knots)
var PROP_TAU = 15;                                         // s: inertia of the boat's speed
var PROP_STOP = 1;                                         // index of "stop", the starting position

function newPropulsion() { return { pos: PROP_STOP, speed: 0, dist: 0 }; }

// One notch up (+1) or down (-1) on the telegraph, stopping at the ends. Returns whether the handle moved.
function telegraphStep(prop, dir) {
  var next = Math.max(0, Math.min(PROP_NAMES.length - 1, prop.pos + (dir > 0 ? 1 : -1)));
  if (next === prop.pos) return false;
  prop.pos = next;
  return true;
}

function propulsionDemand(prop) { return PROP_DEMAND[prop.pos]; }

// One 10 Hz step, after the electrical network: the speed eases towards set-point x bus voltage.
function propulsionStep(state) {
  var p = state.prop, V = state.power.V;
  var target = PROP_SPEED[p.pos] * PROP_VMAX * V;
  p.speed += (target - p.speed) * DT / PROP_TAU;
  p.dist += p.speed * DT;
}

// Propulsion noise 0..4, from the speed (the telegraph alone makes none: a stopped boat is quiet).
function propulsionNoise(prop) { return 4 * Math.min(1, Math.abs(prop.speed) / PROP_VMAX); }

// Broadcast (and resync): p = telegraph position 0..4, s = speed m/s, d = distance travelled m, n = noise 0..4.
function propulsionView(prop) {
  function q(x, k) { return Math.round(x * k) / k; }
  return { p: prop.pos, s: q(prop.speed, 100), d: q(prop.dist, 10), n: q(propulsionNoise(prop), 100) };
}
