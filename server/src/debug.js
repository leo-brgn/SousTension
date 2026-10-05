// ---- Debug tools (E1-09) --------------------------------------------------------------------------------------------------------
// A text command channel for developers and playtesters: force a leak, a SCRAM, a breakdown, teleport, tweak a simulation parameter, fast-forward
// time. DISABLED BY DEFAULT: the input field \`dbg\` is only obeyed when the match was created in debug mode (the Nakama runtime environment
// variable SOUSTENSION_DEBUG=1, set only in the local docker-compose, never in CI or production, or the match parameter debug: true). Otherwise
// the command is ignored and the sender is told so. The debug state is NOT game state: it is left out of the state digest, and with debug off
// nothing here touches the simulation. Replies go back to the sender only, as the server message OP_DEBUG: {lines: [...]}.
//
// Commands:  help | state | tp <x> <z> | regime <veille|croisiere|pleine> | scram | restart | leak <1-6> <1-3> [g|d] | unleak
//            break <pump|bilge> <1-2> | repair <pump|bilge> <1-2> | trip <breaker id> | water <1-6> <m3> | bring <item id>
//            get <param> | set <param> <value|default> | ff <seconds>
var DEBUG_FF_MAX_SECONDS = 300;

function debugEnabled(ctx, params) {
  return !!((params && params.debug === true) || (ctx && ctx.env && ctx.env.SOUSTENSION_DEBUG === "1"));
}
function newDebug(enabled) { return { enabled: !!enabled, skew: 0, pending: 0, out: [] }; }

// Tweakable simulation parameters: a whitelist with bounds (the reactor constants may vary between a tenth and ten times their default).
var DEBUG_PARAMS = (function () {
  var p = {};
  function scalar(name, get, set, min, max) { p[name] = { get: get, set: set, min: min, max: max, def: get() }; }
  scalar("water_flow", function () { return WATER_FLOW; }, function (v) { WATER_FLOW = v; }, 0, 20);
  scalar("tau_up", function () { return TAU_UP; }, function (v) { TAU_UP = v; }, 1, 1000);
  scalar("tau_down", function () { return TAU_DOWN; }, function (v) { TAU_DOWN = v; }, 1, 1000);
  scalar("trip_v", function () { return TRIP_V; }, function (v) { TRIP_V = v; }, 0, 1);
  scalar("battery_drain_s", function () { return BATTERY_DRAIN_S; }, function (v) { BATTERY_DRAIN_S = v; }, 10, 86400);
  scalar("bilge_capacity", function () { return BILGE_CAPACITY; }, function (v) { BILGE_CAPACITY = v; }, 0, 2);
  scalar("valve_turn_rate", function () { return VALVE_TURN_RATE; }, function (v) { VALVE_TURN_RATE = v; }, 0.01, 5);
  scalar("prop_vmax", function () { return PROP_VMAX; }, function (v) { PROP_VMAX = v; }, 0, 30);
  scalar("prop_tau", function () { return PROP_TAU; }, function (v) { PROP_TAU = v; }, 1, 120);
  scalar("sink_rate_max", function () { return SINK_RATE_MAX; }, function (v) { SINK_RATE_MAX = v; }, 0, 5);
  scalar("leak_small", function () { return LEAK_RATES[1]; }, function (v) { LEAK_RATES[1] = v; }, 0, 2);
  scalar("leak_medium", function () { return LEAK_RATES[2]; }, function (v) { LEAK_RATES[2] = v; }, 0, 2);
  scalar("leak_large", function () { return LEAK_RATES[3]; }, function (v) { LEAK_RATES[3] = v; }, 0, 2);
  for (var key in REACTOR_K) {
    if (typeof REACTOR_K[key] !== "number") continue;
    (function (k) {
      var d = REACTOR_K[k];
      scalar("reactor." + k, function () { return REACTOR_K[k]; }, function (v) { REACTOR_K[k] = v; }, d > 0 ? d * 0.1 : 0, d > 0 ? d * 10 : 1);
    })(key);
  }
  return p;
})();

