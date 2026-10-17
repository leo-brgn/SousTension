// Waypoints for a scripted walker through the boat of E10-01: a straight line is stopped by the bulkheads, so a walker goes through the hatch of each
// bulkhead it crosses and around the privacy baffle 0.95 m behind it. route(m, from, to) -> [{x, z}, ...] ending at `to`.
// Around each bulkhead there are three zones: 0 = fore of the wall, 1 = the pocket between the wall and the baffle, 2 = aft of the baffle.
function route(m, from, to) {
  const pts = [];
  const B = m.BULKHEAD_Z, H = m.HATCH_X;
  const zone = (d, z) => (z > B[d] ? 0 : (z > B[d] - 1.2 ? 1 : 2));
  const sx = (d) => H[d] + (H[d] < 0 ? 1 : -1) * 1.25;            // the baffle sits in front of the hatch: pass it on the side that has room
  if (to.z < from.z) {                                              // towards the stern: bulkheads in the order met
    for (let d = 0; d < B.length; d++) {
      const zf = zone(d, from.z), zt = zone(d, to.z);
      if (zf >= zt) continue;
      if (zf === 0) { if (from.z > B[d] + 0.7) pts.push({ x: H[d], z: B[d] + 0.7 }); pts.push({ x: H[d], z: B[d] - 0.48 }); }
      if (zt === 2) { if (Math.abs(from.x - H[d]) < 1.1 || zf === 0) pts.push({ x: sx(d), z: B[d] - 0.48 }); pts.push({ x: sx(d), z: B[d] - 1.4 }); }
    }
  } else {                                                           // towards the bow
    for (let d = B.length - 1; d >= 0; d--) {
      const zf = zone(d, from.z), zt = zone(d, to.z);
      if (zf <= zt) continue;
      if (zf === 2) pts.push({ x: sx(d), z: B[d] - 1.4 }, { x: sx(d), z: B[d] - 0.48 }, { x: H[d], z: B[d] - 0.48 });
      else if (Math.abs(from.x - H[d]) > 0.3) pts.push({ x: H[d], z: B[d] - 0.48 });
      if (zt === 0) pts.push({ x: H[d], z: B[d] + 0.7 });
    }
  }
  pts.push({ x: to.x, z: to.z });
  return pts;
}
module.exports = { route };
