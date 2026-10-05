using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SousTension.Sim;

namespace SousTension.Spikes.MovingFrame.Tests
{
    internal sealed class FakeClock : IClockService
    {
        public double Now { get; set; }
    }

    internal sealed class ConstantInput : IInputSource
    {
        public float Mx, Mz; public bool Act, Grab;
        public void Read(out float moveX, out float moveZ, out bool act, out bool grab) { moveX = Mx; moveZ = Mz; act = Act; grab = Grab; }
    }

    /// <summary>Fake authoritative server: echoes each input back as a snapshot after applying CharacterMotion.</summary>
    internal sealed class FakeNetwork : INetworkService
    {
        public string LocalUserId { get; set; } = "me";
        public long BytesSent { get; private set; }
        public long BytesReceived { get; private set; }
        public event Action<StateSnapshot> StateReceived;
        public readonly List<(int seq, float mx, float mz, bool act, bool grab)> Sent = new List<(int, float, float, bool, bool)>();
        private readonly Queue<StateSnapshot> _incoming = new Queue<StateSnapshot>();

        public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void SendInput(int seq, float moveX, float moveZ, bool act, bool grab) { Sent.Add((seq, moveX, moveZ, act, grab)); BytesSent += 24; }
        public void Enqueue(StateSnapshot s) => _incoming.Enqueue(s);
        public void Poll() { while (_incoming.Count > 0) StateReceived?.Invoke(_incoming.Dequeue()); }
        public void Dispose() { }
    }

    public class CharacterMotionTests
    {
        // These expectations mirror server/test/match.test.js (same constants as server/modules/index.js).
        [Test]
        public void Step_MatchesServerConstants()
        {
            float x = 0, z = 0;
            CharacterMotion.Step(ref x, ref z, 1, 0);
            Assert.AreEqual(0.3f, x, 1e-6f);
            Assert.AreEqual(0f, z, 1e-6f);
        }

        [Test]
        public void Step_NormalisesDiagonalAndClampsToInterior()
        {
            float x = 0, z = 0;
            CharacterMotion.Step(ref x, ref z, 1, 1);
            Assert.AreEqual(0.3f, (float)Math.Sqrt(x * x + z * z), 1e-5f);
            for (int i = 0; i < 500; i++) CharacterMotion.Step(ref x, ref z, 1, 1);
            Assert.AreEqual(CharacterMotion.HalfX, x, 1e-5f);
            Assert.AreEqual(CharacterMotion.HalfZ, z, 1e-5f);
        }
    }

    public class PredictionBufferTests
    {
        [Test]
        public void Reconcile_WithMatchingServer_HasZeroCorrection()
        {
            var p = new PredictionBuffer();
            p.Reset(0, 0);
            for (int i = 0; i < 5; i++) p.Predict(1, 0);
            // Server has processed 3 inputs: x = 0.9
            float correction = p.Reconcile(0.9f, 0f, ackedSeq: 3);
            Assert.AreEqual(0f, correction, 1e-5f);
            Assert.AreEqual(2, p.PendingCount);
            Assert.AreEqual(1.5f, p.X, 1e-5f);
        }

        [Test]
        public void Reconcile_WhenServerDiverges_ReplaysPendingInputsFromServerState()
        {
            var p = new PredictionBuffer();
            p.Reset(0, 0);
            for (int i = 0; i < 5; i++) p.Predict(1, 0);          // client thinks x = 1.5
            float correction = p.Reconcile(0.0f, 0f, ackedSeq: 3); // server pushed us back to 0 (e.g. collision)
            Assert.AreEqual(2, p.PendingCount);
            Assert.AreEqual(0.6f, p.X, 1e-5f);                     // replay of the 2 pending inputs
            Assert.AreEqual(0.9f, correction, 1e-5f);
        }
    }

    public class MovingFrameControllerTests
    {
        private static StateSnapshot Snapshot(int tick, params PlayerState[] players)
            => new StateSnapshot(tick, tick * 0.1, players);

        [Test]
        public void DoesNotSendInputsBeforeFirstSnapshot()
        {
            var net = new FakeNetwork(); var clock = new FakeClock();
            var c = new MovingFrameController(net, new ConstantInput { Mx = 1 }, clock, new MovingFrameModel());
            c.Tick(1.0f);
            Assert.AreEqual(0, net.Sent.Count);
        }