// A parameter of the whitelist, by its own name only: never something inherited from Object ("constructor", "__proto__"...).
function dbgParam(name) { return Object.prototype.hasOwnProperty.call(DEBUG_PARAMS, name) ? DEBUG_PARAMS[name] : null; }

function dbgReply(state, id, text) { state.debug.out.push({ to: id, text: text }); }
function dbgNum(tok) { var v = Number(tok); return tok !== undefined && tok !== "" && isFinite(v) ? v : NaN; }
function dbgCompartment(tok) { var c = dbgNum(tok); return c >= 1 && c <= WATER_COMPARTMENTS.length && Math.floor(c) === c ? c - 1 : -1; }

function debugState(state) {
  var r = state.reactor, w = state.water, pw = state.power;
  var fills = [];
  for (var i = 0; i < w.comps.length; i++) fills.push(Math.round(w.comps[i].w * 10) / 10);
  var tripped = [];
  for (var b = 0; b < BREAKERS.length; b++) if (pw.br[b].tripped) tripped.push(BREAKERS[b].id);
  return [
    "tick " + state.tick + " (" + Math.round(state.tick * DT) + " s), players " + state.order.length,
    "reactor: " + r.regime + (r.scram ? " SCRAM" : "") + ", T " + Math.round(r.T) + " C, E " + (Math.round(r.E * 10) / 10) + " MWe, rods " + (Math.round(r.R * 100) / 100),
    "grid: V " + (Math.round(pw.V * 100) / 100) + ", battery " + (Math.round(pw.B * 100) / 100) + ", tripped: " + (tripped.length ? tripped.join(",") : "none"),
    "water (m3 per compartment): " + fills.join(" ") + ", leaks " + state.leaks.list.length,
    "boat: depth " + (Math.round(state.boat.depth * 10) / 10) + " m, telegraph " + PROP_NAMES[state.prop.pos] + ", speed " + (Math.round(state.prop.speed * 10) / 10) + " m/s",
    "manual page " + state.manual.page + ", digest " + stateDigest(state)
  ];
}

