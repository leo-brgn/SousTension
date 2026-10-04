function newInterlock() {
  return { st: [{ by: "", tick: 0 }, { by: "", tick: 0 }], result: "none", resultTick: 0, count: 0 };
}

// A player at a station presses the key. Rejected when out of reach, when the station is already held, or when
// the same player already holds the other station (the rule needs two different players).
function tryActivate(il, id, pl, tick) {
  var best = -1, bestD = STATION_REACH;
  for (var i = 0; i < STATIONS.length; i++) {
    var dx = pl.x - STATIONS[i].x, dz = pl.z - STATIONS[i].z;
    var d = Math.sqrt(dx * dx + dz * dz);
    if (d <= bestD) { best = i; bestD = d; }
  }
  if (best < 0 || il.st[best].by !== "") return;
  if (il.st[1 - best].by === id) return;
  il.st[best].by = id; il.st[best].tick = tick;
}

// Success when both stations are held (by different players, guaranteed by tryActivate) and each was pressed
// within the window of the first one; otherwise the first press expires after the window.
function evaluateInterlock(il, tick) {
  var a = il.st[0], b = il.st[1];
  if (a.by !== "" && b.by !== "") {
    il.result = "success"; il.resultTick = tick; il.count++;
    a.by = ""; b.by = ""; return;
  }
  for (var i = 0; i < 2; i++) {
    var s = il.st[i];
    if (s.by !== "" && tick - s.tick >= INTERLOCK_WINDOW_TICKS) {
      il.result = "timeout"; il.resultTick = tick; s.by = "";
    }
  }
}

function interlockView(il, tick) {
  function rem(s) { return s.by === "" ? 0 : Math.max(0, INTERLOCK_WINDOW_TICKS - (tick - s.tick)); }
  return { a: rem(il.st[0]), b: rem(il.st[1]), ab: il.st[0].by, bb: il.st[1].by, result: il.result, rt: il.resultTick, n: il.count };
}

