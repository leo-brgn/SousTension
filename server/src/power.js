// ---- Electrical network: bus voltage, breakers, emergency battery (E3-07) ------------------------------------------------------
// The turbine makes E (MWe). The reactor's primary pumps are on the ESSENTIAL bus and take their share first (reactor.js, untouched). What is
// left, SURPLUS = max(0, E - essential demand), feeds the boat's consumers through 20 breakers. The bus VOLTAGE V (0..1) follows
// supply / demand slowly: it rises with TAU_UP, it declines slowly with TAU_DOWN ("l'air et la lumière déclinent lentement" in Veille, GDD 3.3),
// but a SCRAM cuts it at once ("tue instantanément toute l'électricité"). The lights show V as white -> orange -> red -> black, the
// signature of the game. A small BATTERY feeds an emergency bus (red emergency lights) while the grid is dark: it charges when V is high,
// lasts ~10 minutes. Breakers can be opened by players (shedding load), TRIP by themselves when the voltage stays low although power exists
// (the biggest closed consumer goes first), and are re-armed by hand at the panel. Effects: lights per compartment and the bilge pumps
// today; the other consumers only report whether they are powered (their gameplay comes with their epics: air E6-06, sonar/radio, ...).
var TAU_UP = 30;                 // s: voltage rise
var TAU_DOWN = 120;              // s: slow decline when supply falls short (a SCRAM is instantaneous)
var TRIP_V = 0.4;                // a breaker trips when V stays below this ...
var TRIP_TICKS = 50;             // ... for 5 s while the plant still produces surplus (nothing to shed otherwise)
var SETTLED = 0.02;               // the voltage counts as settled when the target is at most this far above it
var DARK_V = 0.2;                // below this the grid counts as dark: the emergency bus takes over
var BATTERY_DRAIN_S = 600;       // s of emergency light on a full battery
var BATTERY_CHARGE_S = 900;      // s to charge it from empty while the voltage is high
var BATTERY_CHARGE_V = 0.9;
var BILGE_MIN_V = 0.5;           // the bilge pumps need this voltage (and their breaker closed)

// 20 breakers, one consumer each. Spike layout: floor tiles 0.5 m apart (3 columns x 7 rows) at the stern, left wall, because the spike's
// interaction works on position, not on aim (E2-02 will replace this by a real panel: the positions are data).
var BREAKERS = [
  { id: "light1", demand: 0.15, use: "light", comp: 0 }, { id: "light2", demand: 0.15, use: "light", comp: 1 },
  { id: "light3", demand: 0.15, use: "light", comp: 2 }, { id: "light4", demand: 0.15, use: "light", comp: 3 },
  { id: "light5", demand: 0.15, use: "light", comp: 4 }, { id: "light6", demand: 0.15, use: "light", comp: 5 },
  { id: "bilge0", demand: 0.4, use: "bilge", pump: 0 }, { id: "bilge1", demand: 0.4, use: "bilge", pump: 1 },
  { id: "sonar", demand: 0.6 }, { id: "radio", demand: 0.3 }, { id: "cipher", demand: 0.15 }, { id: "ventilation", demand: 0.5 },
  { id: "galley", demand: 0.5 }, { id: "samovar", demand: 0.25 }, { id: "coffee", demand: 0.25 }, { id: "shower", demand: 0.3 },
  { id: "periscope", demand: 0.15 }, { id: "interphone", demand: 0.15 }, { id: "heater", demand: 0.4 }, { id: "pneumatic", demand: 0.1 }
];
(function placeBreakers() {
  for (var i = 0; i < BREAKERS.length; i++) {
    BREAKERS[i].x = -2.5 + 0.5 * (i % 3);
    BREAKERS[i].z = -9.75 + 0.5 * Math.floor(i / 3);
    BREAKERS[i].reach = 0.3;
  }
})();

function newPower() {
  var br = [];
  for (var i = 0; i < BREAKERS.length; i++) br.push({ closed: true, tripped: false });
  return { V: 1, B: 1, br: br, lowTicks: 0 };
}

