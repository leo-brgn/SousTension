// ---- Hull leaks and their repair (E6-02) -------------------------------------------------------------------------------------
// A leak is a hole in the hull (left or right wall of a compartment) that pours water into that compartment at the flow rate of its size
// (waterAdd every tick, so the boat lists towards the leak's side) until it is repaired. PROTO: ONE leak at a time, scheduled by a seeded
// event (first at ~90 s, the next one 60-120 s after the previous is sealed); other systems (E3-09 failure generator, E7 damage) create
// leaks through leakCreate. Repair v0: carry a "hull patch" (a light cargo item, taken from the toolbox of compartment 2) to the leak and press
// the interaction key; each patch lowers the leak one size (large -> medium -> small -> sealed) and is used up. The hammer / wrench / wedge
// of the full repair kit wait for the one-hand inventory (E2-03/E2-04): the leak "type" is already there to hook them up.
var LEAK_RATES = [0, 0.02, 0.08, 0.25];      // m3/s per size: 0 sealed, 1 small, 2 medium, 3 large
var LEAK_REACH = 1.5;                        // m: a patch must be applied within this distance of the hole
var LEAK_SEED = 5678;
var LEAK_FIRST_TICK = 900;                   // first leak at 90 s
var LEAK_GAP_MIN_TICKS = 600, LEAK_GAP_SPAN_TICKS = 600;   // next one 60-120 s after the previous is sealed
var PATCH_RESPAWN_TICKS = 300;               // a used patch is back in the toolbox after 30 s

function newLeaks() { return { list: [], nextId: 1, rng: LEAK_SEED | 0, nextTick: LEAK_FIRST_TICK }; }

// Seeded PRNG (mulberry32, same family as the reactor's): the state is one uint32 in the leaks object.
function leakRand(L) {
  L.rng = (L.rng + 0x6D2B79F5) | 0;
  var t = Math.imul(L.rng ^ (L.rng >>> 15), 1 | L.rng);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

// Open a leak on the hull wall of a compartment. side -1 = left wall, +1 = right wall, z = position along the boat. size 1..3.
function leakCreate(L, z, side, size) {
  var s = side < 0 ? -1 : 1, sz = Math.max(1, Math.min(3, size | 0));
  var leak = { id: L.nextId++, comp: compartmentAt(z), x: s * HALF_X, z: z, side: s, size: sz, type: sz === 1 ? "rivet" : "plate" };
  L.list.push(leak);
  return leak;
}

function leakRate(leak) { return LEAK_RATES[leak.size]; }

// Nearest leak within reach of a player, or null.
function nearestLeak(L, pl) {
  var best = null, bestD = LEAK_REACH;
  for (var i = 0; i < L.list.length; i++) {
    var dx = pl.x - L.list[i].x, dz = pl.z - L.list[i].z, d = Math.sqrt(dx * dx + dz * dz);
    if (d <= bestD) { best = L.list[i]; bestD = d; }
  }
  return best;
}

// The interaction key was pressed: a player carrying a patch next to a leak applies it. Returns true when the press was a repair.
// `aimed` (E2-02): the leak the player is looking at; it must exist and be within reach. Without it the nearest leak in reach is used.
function tryRepair(state, id, pl, tick, aimed) {
  var held = handItem(state, id, "patch");
  if (!held) return false;
  var leak = aimed || nearestLeak(state.leaks, pl);
  if (!leak) return false;
  if (aimed) {
    var dx = pl.x - aimed.x, dz = pl.z - aimed.z;
    if (Math.sqrt(dx * dx + dz * dz) > LEAK_REACH) return false;
  }
  leak.size--;
  held.active = false; held.carriers = []; held.vx = 0; held.vz = 0; held.respawnTick = tick + PATCH_RESPAWN_TICKS;
  if (leak.size <= 0) {
    state.leaks.list.splice(state.leaks.list.indexOf(leak), 1);
    state.leaks.nextTick = tick + LEAK_GAP_MIN_TICKS + Math.floor(leakRand(state.leaks) * LEAK_GAP_SPAN_TICKS);
  }
  return true;
}

// One 10 Hz step: the scheduled event may open a leak (only when none is active), every open leak pours water.
function leakStep(state, tick) {
  var L = state.leaks;
  if (L.list.length === 0 && tick >= L.nextTick) {
    var c = Math.min(WATER_COMPARTMENTS.length - 1, Math.floor(leakRand(L) * WATER_COMPARTMENTS.length));
    var comp = WATER_COMPARTMENTS[c];
    var z = comp.z0 + 0.5 + leakRand(L) * (comp.z1 - comp.z0 - 1);
    var side = leakRand(L) < 0.5 ? -1 : 1;
    var r = leakRand(L), size = r < 0.5 ? 1 : (r < 0.85 ? 2 : 3);
    leakCreate(L, z, side, size);
  }
  for (var i = 0; i < L.list.length; i++) waterAdd(state.water, L.list[i].comp, leakRate(L.list[i]) * DT, L.list[i].side);
}

// Broadcast (and resync): every open leak.
function leaksView(L) {
  var out = [];
  for (var i = 0; i < L.list.length; i++) {
    var k = L.list[i];
    out.push({ id: k.id, c: k.comp, x: k.x, z: Math.round(k.z * 100) / 100, s: k.size, t: k.type });
  }
  return out;
}
