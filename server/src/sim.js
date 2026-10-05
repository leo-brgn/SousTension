// ---- The fixed-step simulation (E1-06) ------------------------------------------------------------------------------------
// The whole authoritative game state advances in ONE function, simStep(state, tick), called by the match loop exactly once per 10 Hz tick. It
// has no knowledge of Nakama, the network or the clock: it reads and writes `state` and nothing else, so it can be driven headlessly (tests,
// the balance tool of E3-10) and gives the same result for the same inputs. Rules that keep it deterministic (CLAUDE.md, architecture rule 1):
//   - no wall-clock time (Date, performance.now), no timers, no Math.random: randomness only from a seeded generator stored IN the state
//     (reactor drift, leak schedule); a test scans server/src for the forbidden APIs;
//   - iteration order is fixed (arrays and the players' join order, never object key order).
//
// Order of the steps inside a tick (changing it changes the game: the golden digest test will tell):
//   1. inputs      queued player inputs applied under the per-player budget (movement, press / hold / grab)
//   2. coupled     Rule of Two Players: arm / succeed / expire the coupled actions (and run their effects)
//   3. leaks       the scheduled leak may open; every open leak pours into its compartment
//   4. bilge       running bilge pumps take water out of their compartment
//   5. water       flow between compartments through the open openings (trim / list follow)
//   6. cargo       carried cargo follows its carriers, loose cargo slides on the (water-tilted) floor, used patches respawn
//   7. reactor     rods, heat, steam, electricity, cooling, drift, automatic protection
//   8. power       bus voltage from the surplus electricity, emergency battery, breakers that trip
//   9. propulsion  the boat's speed follows the telegraph set-point x bus voltage
//  10. lever       the SCRAM cover falls shut
//  11. boat        buoyancy: descent speed and depth follow the SCRAM latch
function simStep(state, tick) {
  // 1. inputs: +1 per tick per player (the nominal input rate), capped at MAX_ALLOWANCE. In steady state this is exactly one input per tick;
  //    after a network stall (TCP retransmission) the backlog drains in a few ticks instead of lagging forever. The long-term rate can never
  //    exceed TICK_RATE inputs/s, so flooding the server with inputs does not speed a player up.
  for (var k = 0; k < state.order.length; k++) {
    var pl = state.players[state.order[k]];
    pl.allowance = Math.min(MAX_ALLOWANCE, pl.allowance + 1);
    while (pl.allowance >= 1 && pl.queue.length > 0) {
      var next = pl.queue.shift();
      stepPlayer(pl, next.mx, next.mz);
      if (next.act) { if (next.use) useTarget(state, state.order[k], pl, tick, next.use); else tryAct(state, state.order[k], pl, tick); }
      if (next.hold) { if (next.use) holdTarget(state, pl, next.use); else tryHold(state, pl); }
      if (next.grab) tryGrab(state.cargo, state.order[k], pl, tick);
      pl.seq = next.seq;
      pl.applied += 1;
      pl.allowance -= 1;
    }
  }
  evaluateCoupled(state, tick);
  leakStep(state, tick);
  bilgeStep(state);
  waterStep(state.water);
  updateCargo(state, tick);
  reactorStep(state.reactor);
  powerStep(state);
  propulsionStep(state);
  leverStep(state.lever);
  boatStep(state.boat, state.reactor);
  state.tick = tick;
}

// Queue one parsed client input on a player (stale or duplicate sequences are rejected, a flooding client loses its oldest inputs).
function queueInput(p, input) {
  if (typeof input.seq !== "number" || input.seq <= p.lastQueued) return false;
  p.lastQueued = input.seq;
  p.queue.push({ seq: input.seq, mx: +input.mx || 0, mz: +input.mz || 0, act: input.act === true || input.act === 1,
                 grab: input.grab === true || input.grab === 1, hold: input.hold === true || input.hold === 1,
                 use: typeof input.use === "string" && input.use.length <= 40 ? input.use : "" });
  while (p.queue.length > MAX_QUEUED_INPUTS) p.queue.shift();
  return true;
}

// A 64-bit fingerprint (two FNV-1a hashes, hex) of the WHOLE game state: same state, same digest. Object keys are sorted so the digest does not
// depend on insertion order; the Nakama presence objects (network handles, not game state) are left out.
function stateDigest(state) {
  var text = stableStringify(state);
  var h1 = 0x811c9dc5, h2 = 0x01000193 ^ 0x9747b28c;
  for (var i = 0; i < text.length; i++) {
    var c = text.charCodeAt(i);
    h1 = Math.imul(h1 ^ c, 0x01000193) >>> 0;
    h2 = Math.imul(h2 ^ c, 0x85ebca6b) >>> 0;
  }
  return ("00000000" + h1.toString(16)).slice(-8) + ("00000000" + h2.toString(16)).slice(-8);
}

function stableStringify(v) {
  if (v === null || typeof v !== "object") return typeof v === "number" && !isFinite(v) ? "\"" + String(v) + "\"" : JSON.stringify(v);
  if (Array.isArray(v)) {
    var a = [];
    for (var i = 0; i < v.length; i++) a.push(stableStringify(v[i]));
    return "[" + a.join(",") + "]";
  }
  var keys = Object.keys(v).sort(), o = [];
  for (var k = 0; k < keys.length; k++) {
    if (keys[k] === "presence") continue;
    o.push(JSON.stringify(keys[k]) + ":" + stableStringify(v[keys[k]]));
  }
  return "{" + o.join(",") + "}";
}
