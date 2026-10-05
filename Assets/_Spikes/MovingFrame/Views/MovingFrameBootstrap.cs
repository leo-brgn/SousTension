using System;
using System.Threading;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Composition root of the spike (the only place that wires services, model, controller and views).
    /// Controls: WASD move, mouse look, E = press the interlock key (two players, one at each end, within 3 s), F = grab/drop cargo (the heavy flask needs two players within 3 s),
    /// E at the RK-1 panel (left wall, compartment 4) = turn the regime selector one position (Veille, Croisiere, Pleine, back to Veille).
    /// Add this component to an empty GameObject in an empty scene and press Play (Nakama must be running: server/docker-compose.yml).
    ///
    /// Command-line / environment options (for several instances on one machine):
    ///   --device=NAME        device id used to authenticate (default: unique per machine + random suffix)
    ///   --latency=MS         simulated one-way latency (default 0)
    ///   --jitter=MS          simulated jitter (default 0)
    ///   --loss=PERCENT       simulated TCP loss: affected messages are delayed by 200 ms and block those behind (default 0)
    ///   --host=HOST --port=N Nakama endpoint (default 127.0.0.1:7350)
    /// </summary>
    public sealed class MovingFrameBootstrap : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private float simulatedLatencyMs = 0f;
        [SerializeField] private float simulatedJitterMs = 0f;
        [SerializeField] private float simulatedLossPercent = 0f;

        private MovingFrameController _controller;
        private INetworkService _network;
        private MetricsHudView _hud;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private async void Start()
        {
            ParseArgs(out string device, out float latency, out float jitter, out float loss, out string h, out int p);
            if (latency >= 0) simulatedLatencyMs = latency;
            if (jitter >= 0) simulatedJitterMs = jitter;
            if (loss >= 0) simulatedLossPercent = loss;
            if (!string.IsNullOrEmpty(h)) host = h;
            if (p > 0) port = p;
            device = string.IsNullOrEmpty(device) ? SystemInfo.deviceUniqueIdentifier + "-" + UnityEngine.Random.Range(1000, 9999) : device;
            if (device.Length < 10) device = device.PadRight(10, '0'); // Nakama requires 10-128 chars

            // Services
            var clock = new UnityClockService();
            INetworkService network = new NakamaNetworkService(device, host, port);
            if (simulatedLatencyMs > 0 || simulatedLossPercent > 0)
                network = new SimulatedLatencyNetworkService(network, clock, simulatedLatencyMs / 1000.0, simulatedJitterMs / 1000.0, simulatedLossPercent / 100.0);
            var input = new KeyboardInputSource();
            _network = network;

            // Model + Controller
            var model = new MovingFrameModel();
            _controller = new MovingFrameController(network, input, clock, model);

            // Views
            var boat = BuildBoat();
            boat.Bind(model, clock, new BoatMotion());
            var cam = BuildCamera();
            var characters = new GameObject("Characters").AddComponent<CharactersView>();
            characters.Bind(model, clock, input, boat.transform, cam);
            new GameObject("Stations").AddComponent<StationsView>().Bind(model, boat.transform);
            new GameObject("Cargo").AddComponent<CargoView>().Bind(model, clock, boat.transform);
            new GameObject("ReactorPanel").AddComponent<ReactorPanelView>().Bind(model, boat.transform);
            new GameObject("ScramLever").AddComponent<ScramLeverView>().Bind(model, boat.transform);
            new GameObject("PrimaryCircuit").AddComponent<PrimaryCircuitView>().Bind(model, boat.transform);
            new GameObject("Water").AddComponent<WaterView>().Bind(model, boat.transform);
            new GameObject("Leaks").AddComponent<LeaksView>().Bind(model, boat.transform);
            new GameObject("BilgePumps").AddComponent<BilgePumpsView>().Bind(model, boat.transform);
            new GameObject("BreakerPanel").AddComponent<BreakerPanelView>().Bind(model, boat.transform);
            new GameObject("Telegraph").AddComponent<TelegraphView>().Bind(model, boat.transform);
            new GameObject("GridLighting").AddComponent<GridLightingView>().Bind(model);
            _hud = new GameObject("MetricsHud").AddComponent<MetricsHudView>();
            _hud.Bind(model, network, _controller, clock);
            Cursor.lockState = CursorLockMode.Locked;

            try
            {
                await network.ConnectAsync(_cts.Token);
                _hud.SetStatus($"connected as {network.LocalUserId?.Substring(0, 6)} | sim {simulatedLatencyMs:F0} ms, {simulatedLossPercent:F0} % loss");
            }
            catch (Exception e)
            {
                _hud.SetStatus("connection failed: " + e.Message);
                Debug.LogError(e);
            }
        }

        private void Update() => _controller?.Tick(Time.deltaTime);

        private void OnDestroy()
        {
            _cts.Cancel();
            _controller?.Dispose();
            _network?.Dispose();
        }

        private static BoatView BuildBoat()
        {
            var root = new GameObject("Boat");
            Piece(root.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(2 * CharacterMotion.HalfX + 0.4f, 0.2f, 2 * CharacterMotion.HalfZ + 0.4f), new Color(0.35f, 0.4f, 0.35f));
            Piece(root.transform, "WallL", new Vector3(-CharacterMotion.HalfX - 0.2f, 1.25f, 0), new Vector3(0.2f, 2.5f, 2 * CharacterMotion.HalfZ + 0.4f), new Color(0.45f, 0.55f, 0.45f));
            Piece(root.transform, "WallR", new Vector3(CharacterMotion.HalfX + 0.2f, 1.25f, 0), new Vector3(0.2f, 2.5f, 2 * CharacterMotion.HalfZ + 0.4f), new Color(0.45f, 0.55f, 0.45f));
            Piece(root.transform, "WallBow", new Vector3(0, 1.25f, CharacterMotion.HalfZ + 0.2f), new Vector3(2 * CharacterMotion.HalfX + 0.4f, 2.5f, 0.2f), new Color(0.6f, 0.45f, 0.35f));
            Piece(root.transform, "WallStern", new Vector3(0, 1.25f, -CharacterMotion.HalfZ - 0.2f), new Vector3(2 * CharacterMotion.HalfX + 0.4f, 2.5f, 0.2f), new Color(0.75f, 0.2f, 0.2f));
            // Fixed reference cubes inside the boat: they must not slide relative to the floor.
            for (int i = -2; i <= 2; i++) Piece(root.transform, "Marker" + i, new Vector3(i * 1.2f, 0.3f, i * 4f), Vector3.one * 0.6f, Color.HSVToRGB((i + 2) / 5f, 0.7f, 0.9f));
            // A static "world" reference outside the boat to feel the motion.
            var world = GameObject.CreatePrimitive(PrimitiveType.Plane);
            world.name = "WorldGround"; world.transform.position = new Vector3(0, -6f, 0); world.transform.localScale = Vector3.one * 10f;
            world.GetComponent<Renderer>().material.color = new Color(0.15f, 0.25f, 0.5f);
            return root.AddComponent<BoatView>();
        }

        private static void Piece(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
        }

        private static Camera BuildCamera()
        {
            var existing = Camera.main;
            var cam = existing != null ? existing : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera"; cam.nearClipPlane = 0.05f;
            return cam;
        }

        private static void ParseArgs(out string device, out float latency, out float jitter, out float loss, out string host, out int port)
        {
            device = null; host = null; latency = jitter = loss = -1f; port = 0;
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (a.StartsWith("--device=")) device = a.Substring(9);
                else if (a.StartsWith("--latency=")) float.TryParse(a.Substring(10), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out latency);
                else if (a.StartsWith("--jitter=")) float.TryParse(a.Substring(9), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out jitter);
                else if (a.StartsWith("--loss=")) float.TryParse(a.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out loss);
                else if (a.StartsWith("--host=")) host = a.Substring(7);
                else if (a.StartsWith("--port=")) int.TryParse(a.Substring(7), out port);
            }
        }
    }
}