// Run one command line for player \`id\` (called from simStep for the input that carries it).
function debugCommand(state, id, pl, line, tick) {
  if (!state.debug.enabled) { dbgReply(state, id, "debug disabled on this server (set SOUSTENSION_DEBUG=1 for a local debug server)"); return; }
  var tok = line.trim().split(/\s+/);
  var cmd = tok[0].toLowerCase();
  var reply = function (t) { dbgReply(state, id, t); };
  var i, n, v;
  if (cmd === "help") {
    reply("help | state | tp x z | regime veille|croisiere|pleine | scram | restart | leak 1-6 1-3 [g|d] | unleak | break pump|bilge 1-2 | repair pump|bilge 1-2");
    reply("trip <breaker> | water 1-6 m3 | bring <item> | get <param> | set <param> <value|default> | ff <seconds>");
  } else if (cmd === "state") {
    var lines = debugState(state);
    for (i = 0; i < lines.length; i++) reply(lines[i]);
  } else if (cmd === "tp") {
    var x = dbgNum(tok[1]), z = dbgNum(tok[2]);
    if (isNaN(x) || isNaN(z)) { reply("usage: tp <x> <z>"); return; }
    pl.x = clamp(x, -HALF_X, HALF_X); pl.z = clamp(z, -HALF_Z, HALF_Z);
    reply("teleported to " + pl.x + " " + pl.z);
  } else if (cmd === "regime") {
    if (!reactorSetRegime(state.reactor, tok[1])) { reply("usage: regime veille|croisiere|pleine"); return; }
    reply("regime " + tok[1]);
  } else if (cmd === "scram") {
    reactorScram(state.reactor); reply("SCRAM");
  } else if (cmd === "restart") {
    reactorRestart(state.reactor); state.lever.reset = false; state.lever.cover = 0; reply("reactor restarted (no procedure)");
  } else if (cmd === "leak") {
    var c = dbgCompartment(tok[1]), size = dbgNum(tok[2]);
    if (c < 0 || !(size >= 1 && size <= 3 && Math.floor(size) === size)) { reply("usage: leak <1-6> <1-3> [g|d]"); return; }
    var comp = WATER_COMPARTMENTS[c];
    var k = leakCreate(state.leaks, (comp.z0 + comp.z1) / 2, tok[3] === "g" ? -1 : 1, size);
    reply("leak " + k.id + " in compartment " + (c + 1) + ", size " + size + ", " + (k.side < 0 ? "left" : "right") + " wall (z " + Math.round(k.z * 10) / 10 + ")");
  } else if (cmd === "unleak") {
    n = state.leaks.list.length; state.leaks.list = []; reply(n + " leak(s) closed");
  } else if (cmd === "break" || cmd === "repair") {
    n = dbgNum(tok[2]);
    if ((tok[1] !== "pump" && tok[1] !== "bilge") || !(n === 1 || n === 2)) { reply("usage: " + cmd + " pump|bilge 1-2"); return; }
    if (tok[1] === "pump") (cmd === "break" ? reactorBreakPump : reactorRepairPump)(state.reactor, n - 1);
    else (cmd === "break" ? bilgeBreak : bilgeRepair)(state.bilge, n - 1);
    reply(tok[1] + " " + n + (cmd === "break" ? " broken" : " repaired"));
  } else if (cmd === "trip") {
    var bi = -1;
    for (i = 0; i < BREAKERS.length; i++) if (BREAKERS[i].id === tok[1]) bi = i;
    if (bi < 0) { reply("unknown breaker; ids: " + BREAKERS.map(function (b) { return b.id; }).join(",")); return; }
    breakerTrip(state.power, bi); reply("breaker " + tok[1] + " tripped");
  } else if (cmd === "water") {
    var wc = dbgCompartment(tok[1]); v = dbgNum(tok[2]);
    if (wc < 0 || !(v > 0)) { reply("usage: water <1-6> <m3>"); return; }
    reply(waterAdd(state.water, wc, v, 0) + " m3 added to compartment " + (wc + 1));
  } else if (cmd === "bring") {
    var item = cargoById(state.cargo, tok[1]);
    if (!item) { reply("unknown item; ids: " + state.cargo.map(function (c2) { return c2.id; }).join(",")); return; }
    releaseItem(state, item.id);
    item.x = pl.x; item.z = pl.z; item.fly = false; item.y = 0; item.active = true;
    reply(item.id + " brought to you");
  } else if (cmd === "get") {
    var gp = dbgParam(tok[1]);
    if (!gp) { reply("unknown parameter; known: " + Object.keys(DEBUG_PARAMS).join(", ")); return; }
    reply(tok[1] + " = " + gp.get() + " (default " + gp.def + ", range " + gp.min + ".." + gp.max + ")");
  } else if (cmd === "set") {
    var sp = dbgParam(tok[1]);
    if (!sp) { reply("unknown parameter; known: " + Object.keys(DEBUG_PARAMS).join(", ")); return; }
    v = tok[2] === "default" ? sp.def : dbgNum(tok[2]);
    if (isNaN(v)) { reply("usage: set <param> <number|default>"); return; }
    if (v < sp.min || v > sp.max) { reply(tok[1] + " must be between " + sp.min + " and " + sp.max); return; }
    sp.set(v);
    reply(tok[1] + " = " + sp.get());
  } else if (cmd === "ff") {
    v = dbgNum(tok[1]);
    if (!(v > 0) || v > DEBUG_FF_MAX_SECONDS) { reply("usage: ff <seconds, 0-" + DEBUG_FF_MAX_SECONDS + ">"); return; }
    state.debug.pending += Math.round(v * TICK_RATE);
    reply("fast-forwarding " + v + " s");
  } else {
    reply("unknown command '" + cmd + "' (try help)");
  }
}
