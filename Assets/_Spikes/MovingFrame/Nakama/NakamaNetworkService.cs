using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// <see cref="INetworkService"/> backed by a Nakama authoritative match (server/modules/index.js).
    /// Protocol: opcode 1 = input {seq, mx, mz} (client -> server), opcode 2 = state {tick, t, players[]} (server -> clients).
    /// Socket callbacks may run off the main thread, so snapshots are queued and delivered from <see cref="Poll"/>.
    /// </summary>
    public sealed class NakamaNetworkService : INetworkService
    {
        private const long OpInput = 1;
        private const long OpState = 2;

        [Serializable] private class InputDto { public int seq; public float mx; public float mz; public bool act; public bool grab; public bool hold; }
        [Serializable] private class PlayerDto { public string id; public float x; public float z; public int seq; }
        [Serializable] private class InterlockDto { public int a; public int b; public string ab; public string bb; public string result; public int rt; public int n; }
        [Serializable] private class CoupledDto { public string id; public int a; public int b; public string ab; public string bb; public string result; public int rt; public int n; }
        [Serializable] private class CargoDto { public string id; public float x; public float z; public int h; public string[] c; public string p; public string k; public int a; }
        [Serializable] private class ReactorDto { public string reg; public float R; public float nz; public float P; public float T; public float S; public float E; public float eta; public float F; public float[] v; public int[] pu; public int scram; public int auto; public int leak; public int warn; public int crit; }
        [Serializable] private class LeverDto { public int cv; public int pl; }
        [Serializable] private class RestartDto { public int[] s; public string last; public int lt; public int n; }
        [Serializable] private class GridDto { public float v; public float b; public int em; public int[] br; public int[] lt; public float dm; public float su; }
        [Serializable] private class BilgeDto { public int s; public int r; }
        [Serializable] private class LeakDto { public int id; public int c; public float x; public float z; public int s; public string t; }
        [Serializable] private class WaterDto { public float[] l; public float m; public float tr; public float li; public int[] dr; }
        [Serializable] private class BoatDto { public float d; public float vz; }
        [Serializable] private class StateDto { public int tick; public double t; public PlayerDto[] players; public InterlockDto il; public CargoDto[] cargo; public ReactorDto rx; public CoupledDto[] cp; public RestartDto rs; public WaterDto bw; public LeakDto[] lk; public BilgeDto[] bp; public GridDto pw; public LeverDto sc; public BoatDto boat; }
        [Serializable] private class MatchDto { public string matchId; }

        private readonly string _scheme, _host, _serverKey, _deviceId;
        private readonly int _port;
        private readonly ConcurrentQueue<StateSnapshot> _queue = new ConcurrentQueue<StateSnapshot>();

        private IClient _client;
        private ISession _session;
        private ISocket _socket;
        private string _matchId;
        private long _bytesSent, _bytesReceived;

        public event Action<StateSnapshot> StateReceived;
        public string LocalUserId => _session?.UserId;
        public long BytesSent => Interlocked.Read(ref _bytesSent);
        public long BytesReceived => Interlocked.Read(ref _bytesReceived);

        public NakamaNetworkService(string deviceId, string host = "127.0.0.1", int port = 7350,
            string scheme = "http", string serverKey = "defaultkey")
        {
            _deviceId = deviceId; _host = host; _port = port; _scheme = scheme; _serverKey = serverKey;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _client = new Client(_scheme, _host, _port, _serverKey, UnityWebRequestAdapter.Instance);
            _session = await _client.AuthenticateDeviceAsync(_deviceId);
            var rpc = await _client.RpcAsync(_session, "get_moving_frame_match", "{}");
            _matchId = JsonUtility.FromJson<MatchDto>(rpc.Payload).matchId;

            _socket = _client.NewSocket();
            _socket.ReceivedMatchState += OnMatchState;
            await _socket.ConnectAsync(_session, true);
            await _socket.JoinMatchAsync(_matchId);
        }

        public void SendInput(int seq, float moveX, float moveZ, bool act, bool grab, bool hold = false)
        {
            if (_socket == null || !_socket.IsConnected) return;
            var json = JsonUtility.ToJson(new InputDto { seq = seq, mx = moveX, mz = moveZ, act = act, grab = grab, hold = hold });
            var bytes = Encoding.UTF8.GetBytes(json);
            Interlocked.Add(ref _bytesSent, bytes.Length);
            _ = SendAsync(bytes);
        }

        private async Task SendAsync(byte[] bytes)
        {
            try { await _socket.SendMatchStateAsync(_matchId, OpInput, bytes); }
            catch (Exception e) { Debug.LogWarning("[Nakama] send failed: " + e.Message); }
        }

        private void OnMatchState(IMatchState state)
        {
            if (state.OpCode != OpState) return;
            Interlocked.Add(ref _bytesReceived, state.State.Length);
            var dto = JsonUtility.FromJson<StateDto>(Encoding.UTF8.GetString(state.State));
            var players = new PlayerState[dto.players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                var p = dto.players[i];
                players[i] = new PlayerState(p.id, p.x, p.z, p.seq);
            }
            var il = dto.il == null ? default : new InterlockState(dto.il.a, dto.il.b, dto.il.ab, dto.il.bb, dto.il.result, dto.il.rt, dto.il.n);
            var cargo = new CargoState[dto.cargo == null ? 0 : dto.cargo.Length];
            for (int i = 0; i < cargo.Length; i++)
            {
                var c = dto.cargo[i];
                cargo[i] = new CargoState(c.id, c.x, c.z, c.h == 1, c.c, c.p, c.k, c.a != 0);
            }
            ReactorState reactor = default;
            if (dto.rx != null && !string.IsNullOrEmpty(dto.rx.reg))
            {
                var x = dto.rx;
                reactor = new ReactorState(x.reg, x.R, x.nz, x.P, x.T, x.S, x.E, x.eta, x.F, x.v,
                    new[] { x.pu != null && x.pu.Length > 0 && x.pu[0] == 1, x.pu != null && x.pu.Length > 1 && x.pu[1] == 1 },
                    x.scram == 1, x.auto == 1, x.leak == 1, x.warn == 1, x.crit == 1,
                    new[] { x.pu != null && x.pu.Length > 0 && x.pu[0] == 2, x.pu != null && x.pu.Length > 1 && x.pu[1] == 2 });
            }
            var coupled = new CoupledActionState[dto.cp == null ? 0 : dto.cp.Length];
            for (int i = 0; i < coupled.Length; i++)
            {
                var c = dto.cp[i];
                coupled[i] = new CoupledActionState(c.id, new InterlockState(c.a, c.b, c.ab, c.bb, c.result, c.rt, c.n));
            }
            var restart = dto.rs != null && dto.rs.s != null && dto.rs.s.Length >= 3
                ? new RestartState(dto.rs.s[0] == 1, dto.rs.s[1] == 1, dto.rs.s[2] == 1, dto.rs.last, dto.rs.lt, dto.rs.n) : default;
            WaterState water = default;
            if (dto.bw != null && dto.bw.l != null)
            {
                var doors = new bool[dto.bw.dr == null ? 0 : dto.bw.dr.Length];
                for (int i = 0; i < doors.Length; i++) doors[i] = dto.bw.dr[i] == 1;
                water = new WaterState(dto.bw.l, dto.bw.m, dto.bw.tr, dto.bw.li, doors);
            }
            LeakState[] leaks = null;
            if (dto.lk != null)
            {
                leaks = new LeakState[dto.lk.Length];
                for (int i = 0; i < leaks.Length; i++) { var k = dto.lk[i]; leaks[i] = new LeakState(k.id, k.c, k.x, k.z, k.s, k.t); }
            }
            BilgeState bilge = default;
            if (dto.bp != null)
            {
                var st = new int[dto.bp.Length]; var run = new bool[dto.bp.Length];
                for (int i = 0; i < st.Length; i++) { st[i] = dto.bp[i].s; run[i] = dto.bp[i].r == 1; }
                bilge = new BilgeState(st, run);
            }
            var grid = dto.pw != null && dto.pw.br != null ? new GridState(dto.pw.v, dto.pw.b, dto.pw.em == 1, dto.pw.br, dto.pw.lt, dto.pw.dm, dto.pw.su) : default;
            var lever = dto.sc != null ? new ScramLeverState(dto.sc.cv == 1, dto.sc.pl == 1) : default;
            var boat = dto.boat != null ? new BoatDepthState(dto.boat.d, dto.boat.vz) : default;
            _queue.Enqueue(new StateSnapshot(dto.tick, dto.t, players, il, cargo, reactor, lever, boat, coupled, restart, water, leaks, bilge, grid));
        }

        public void Poll()
        {
            while (_queue.TryDequeue(out var snapshot)) StateReceived?.Invoke(snapshot);
        }

        public void Dispose()
        {
            if (_socket != null)
            {
                _socket.ReceivedMatchState -= OnMatchState;
                _ = _socket.CloseAsync();
            }
        }
    }
}
