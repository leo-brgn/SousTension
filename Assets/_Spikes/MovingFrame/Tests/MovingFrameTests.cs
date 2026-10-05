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

    internal sealed class FakeUse : IUseInput { public bool UseHeld { get; set; } }
    internal sealed class FakeAim : IAimSource { public string TargetId { get; set; } }
    internal sealed class FakeHands : IHandsInput { public bool TakeHeld { get; set; } public bool DropHeld { get; set; } public bool StowHeld { get; set; } public bool ThrowHeld { get; set; } public bool NextHeld { get; set; } public bool PrevHeld { get; set; } }
    internal sealed class FakeLook : ILookSource { public float Yaw { get; set; } }

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
        public readonly List<(int seq, float mx, float mz, bool act, bool grab, bool hold, string use, string hand, float yaw)> Sent = new List<(int, float, float, bool, bool, bool, string, string, float)>();
        private readonly Queue<StateSnapshot> _incoming = new Queue<StateSnapshot>();

        public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
        public void SendInput(int seq, float moveX, float moveZ, bool act, bool grab, bool hold = false, string use = null, string hand = null, float yaw = 0f) { Sent.Add((seq, moveX, moveZ, act, grab, hold, use, hand, yaw)); BytesSent += 24; }
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
        public void HeldInteractionKey_IsSentAsHoldEveryTick_WhileActIsOnlyTheFirstPress()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var input = new ConstantInput { Act = true };
            var c = new MovingFrameController(net, input, clock, model);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0);
            net.Sent.Clear();
            clock.Now += 0.3; c.Tick(0.3f);
            Assert.GreaterOrEqual(net.Sent.Count, 3);
            foreach (var s in net.Sent) Assert.IsTrue(s.hold, "the key is still down: hold every tick");
            Assert.AreEqual(1, net.Sent.FindAll(s => s.act).Count, "act is the press edge only");
        }

        [Test]
        public void Snapshot_StoresPumpStates_IncludingBroken()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            var rx = new ReactorState("veille", 0.1f, 0f, 10f, 291f, 30f, 4f, 0.6f, 0.6f,
                new[] { 1f, 0.5f, 0.25f, 0f }, new[] { true, false }, false, false, false, false, false, new[] { false, true });
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, rx));
            c.Tick(0);
            Assert.IsTrue(model.Reactor.Pumps[0]); Assert.IsFalse(model.Reactor.Pumps[1]);
            Assert.IsFalse(model.Reactor.PumpBroken[0]); Assert.IsTrue(model.Reactor.PumpBroken[1]);
            Assert.AreEqual(0.25f, model.Reactor.Valves[2], 1e-5f);
        }

        [Test]
        public void Snapshot_StoresEveryCoupledAction_ById_AndKeepsLastWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.TryGetCoupled("demo2", out _));
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, new[]
            {
                new CoupledActionState("demo", new InterlockState(0, 0, "", "", "none", 0, 0)),
                new CoupledActionState("demo2", new InterlockState(12, 0, "pal", "", "none", 0, 3)),
            }));
            c.Tick(0);
            Assert.IsTrue(model.TryGetCoupled("demo2", out var s));
            Assert.AreEqual(12, s.RemainingA); Assert.AreEqual("pal", s.HolderA); Assert.AreEqual(3, s.Count);
            Assert.IsTrue(model.TryGetCoupled("demo", out var d)); Assert.AreEqual(0, d.Count);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));   // older server: nothing wiped
            c.Tick(0);
            Assert.IsTrue(model.TryGetCoupled("demo2", out s)); Assert.AreEqual(12, s.RemainingA);
        }

        [Test]
        public void Snapshot_StoresRestartProcedure_AndKeepsLastValuesWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Restart.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null,
                new RestartState(true, false, true, "refused", 7, 0)));
            c.Tick(0);
            Assert.IsTrue(model.Restart.LeverBack); Assert.IsFalse(model.Restart.ValvesOpen); Assert.IsTrue(model.Restart.PumpsRunning);
            Assert.AreEqual("refused", model.Restart.Last); Assert.AreEqual(7, model.Restart.LastTick);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0);
            Assert.AreEqual("refused", model.Restart.Last);   // an older server must not wipe it
        }

        [Test]
        public void Snapshot_StoresWater_AndKeepsLastValuesWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Water.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default,
                new WaterState(new[] { 0.5f, 0.1f, 0f, 0f, 0f, 0f }, 22f, 3.2f, -1.5f, new[] { true, true, false, true, true })));
            c.Tick(0);
            Assert.AreEqual(0.5f, model.Water.Fill[0], 1e-5f); Assert.AreEqual(3.2f, model.Water.TrimDeg, 1e-5f);
            Assert.AreEqual(-1.5f, model.Water.ListDeg, 1e-5f); Assert.IsFalse(model.Water.DoorOpen[2]);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));   // older server: nothing wiped
            c.Tick(0);
            Assert.AreEqual(22f, model.Water.MassTonnes, 1e-5f);
        }

        [Test]
        public void Snapshot_StoresLeaks_AndKeepsLastListWhenTheServerSendsNone()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.AreEqual(0, model.Leaks.Length);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default,
                new[] { new LeakState(7, 3, 3f, -1.5f, 2, "plate") }));
            c.Tick(0);
            Assert.AreEqual(1, model.Leaks.Length);
            Assert.AreEqual(7, model.Leaks[0].Id); Assert.AreEqual(2, model.Leaks[0].Size); Assert.AreEqual("plate", model.Leaks[0].Type);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));                   // older server: list kept
            c.Tick(0);
            Assert.AreEqual(1, model.Leaks.Length);
            net.Enqueue(new StateSnapshot(3, 0.3, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default,
                new LeakState[0]));                                                                                // sealed: the list is now empty
            c.Tick(0);
            Assert.AreEqual(0, model.Leaks.Length);
        }

        [Test]
        public void CargoState_CarriesTheKindAndWhetherAUsedPatchIsAway()
        {
            var crate = new CargoState("crate1", 0, 0, false, null, "");
            Assert.AreEqual("crate", crate.Kind); Assert.IsTrue(crate.Active);
            var patch = new CargoState("patch1", 0, 0, false, null, "", "patch", false);
            Assert.AreEqual("patch", patch.Kind); Assert.IsFalse(patch.Active);
        }

        [Test]
        public void Snapshot_StoresBilgePumps_AndKeepsLastStateWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Bilge.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default, null,
                new BilgeState(new[] { 1, 2 }, new[] { true, false })));
            c.Tick(0);
            Assert.AreEqual(1, model.Bilge.State[0]); Assert.AreEqual(2, model.Bilge.State[1]);
            Assert.IsTrue(model.Bilge.Running[0]); Assert.IsFalse(model.Bilge.Running[1]);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));                   // older server
            c.Tick(0);
            Assert.AreEqual(2, model.Bilge.State[1]);
        }

        [Test]
        public void Snapshot_StoresTheElectricalGrid_AndKeepsLastStateWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Grid.Valid);
            var breakers = new int[20]; for (int i = 0; i < 20; i++) breakers[i] = 1; breakers[8] = 2; breakers[3] = 0;
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default, null, default,
                new GridState(0.35f, 0.8f, false, breakers, new[] { 1, 1, 1, 0, 1, 1 }, 5.35f, 2f)));
            c.Tick(0);
            Assert.AreEqual(0.35f, model.Grid.Voltage, 1e-5f); Assert.AreEqual(0.8f, model.Grid.Battery, 1e-5f);
            Assert.AreEqual(2, model.Grid.Breakers[8]); Assert.AreEqual(0, model.Grid.Breakers[3]); Assert.AreEqual(0, model.Grid.LightBands[3]);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));                   // older server
            c.Tick(0);
            Assert.AreEqual(0.35f, model.Grid.Voltage, 1e-5f);
        }

        [Test]
        public void Snapshot_StoresPropulsion_AndKeepsLastStateWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Propulsion.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default, null, default, default,
                new PropulsionState(3, 2.4f, 120.5f, 1.6f)));
            c.Tick(0);
            Assert.AreEqual(3, model.Propulsion.Telegraph); Assert.AreEqual(2.4f, model.Propulsion.Speed, 1e-5f);
            Assert.AreEqual(120.5f, model.Propulsion.Distance, 1e-4f); Assert.AreEqual(1.6f, model.Propulsion.Noise, 1e-5f);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));                   // older server
            c.Tick(0);
            Assert.AreEqual(3, model.Propulsion.Telegraph);
        }

        // ---- Aimed interaction (E2-02) ----
        private static (MovingFrameController c, FakeNetwork net, FakeClock clock, MovingFrameModel model, FakeUse use, FakeAim aim) AimSetup(CargoState[] cargo = null)
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel(); var use = new FakeUse(); var aim = new FakeAim();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model, use, aim);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, cargo));
            c.Tick(0);
            net.Sent.Clear();
            return (c, net, clock, model, use, aim);
        }

        [Test]
        public void Click_OnAnAimedObject_SendsActOnThePressThenHoldWhileHeld_WithTheTargetId()
        {
            var s = AimSetup();
            s.aim.TargetId = "valve2"; s.use.UseHeld = true;
            s.c.Tick(0.3f);
            Assert.GreaterOrEqual(s.net.Sent.Count, 3);
            foreach (var sent in s.net.Sent) { Assert.AreEqual("valve2", sent.use); Assert.IsTrue(sent.hold, "held button = hold"); }
            Assert.AreEqual(1, s.net.Sent.FindAll(x => x.act).Count, "act is the press edge only");
            Assert.IsTrue(s.net.Sent[0].act);
        }

        [Test]
        public void Click_WithNothingAimedAndNothingInHand_SendsNothing_AndNoButtonSendsNoTarget()
        {
            var s = AimSetup();
            s.use.UseHeld = true; s.aim.TargetId = null;
            s.c.Tick(0.3f);
            foreach (var sent in s.net.Sent) { Assert.IsNull(sent.use); Assert.IsFalse(sent.act); Assert.IsFalse(sent.hold); }
            s.net.Sent.Clear();
            s.use.UseHeld = false; s.aim.TargetId = "regime";
            s.c.Tick(0.3f);
            foreach (var sent in s.net.Sent) { Assert.IsNull(sent.use, "looking at an object without clicking does not send it"); Assert.IsFalse(sent.act); }
        }

        [Test]
        public void Click_WithAPatchOrTheBucketInHandAndNothingAimed_UsesTheItem()
        {
            var cargo = new[] { new CargoState("patch1", 0, 0, false, new[] { "me" }, "", "patch", true) };
            var s = AimSetup(cargo);
            s.model.LocalId = "me";
            Assert.IsTrue(s.model.IsLocalHoldingUsableItem());
            s.use.UseHeld = true; s.aim.TargetId = null;
            s.c.Tick(0.3f);
            Assert.GreaterOrEqual(s.net.Sent.Count, 3);
            Assert.AreEqual("item", s.net.Sent[0].use); Assert.IsTrue(s.net.Sent[0].act);
            // aiming at something wins over "item"
            s.net.Sent.Clear(); s.aim.TargetId = "leak:4";
            s.c.Tick(0.3f);
            Assert.AreEqual("leak:4", s.net.Sent[0].use);
        }

        [Test]
        public void ACrateInHand_IsNotAUsableItem()
        {
            var s = AimSetup(new[] { new CargoState("crate1", 0, 0, false, new[] { "me" }, "") });
            s.model.LocalId = "me";
            Assert.IsFalse(s.model.IsLocalHoldingUsableItem());
        }

        [Test]
        public void TheInteractionKeyAlone_KeepsTheOldPositionBasedBehaviour_NoTarget()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput { Act = true }, clock, model, new FakeUse(), new FakeAim { TargetId = "regime" });
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0); net.Sent.Clear();
            c.Tick(0.3f);
            Assert.GreaterOrEqual(net.Sent.Count, 3);
            foreach (var sent in net.Sent) { Assert.IsNull(sent.use); Assert.IsTrue(sent.hold); }
            Assert.AreEqual(1, net.Sent.FindAll(x => x.act).Count);
        }

        // ---- Hands (E2-03) ----
        [Test]
        public void HandKeys_SendOneCommandOnThePress_DropBeforeStowBeforeTake()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel(); var hands = new FakeHands();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model, null, null, hands);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0); net.Sent.Clear();
            hands.TakeHeld = true;
            c.Tick(0.3f);
            Assert.AreEqual("take", net.Sent[0].hand);
            for (int i = 1; i < net.Sent.Count; i++) Assert.IsNull(net.Sent[i].hand, "a held key sends the command once");
            net.Sent.Clear();
            hands.TakeHeld = false; hands.DropHeld = true; hands.StowHeld = true;
            c.Tick(0.1f);
            Assert.AreEqual("drop", net.Sent[0].hand, "drop wins over stow");
            net.Sent.Clear();
            hands.DropHeld = false;
            c.Tick(0.1f);
            Assert.IsNull(net.Sent[0].hand, "stow was already held");
            net.Sent.Clear();
            hands.StowHeld = false; c.Tick(0.1f); net.Sent.Clear();
            hands.StowHeld = true; c.Tick(0.1f);
            Assert.AreEqual("stow", net.Sent[0].hand);
        }

        // ---- Mass, speed and throws (E2-04) ----
        [Test]
        public void CarryLoad_MatchesTheServerRule_AndTheCharacterWalksSlowerWithAFactor()
        {
            Assert.AreEqual(1f, CarryLoad.SpeedFactor(0f), 1e-6f);
            Assert.AreEqual(1f - 0.012f * 12f, CarryLoad.SpeedFactor(CarryLoad.ItemMass("crate")), 1e-6f);
            Assert.AreEqual(1f - 0.012f * 3f, CarryLoad.SpeedFactor(CarryLoad.ItemMass("patch") + CarryLoad.ItemMass("bucket")), 1e-6f);
            Assert.AreEqual(1f - 0.012f * 20f, CarryLoad.SpeedFactor(CarryLoad.ItemMass("fuel") / 2f), 1e-6f);   // the flask is shared by two
            Assert.AreEqual(0.5f, CarryLoad.SpeedFactor(500f), 1e-6f);
            float x = 0, z = 0, x2 = 0, z2 = 0;
            CharacterMotion.Step(ref x, ref z, 1, 0);
            CharacterMotion.Step(ref x2, ref z2, 1, 0, 0.5f);
            Assert.AreEqual(x * 0.5f, x2, 1e-6f);
        }

        [Test]
        public void Prediction_ReplaysEachPendingInputWithTheFactorItWasPredictedWith()
        {
            var p = new PredictionBuffer();
            p.Reset(0, 0);
            for (int i = 0; i < 3; i++) p.Predict(1, 0, 1f);
            for (int i = 0; i < 3; i++) p.Predict(1, 0, 0.5f);
            float predicted = p.X;
            // the server processed the first 3 inputs (full speed) and says x = 0.9
            float correction = p.Reconcile(0.9f, 0f, 3);
            Assert.AreEqual(predicted, p.X, 1e-5f, "replaying the 3 slowed inputs from the acknowledged position gives the same place");
            Assert.AreEqual(0f, correction, 1e-5f);
        }

        [Test]
        public void TheLocalCarryFactor_FollowsWhatTheLocalPlayerHolds()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            model.LocalId = "me";
            Assert.AreEqual(1f, model.LocalCarryFactor(), 1e-6f);
            var cargo = new[]
            {
                new CargoState("crate1", 0, 0, false, new[] { "me" }, "", "crate"),
                new CargoState("fuel", 0, 0, true, new[] { "me", "pal" }, "", "fuel"),
                new CargoState("patch1", 0, 0, false, new[] { "me" }, "", "patch"),
                new CargoState("bucket", 0, 0, false, new[] { "me" }, "", "bucket"),
            };
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0, new[] { "crate1", "crate1", "" }) }, default, cargo));
            c.Tick(0);
            Assert.AreEqual(1f - 0.012f * 12f, model.LocalCarryFactor(), 1e-6f, "a two-handed crate counts once");
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0, new[] { "fuel", "fuel", "" }) }, default, cargo));
            c.Tick(0);
            Assert.AreEqual(1f - 0.012f * 20f, model.LocalCarryFactor(), 1e-6f, "half of the shared flask");
            net.Enqueue(new StateSnapshot(3, 0.3, new[] { new PlayerState("me", 0, 0, 0, new[] { "patch1", "bucket", "" }) }, default, cargo));
            c.Tick(0);
            Assert.AreEqual(1f - 0.012f * 3f, model.LocalCarryFactor(), 1e-6f);
        }

        [Test]
        public void RightClick_SendsTheThrowOnThePress_AndEveryInputCarriesTheHeading()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel(); var hands = new FakeHands(); var look = new FakeLook { Yaw = 1.5f };
            var c = new MovingFrameController(net, new ConstantInput(), clock, model, null, null, hands, look);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0); net.Sent.Clear();
            hands.ThrowHeld = true;
            c.Tick(0.3f);
            Assert.AreEqual("throw", net.Sent[0].hand);
            for (int i = 1; i < net.Sent.Count; i++) Assert.IsNull(net.Sent[i].hand, "one throw per press");
            foreach (var sent in net.Sent) Assert.AreEqual(1.5f, sent.yaw, 1e-6f);
        }

        // ---- The Operating Manual (E5-02) ----
        [Test]
        public void Snapshot_StoresTheManualPage_AndKeepsLastPageWhenMissing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            Assert.IsFalse(model.Manual.Valid);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }, default, null, default, default, default, null, default, default, null, default, default, default,
                new ManualState(3, 6)));
            c.Tick(0);
            Assert.AreEqual(3, model.Manual.Page); Assert.AreEqual(6, model.Manual.PageCount);
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0);
            Assert.AreEqual(3, model.Manual.Page);
        }

        [Test]
        public void TheLocalPlayerIsReading_OnlyWhileHoldingTheManual()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            model.LocalId = "me";
            var cargo = new[] { new CargoState("manual", 0, 0, false, new[] { "me" }, "", "manual"), new CargoState("bucket", 0, 0, false, new[] { "me" }, "", "bucket") };
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0, new[] { "manual", "manual", "" }) }, default, cargo));
            c.Tick(0);
            Assert.IsTrue(model.IsLocalReading());
            Assert.AreEqual(1f - 0.012f * 8f, model.LocalCarryFactor(), 1e-6f, "the binder weighs 8 kg");
            net.Enqueue(new StateSnapshot(2, 0.2, new[] { new PlayerState("me", 0, 0, 0, new[] { "bucket", "", "" }) }, default, cargo));
            c.Tick(0);
            Assert.IsFalse(model.IsLocalReading());
        }

        [Test]
        public void TheArrowKeys_SendNextAndPrevOnThePress()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel(); var hands = new FakeHands();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model, null, null, hands);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0) }));
            c.Tick(0); net.Sent.Clear();
            hands.NextHeld = true;
            c.Tick(0.3f);
            Assert.AreEqual("next", net.Sent[0].hand);
            for (int i = 1; i < net.Sent.Count; i++) Assert.IsNull(net.Sent[i].hand);
            net.Sent.Clear();
            hands.NextHeld = false; c.Tick(0.1f); net.Sent.Clear();
            hands.PrevHeld = true; c.Tick(0.1f);
            Assert.AreEqual("prev", net.Sent[0].hand);
        }

        [Test]
        public void ManualPages_EveryPageHasATextForEachStep_AndAtMostFiveSteps()
        {
            Assert.AreEqual("index", ManualView.Pages[0].id);
            foreach (var (id, steps) in ManualView.Pages)
            {
                Assert.LessOrEqual(steps, 5, id);
                Assert.AreNotEqual("manual." + id + ".title", ManualView.Text("manual." + id + ".title"), id + ": title is missing");
                for (int k = 1; k <= steps; k++) Assert.AreNotEqual("manual." + id + ".step" + k, ManualView.Text("manual." + id + ".step" + k), id + " step " + k);
            }
            StringAssert.Contains("Sommaire", ManualView.PageText(0));
            StringAssert.Contains("Redémarrage", ManualView.PageText(0), "the contents lists the procedures");
            StringAssert.Contains("1 / 6", ManualView.PageText(0));
        }

        [Test]
        public void CargoState_CarriesItsHeight()
        {
            Assert.AreEqual(0f, new CargoState("c", 0, 0, false, null, "").Y);
            Assert.AreEqual(1.4f, new CargoState("c", 0, 0, false, null, "", "bucket", true, 1.4f).Y, 1e-6f);
        }

        [Test]
        public void Snapshot_CarriesWhatEachPlayerHolds_AndUnknownPlayersHoldNothing()
        {
            var net = new FakeNetwork(); var clock = new FakeClock(); var model = new MovingFrameModel();
            var c = new MovingFrameController(net, new ConstantInput(), clock, model);
            net.Enqueue(new StateSnapshot(1, 0.1, new[] { new PlayerState("me", 0, 0, 0, new[] { "patch1", "", "flashlight" }), new PlayerState("pal", 1, 1, 0, new[] { "crate1", "crate1", "" }) }));
            c.Tick(0);
            model.LocalId = "me";
            CollectionAssert.AreEqual(new[] { "patch1", "", "flashlight" }, model.HandsOf("me"));
            CollectionAssert.AreEqual(new[] { "crate1", "crate1", "" }, model.HandsOf("pal"));
            CollectionAssert.AreEqual(new[] { "", "", "" }, model.HandsOf("nobody"));
            CollectionAssert.AreEqual(new[] { "", "", "" }, new PlayerState("x", 0, 0, 0).Hands);
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