        [Test]
        public void SendsOneInputPerFixedTick_AndPredictsLocally()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput { Mx = 1 }, clock, model);
            net.Enqueue(Snapshot(1, new PlayerState("me", 0, 0, 0)));
            c.Tick(0.0f);                  // first snapshot initialises prediction
            c.Tick(0.35f);                 // 3 whole ticks of 0.1 s
            Assert.AreEqual(3, net.Sent.Count);
            Assert.AreEqual(new[] { 1, 2, 3 }, new[] { net.Sent[0].seq, net.Sent[1].seq, net.Sent[2].seq });
            Assert.AreEqual(0.9f, model.LocalX, 1e-5f);
        }

        [Test]
        public void Act_IsSentOncePerKeyPress_NotWhileHeld()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var input = new ConstantInput();
            var c = new MovingFrameController(net, input, clock, model);
            net.Enqueue(Snapshot(1, new PlayerState("me", 0, 0, 0)));
            c.Tick(0.0f);
            input.Act = true;  c.Tick(0.35f);      // 3 ticks with the key held
            input.Act = false; c.Tick(0.1f);       // released
            input.Act = true;  c.Tick(0.1f);       // pressed again
            Assert.AreEqual(new[] { true, false, false, false, true }, net.Sent.ConvertAll(s => s.act).ToArray());
        }

        [Test]
        public void Grab_IsSentOncePerKeyPress_AndIndependentFromAct()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var input = new ConstantInput();
            var c = new MovingFrameController(net, input, clock, model);
            net.Enqueue(Snapshot(1, new PlayerState("me", 0, 0, 0)));
            c.Tick(0.0f);
            input.Grab = true; c.Tick(0.25f);      // held for 2 ticks
            input.Grab = false; c.Tick(0.1f);
            input.Grab = true; input.Act = true; c.Tick(0.1f);
            Assert.AreEqual(new[] { true, false, false, true }, net.Sent.ConvertAll(s => s.grab).ToArray());
            Assert.AreEqual(new[] { false, false, false, true }, net.Sent.ConvertAll(s => s.act).ToArray());
        }

        [Test]
        public void Cargo_CarriedByLocal_UsesPrediction_OthersUseInterpolatedSnapshots()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput { Mx = 1 }, clock, model);
            var me = new PlayerState("me", 0, 0, 0);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { me }, default, new[]
            {
                new CargoState("mine", 0, 0, false, new[] { "me" }, ""),
                new CargoState("loose", 2, 3, false, new string[0], ""),
            }));
            c.Tick(0f);
            c.Tick(0.35f);                                       // local prediction moves 3 steps: x = 0.9
            Assert.IsTrue(model.TryGetCargoRenderPosition("mine", 0.1, out float x, out float z));
            Assert.AreEqual(0.9f, x, 1e-5f);                     // follows the prediction, not the stale snapshot
            Assert.IsTrue(model.TryGetCargoRenderPosition("loose", 0.1, out x, out z));
            Assert.AreEqual(2f, x); Assert.AreEqual(3f, z);
        }

        [Test]
        public void Cargo_HeavyShared_DrawnAtMidpointOfLocalAndPartner()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0), new PlayerState("pal", 4, 2, 0) }, default,
                new[] { new CargoState("fuel", 2, 1, true, new[] { "me", "pal" }, "") }));
            c.Tick(0f);
            Assert.IsTrue(model.TryGetCargoRenderPosition("fuel", 0.1, out float x, out float z));
            Assert.AreEqual(2f, x, 1e-5f); Assert.AreEqual(1f, z, 1e-5f);
        }

        [Test]
        public void Snapshot_StoresAuthoritativeReactorGauges_AndIgnoresMissingReactorData()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Reactor.Valid);
            var rx = new ReactorState("croisiere", 0.45f, 1.75f, 48f, 312f, 120f, 15f, 1f, 1.15f,
                new[] { 1f, 1f, 1f, 1f }, new[] { true, true }, false, false, false, false, false);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, rx));
            c.Tick(0);
            Assert.IsTrue(model.Reactor.Valid);
            Assert.AreEqual("croisiere", model.Reactor.Regime);
            Assert.AreEqual(312f, model.Reactor.Temp);
            Assert.AreEqual(1.75f, model.Reactor.Noise);
            // a snapshot without reactor data (older server) must not wipe the last known gauges
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0);
            Assert.AreEqual("croisiere", model.Reactor.Regime);
        }

        [Test]
        public void Snapshot_StoresScramLeverAndBoatDepth_AndKeepsLastValuesWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Lever.Valid); Assert.IsFalse(model.Boat.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default,
                new ScramLeverState(true, false), new BoatDepthState(0f, 0f)));
            c.Tick(0);
            Assert.IsTrue(model.Lever.CoverOpen); Assert.IsFalse(model.Lever.Pulled);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default,
                new ScramLeverState(true, true), new BoatDepthState(2.5f, 0.2f)));
            c.Tick(0);
            Assert.IsTrue(model.Lever.Pulled);
            Assert.AreEqual(2.5f, model.Boat.Depth, 1e-5f);
            Assert.AreEqual(0.2f, model.Boat.DescentRate, 1e-5f);
            // an older server (no lever/boat data) must not wipe what is already known
            net.Enqueue(new StateSnapshot(3, 0.3, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0);
            Assert.IsTrue(model.Lever.Pulled);
            Assert.AreEqual(2.5f, model.Boat.Depth, 1e-5f);
        }

        [Test]
        public void Snapshot_StoresAuthoritativeInterlockState_WithoutPredicting()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            net.Enqueue(new StateSnapshot(7, 0.7, new[] { new PlayerState("me", 0, 0, 0) },
                new InterlockState(12, 0, "me", "", "none", 0, 0)));
            c.Tick(0);
            Assert.AreEqual(12, model.Interlock.RemainingA);
            Assert.AreEqual("me", model.Interlock.HolderA);
            net.Enqueue(new StateSnapshot(8, 0.8, new[] { new PlayerState("me", 0, 0, 0) },
                new InterlockState(0, 0, "", "", "success", 8, 1)));
            c.Tick(0);
            Assert.AreEqual("success", model.Interlock.Result);
            Assert.AreEqual(1, model.Interlock.Count);
        }

        [Test]
        public void Snapshot_UpdatesRemotePlayersAndServerClock()
        {
            var net = new FakeNetwork(); var clock = new FakeClock { Now = 10.0 }; var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            net.Enqueue(Snapshot(5, new PlayerState("me", 0, 0, 0), new PlayerState("other", 2, 3, 0)));
            c.Tick(0);
            Assert.IsTrue(model.HasServerTime);
            Assert.AreEqual(0.5, model.EstimateServerTime(10.0), 1e-9);
            Assert.AreEqual(1.0, model.EstimateServerTime(10.5), 1e-9);
            Assert.IsTrue(model.TrySampleRemote("other", 0.5, out float x, out float z));
            Assert.AreEqual(2f, x); Assert.AreEqual(3f, z);
        }

        [Test]
        public void SimulatedLatency_DelaysAndDropsMessages()
        {
            var clock = new FakeClock { Now = 0 };
            var inner = new FakeNetwork();
            var net = new SimulatedLatencyNetworkService(inner, clock, oneWayLatencySeconds: 0.1);
            net.SendInput(1, 1, 0, false, false);
            net.Poll();
            Assert.AreEqual(0, inner.Sent.Count);        // still in flight
            clock.Now = 0.11; net.Poll();
            Assert.AreEqual(1, inner.Sent.Count);

            int received = 0; net.StateReceived += _ => received++;
            inner.Enqueue(Snapshot(1, new PlayerState("me", 0, 0, 0)));
            net.Poll();                                    // inner delivers -> scheduled at 0.21
            Assert.AreEqual(0, received);
            clock.Now = 0.25; net.Poll();
            Assert.AreEqual(1, received);

        }

        [Test]
        public void SimulatedLoss_RetransmitsLateAndKeepsOrder()
        {
            // TCP semantics: a "lost" message is delayed by the retransmit timeout and blocks the ones behind it.
            var clock = new FakeClock { Now = 0 };
            var inner = new FakeNetwork();
            var net = new SimulatedLatencyNetworkService(inner, clock, 0.0, lossRate: 1.0, retransmitDelaySeconds: 0.2);
            net.SendInput(1, 1, 0, false, false);
            net.SendInput(2, 1, 0, false, false);
            clock.Now = 0.1; net.Poll();
            Assert.AreEqual(0, inner.Sent.Count);                       // still waiting for the retransmission
            clock.Now = 0.25; net.Poll();
            Assert.AreEqual(new[] { 1, 2 }, new[] { inner.Sent[0].seq, inner.Sent[1].seq }); // delivered, in order
        }
    }
}
