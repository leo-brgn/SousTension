// ---- The OK-114 Operating Manual (E5-02) --------------------------------------------------------------------------------------
// GDD 3.5: a physical 8 kg binder at the central post, ONE copy. It must be held with two hands, so whoever reads cannot act (the two-handed rule
// of hands.js locks every control): the game nudges players towards a reader / doer duo. v0 ("papier / placeholder"): a contents page and five
// procedures of at most 5 steps each, all true to the game as it is; the texts live on the CLIENT under localization keys
// (manual.<page id>.title, manual.<page id>.step<k>), the server only knows the page ids and how many steps each has. The current page is global
// state; pages are turned only while holding the binder. Tearing pages out, annotations and errata are v1; opening on the right page when an
// alarm rings (E5-04) will use manualGoto.
var MANUAL_PAGES = [
  { id: "index", steps: 0 },          // contents
  { id: "scram", steps: 2 },          // lift the cover, pull the lever
  { id: "restart", steps: 4 },        // lever back, valves, pumps, the two keys
  { id: "leak", steps: 4 },           // patch, carry, click the leak, bail
  { id: "power", steps: 3 },          // find the tripped breaker, re-arm, shed load
  { id: "propulsion", steps: 2 }      // telegraph, more speed = less light
];
var MANUAL_MAX_STEPS = 5;

function newManual() { return { page: 0 }; }

function manualPageIndex(id) {
  for (var i = 0; i < MANUAL_PAGES.length; i++) if (MANUAL_PAGES[i].id === id) return i;
  return -1;
}

// Turn the page by dir (+1 / -1), stopping at the first and last page. Only the player holding the binder can: returns whether the page changed.
function manualFlip(state, playerId, dir) {
  if (!handItem(state, playerId, "manual")) return false;
  var next = Math.max(0, Math.min(MANUAL_PAGES.length - 1, state.manual.page + (dir > 0 ? 1 : -1)));
  if (next === state.manual.page) return false;
  state.manual.page = next;
  return true;
}

// Open the manual on a page, by id or index (E5-04: the page of the alarm that rings). Returns whether the page exists.
function manualGoto(state, page) {
  var i = typeof page === "number" ? page : manualPageIndex(page);
  if (i < 0 || i >= MANUAL_PAGES.length) return false;
  state.manual.page = i;
  return true;
}

// Broadcast (and resync): p = current page, n = number of pages.
function manualView(manual) { return { p: manual.page, n: MANUAL_PAGES.length }; }