// Power drawn by the closed breakers (MWe), the whole demand (breakers + propulsion, E3-08) and the surplus the plant offers them.
function powerDemand(state) { return gridDemand(state.power) + propulsionDemand(state.prop); }
function gridDemand(pw) {
  var d = 0;
  for (var i = 0; i < BREAKERS.length; i++) if (pw.br[i].closed) d += BREAKERS[i].demand;
  return d;
}
function gridSurplus(reactor) {
  var essential = ((reactor.pumps[0] ? 1 : 0) + (reactor.pumps[1] ? 1 : 0)) * REACTOR_K.pumpDemand;
  return Math.max(0, reactor.E - essential);
}

// A press at a breaker: closed -> open (shedding a load); open or tripped -> closed (re-arm). The next step decides if it holds.
function breakerToggle(pw, i) {
  var b = pw.br[i];
  if (b.closed) { b.closed = false; b.tripped = false; } else { b.closed = true; b.tripped = false; }
  return true;
}
function breakerTrip(pw, i) { pw.br[i].closed = false; pw.br[i].tripped = true; return true; }

// Is a consumer fed? (its breaker is closed and the bus voltage is at least minV)
function consumerPowered(pw, i, minV) { return pw.br[i].closed && pw.V >= minV; }

// One 10 Hz step, after the reactor.
function powerStep(state) {
  var pw = state.power, r = state.reactor;
  var demand = powerDemand(state), surplus = gridSurplus(r);
  var target = demand > 0 ? Math.min(1, surplus / demand) : 1;
  if (r.scram) {
    pw.V = 0;                                              // the SCRAM tears the whole grid down at once
  } else {
    pw.V += (target - pw.V) * DT / (target > pw.V ? TAU_UP : TAU_DOWN);
  }
  // emergency battery: carries the emergency bus while the grid is dark, charges while the voltage is high
  if (pw.V < DARK_V) pw.B = Math.max(0, pw.B - DT / BATTERY_DRAIN_S);
  else if (pw.V > BATTERY_CHARGE_V) pw.B = Math.min(1, pw.B + DT / BATTERY_CHARGE_S);
  // load shedding: the voltage is low and NOT recovering (it has settled within SETTLED of the target supply ratio, or is above it: a structural
  // overload, not the plant spinning up after a restart) although the plant produces surplus -> the biggest closed consumer trips
  if (!r.scram && surplus > 0 && pw.V < TRIP_V && target <= pw.V + SETTLED) {
    pw.lowTicks++;
    if (pw.lowTicks >= TRIP_TICKS) {
      var big = -1;
      for (var i = 0; i < BREAKERS.length; i++) if (pw.br[i].closed && (big < 0 || BREAKERS[i].demand > BREAKERS[big].demand)) big = i;
      if (big >= 0) breakerTrip(pw, big);
      pw.lowTicks = 0;
    }
  } else pw.lowTicks = 0;
}

// Light level of a compartment: the bus voltage when its lights breaker is closed. Band: 3 white, 2 orange, 1 red, 0 dark.
function lightLevel(pw, comp) {
  for (var i = 0; i < BREAKERS.length; i++) if (BREAKERS[i].use === "light" && BREAKERS[i].comp === comp) return pw.br[i].closed ? pw.V : 0;
  return 0;
}
function lightBand(level) { return level >= 0.8 ? 3 : (level >= 0.5 ? 2 : (level >= DARK_V ? 1 : 0)); }

// Broadcast (and resync): v = bus voltage, b = battery 0..1, em = emergency lights on (grid dark, battery left), br = per breaker 0 open / 1 closed /
// 2 tripped, lt = light band per compartment (3 white .. 0 dark), dm = demand, su = surplus (MWe).
function powerView(state) {
  var pw = state.power, br = [], lt = [];
  function q(x, k) { return Math.round(x * k) / k; }
  for (var i = 0; i < BREAKERS.length; i++) br.push(pw.br[i].tripped ? 2 : (pw.br[i].closed ? 1 : 0));
  for (var c = 0; c < WATER_COMPARTMENTS.length; c++) lt.push(lightBand(lightLevel(pw, c)));
  return { v: q(pw.V, 1000), b: q(pw.B, 1000), em: pw.V < DARK_V && pw.B > 0 ? 1 : 0, br: br, lt: lt, dm: q(powerDemand(state), 100), su: q(gridSurplus(state.reactor), 100) };
}
