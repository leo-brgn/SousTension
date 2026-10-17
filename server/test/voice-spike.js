// Spike E8-07a: how late does a voice packet arrive when it is relayed by the authoritative match (10 Hz loop)?
// 4 clients join a bare test match (rpc create_test_match, debug mode), send 20 ms Opus-sized packets (60 bytes), and measure the delay at the listeners.
// All clients live in this one process, so they share one clock: the delay is exact. Needs Nakama (docker compose up).
// Usage: node server/test/voice-spike.js
const HOST = "127.0.0.1:7350";
const SERVER_KEY = "defaultkey";
const OP_VOICE = 4;
const PACKET_BYTES = 60, FRAME_MS = 20, SECONDS = 8;

const b64 = (s) => Buffer.from(s).toString("base64");

async function auth(deviceId) {
  const r = await fetch(`http://${HOST}/v2/account/authenticate/device?create=true`, {
    method: "POST", headers: { Authorization: "Basic " + b64(SERVER_KEY + ":"), "Content-Type": "application/json" }, body: JSON.stringify({ id: deviceId })
  });
  if (!r.ok) throw new Error("auth failed " + r.status);
  return (await r.json()).token;
}
async function createTestMatch(token) {
  const r = await fetch(`http://${HOST}/v2/rpc/create_test_match?http_key=`, { method: "POST", headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" }, body: JSON.stringify("{}") });
  if (!r.ok) throw new Error("rpc failed " + r.status + " " + (await r.text()));
  return JSON.parse((await r.json()).payload).matchId;
}
function connect(token) {
  return new Promise((resolve, reject) => { const ws = new WebSocket(`ws://${HOST}/ws?token=${token}&format=json`); ws.onopen = () => resolve(ws); ws.onerror = () => reject(new Error("ws error")); });
}

async function client(idx, matchId, token) {
  const ws = await connect(token);
  const me = { idx, userId: null, delays: [], got: new Map(), bytesDown: 0 };
  ws.onmessage = (ev) => {
    me.bytesDown += ev.data.length;
    const msg = JSON.parse(ev.data);
    if (msg.match) me.userId = msg.match.self.user_id;
    const md = msg.match_data;
    if (md && Number(md.op_code) === OP_VOICE) {
      const buf = Buffer.from(md.data, "base64");
      const sender = buf.readUInt8(0), seq = buf.readUInt32BE(1), sentAt = buf.readDoubleBE(5);
      if (sender === idx) return;                                   // own packet echoed back
      me.delays.push(performance.now() - sentAt);
      const k = sender;
      me.got.set(k, (me.got.get(k) || 0) + 1);
    }
  };
  ws.send(JSON.stringify({ cid: "1", match_join: { match_id: matchId } }));
  await new Promise((r) => setTimeout(r, 500));
  me.ws = ws;
  me.talk = (seconds) => new Promise((resolve) => {
    let seq = 0, sent = 0;
    const start = performance.now();
    const tick = () => {                                            // drift-free 20 ms schedule
      const due = Math.floor((performance.now() - start) / FRAME_MS);
      while (seq < due && seq < seconds * 1000 / FRAME_MS) {
        const buf = Buffer.alloc(PACKET_BYTES);
        buf.writeUInt8(idx, 0); buf.writeUInt32BE(seq, 1); buf.writeDoubleBE(performance.now(), 5);
        ws.send(JSON.stringify({ match_data_send: { match_id: matchId, op_code: String(OP_VOICE), data: buf.toString("base64") } }));
        seq++; sent++;
      }
      if (seq >= seconds * 1000 / FRAME_MS) return resolve(sent);
      setTimeout(tick, 4);
    };
    tick();
  });
  return me;
}

const pct = (a, p) => { const s = a.slice().sort((x, y) => x - y); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : NaN; };

(async () => {
  const tokens = await Promise.all([0, 1, 2, 3].map((i) => auth("voice-spike-" + i + "-0000000000")));
  const matchId = await createTestMatch(tokens[0]);
  console.log("test match:", matchId);
  const clients = [];
  for (let i = 0; i < 4; i++) clients.push(await client(i, matchId, tokens[i]));

  for (const [label, talkers] of [["1 speaker", [0]], ["4 speakers", [0, 1, 2, 3]]]) {
    for (const c of clients) { c.delays = []; c.got.clear(); c.bytesDown = 0; }
    const sent = await Promise.all(talkers.map((i) => clients[i].talk(SECONDS)));
    await new Promise((r) => setTimeout(r, 500));
    const expected = sent[0] * (talkers.length - (talkers.includes(1) ? 0 : 0));
    console.log(`\n== ${label}: ${sent[0]} packets of ${PACKET_BYTES} B per speaker`);
    clients.forEach((c) => {
      const others = talkers.filter((t) => t !== c.idx).length;
      const want = sent[0] * others;
      const got = c.delays.length;
      console.log(`listener ${c.idx}: received ${got}/${want} (${want ? (100 - 100 * got / want).toFixed(1) : 0} % lost)  delay p50=${pct(c.delays, 0.5).toFixed(0)} ms  p95=${pct(c.delays, 0.95).toFixed(0)} ms  p99=${pct(c.delays, 0.99).toFixed(0)} ms  max=${Math.max(...c.delays, 0).toFixed(0)} ms  downlink=${(c.bytesDown / SECONDS / 1024).toFixed(1)} kB/s`);
    });
  }
  clients.forEach((c) => c.ws.close());
  process.exit(0);
})().catch((e) => { console.error(e); process.exit(1); });
