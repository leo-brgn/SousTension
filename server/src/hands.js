// ---- Hands: one hand = one thing (E2-03) ------------------------------------------------------------------------------------------
// GDD 5: "une main = une chose". A player has a left hand, a right hand and ONE chest pocket (a single small item: a torch, a stamp, a sandwich),
// no inventory beyond that. Items are described in data (ITEM_KINDS): how many hands they need and whether they fit the pocket. A two-handed
// item (a crate, the fuel flask, later the 8 kg Manual) takes both hands and LOCKS every other action: pressing a control, holding a valve
// wheel or arming a command of the Rule of Two Players all need a free hand (canUseHands). Using the item you hold (a patch on a leak, the
// bucket in the water) needs no extra hand. The heavy flask is carried by two players, each with both hands.
//
// Protocol (input fields): take (pick the nearest item into free hands), drop (true = the last item taken, or "L" / "R" / "P" for a slot), stow
// (a pocketable one-hand item goes hand -> pocket, or pocket -> hand). The legacy field grab stays: it drops what you hold, else takes (the old F key).
var ITEM_KINDS = {
  crate: { hands: 2 },
  fuel: { hands: 2 },
  patch: { hands: 1 },
  bucket: { hands: 1 },
  flashlight: { hands: 1, pocket: true }
};

function newHands() { return { l: "", r: "", p: "", order: [] }; }     // slots hold cargo ids; order = items in the order they were taken

function itemKind(item) { return ITEM_KINDS[item.kind] || ITEM_KINDS.crate; }
function cargoById(cargo, id) {
  for (var i = 0; i < cargo.length; i++) if (cargo[i].id === id) return cargo[i];
  return null;
}
function freeHandCount(h) { return (h.l === "" ? 1 : 0) + (h.r === "" ? 1 : 0); }
function holdsTwoHanded(h) { return h.l !== "" && h.l === h.r; }
function holdsAnything(h) { return h.l !== "" || h.r !== "" || h.p !== ""; }
// A control may be pressed (or a valve held, or a command armed) only with a free hand and no two-handed item.
function canUseHands(pl) { return !holdsTwoHanded(pl.hands) && freeHandCount(pl.hands) >= 1; }

// The item of a given kind held in a HAND (not the pocket) by the player, or null (bucket, patch...).
function handItem(state, playerId, kind) {
  var h = state.players[playerId].hands;
  var slots = [h.r, h.l];
  for (var i = 0; i < slots.length; i++) {
    if (slots[i] === "") continue;
    var item = cargoById(state.cargo, slots[i]);
    if (item && item.kind === kind && item.carriers.indexOf(playerId) >= 0) return item;
  }
  return null;
}

function removeFromHands(h, itemId) {
  if (h.l === itemId) h.l = "";
  if (h.r === itemId) h.r = "";
  if (h.p === itemId) h.p = "";
  var k = h.order.indexOf(itemId);
  if (k >= 0) h.order.splice(k, 1);
}

// Pick the nearest free item within reach into free hands. A heavy item needs a second player taking it within the window (cargo.js: pend).
function takeItem(state, id, pl, tick) {
  var h = pl.hands, best = null, bestD = GRAB_REACH;
  for (var i = 0; i < state.cargo.length; i++) {
    var c = state.cargo[i];
    if (!c.active) continue;                                        // a used patch is not in the world until it respawns
    if (c.carriers.length >= (c.heavy ? 2 : 1)) continue;
    if (c.carriers.indexOf(id) >= 0 || c.pend === id) continue;
    var dx = pl.x - c.x, dz = pl.z - c.z, d = Math.sqrt(dx * dx + dz * dz);
    if (d <= bestD) { best = c; bestD = d; }
  }
  if (!best) return false;
  var need = itemKind(best).hands;
  if (freeHandCount(h) < need) return false;
  if (best.heavy) {
    if (best.pend === "") { best.pend = id; best.pendTick = tick; }
    else { best.carriers = [best.pend, id]; best.pend = ""; }
  } else best.carriers = [id];
  if (need === 2) { h.l = best.id; h.r = best.id; } else if (h.r === "") h.r = best.id; else h.l = best.id;
  h.order.push(best.id);
  return true;
}

// Let go of an item: it stays where the player stands. which: true / "" = the last item taken, "L" / "R" / "P" = a slot. Dropping a shared
// (heavy) carry makes both carriers let go.
function dropItem(state, id, pl, which) {
  var h = pl.hands, itemId = "";
  if (which === "L") itemId = h.l; else if (which === "R") itemId = h.r; else if (which === "P") itemId = h.p;
  else if (h.order.length > 0) itemId = h.order[h.order.length - 1];
  if (itemId === "") return false;
  releaseItem(state, itemId);
  return true;
}

// The item leaves everybody's hands and carriers (a dropped flask drops for both of its carriers).
function releaseItem(state, itemId) {
  var item = cargoById(state.cargo, itemId);
  if (item) { item.carriers = []; item.pend = ""; item.vx = 0; item.vz = 0; }
  for (var k = 0; k < state.order.length; k++) removeFromHands(state.players[state.order[k]].hands, itemId);
}

// hand <-> pocket for a pocketable one-hand item: the pocket holds ONE small thing.
function stowItem(state, id, pl) {
  var h = pl.hands;
  if (h.p !== "") {                                                 // pocket -> a free hand
    if (freeHandCount(h) < 1) return false;
    var out = h.p;
    h.p = "";
    if (h.r === "") h.r = out; else h.l = out;
    return true;
  }
  for (var i = h.order.length - 1; i >= 0; i--) {                   // hand -> pocket: the last pocketable one-hand item taken
    var item = cargoById(state.cargo, h.order[i]);
    if (!item || !itemKind(item).pocket || itemKind(item).hands !== 1) continue;
    if (h.l === item.id) h.l = ""; else if (h.r === item.id) h.r = ""; else continue;
    h.p = item.id;
    return true;
  }
  return false;
}

// The legacy F key: let go of what you hold, otherwise pick up the nearest item.
function grabToggle(state, id, pl, tick) {
  if (holdsAnything(pl.hands)) dropItem(state, id, pl, true);
  else takeItem(state, id, pl, tick);
}

// A player left the match: everything they held is let go.
function releasePlayerItems(state, id) {
  var pl = state.players[id];
  if (!pl) return;
  var ids = [pl.hands.l, pl.hands.r, pl.hands.p];
  for (var i = 0; i < ids.length; i++) if (ids[i] !== "") releaseItem(state, ids[i]);
  for (var c = 0; c < state.cargo.length; c++) if (state.cargo[c].pend === id) state.cargo[c].pend = "";
}

// Keep the slots truthful: an item that is no longer carried by that player (the second carrier did not come in time, a used patch, a carrier who
// left) leaves their hands.
function syncHands(state) {
  for (var k = 0; k < state.order.length; k++) {
    var pid = state.order[k], h = state.players[pid].hands;
    var slots = ["l", "r", "p"];
    for (var s = 0; s < slots.length; s++) {
      var itemId = h[slots[s]];
      if (itemId === "") continue;
      var item = cargoById(state.cargo, itemId);
      var valid = item && item.active && (item.carriers.indexOf(pid) >= 0 || item.pend === pid);
      if (!valid) removeFromHands(h, itemId);
    }
  }
}

// Broadcast (and resync): what a player holds, [left, right, pocket] as cargo ids ("" = empty).
function handsView(h) { return [h.l, h.r, h.p]; }
