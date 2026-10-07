using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Day;
using BuffetSim.Debugging;
using BuffetSim.Economy;
using BuffetSim.Events;
using BuffetSim.Food;
using BuffetSim.Gambling;
using BuffetSim.Models;
using BuffetSim.Player;
using BuffetSim.Stations;
using BuffetSim.Tables;
using BuffetSim.ToGo;
using BuffetSim.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace BuffetSim.Bootstrap
{
    /// <summary>
    /// Assembles the whole demo at runtime out of primitives: the room, buffet line, tables,
    /// kitchen stations, NavMesh, player and the simulation systems. The scene file only holds this
    /// one component, so the level is reproducible from code until real art replaces it.
    /// </summary>
    public sealed class DemoSceneBuilder : MonoBehaviour
    {
        [Header("Data (runtime defaults are created when empty)")]
        [SerializeField] private EconomyConfig economyConfig;
        [SerializeField] private FoodCatalog foodCatalog;
        [Tooltip("Optional: the chaos events that can fire; a runtime default with the four demo events is used when empty.")]
        [SerializeField] private ChaosEventCatalog chaosCatalog;
        [Tooltip("Optional: the forty fortunes; a runtime default is used when empty.")]
        [SerializeField] private FortuneCatalog fortuneCatalog;
        [Tooltip("Optional lit material used as the base for every placeholder colour; assign one so builds include the shader.")]
        [SerializeField] private Material baseMaterial;
        [Tooltip("0 = random every run.")]
        [SerializeField] private int randomSeed;

        // Room: x from -15..15, dining z from -10..13, sidewalk z from -13..-10.
        private const float WallHeight = 3.2f;
        private const float WallThickness = 0.3f;
        private const float FrontWallZ = -10f;
        private const float BackWallZ = 13f;
        private const float SideWallX = 15f;
        private const float DoorMinX = 3f;
        private const float DoorMaxX = 5f;
        private const float BuffetZ = 4f;
        private const float BuffetSlotSpacing = 2.5f;
        private const float CounterHeight = 0.9f;
        // In the Meshy "Buffet Food Station" model the counter surface sits at about 48% of the
        // model's total height; the rest is the sneeze guard.
        private const float StationCounterFraction = 0.48f;

        private const string StationModel = "BuffetFoodStation";
        private const string TrayModel = "EmptyBuffetTray";
        private const string FryerModel = "FryerBodyBasin";
        private const string FryerBasketModel = "FryerBasket";
        private const string StoveModel = "StoveBase";
        private const string StoveKnobModel = "StoveKnob";

        private static readonly Vector3 RegisterPoint = new Vector3(-3f, 0f, -7.3f);
        private static readonly Vector3 QueueStart = new Vector3(-1.3f, 0f, -7.6f);
        private static readonly Vector3 QueueDirection = new Vector3(1f, 0f, -0.35f);
        private static readonly Vector3 SpawnPoint = new Vector3(5.5f, 0f, -12f);
        private static readonly Vector3 ExitPoint = new Vector3(2.5f, 0f, -12f);
        private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0.1f, -4f);
        private static readonly Vector3 WindowCenter = new Vector3(-9f, 1.6f, FrontWallZ);
        private static readonly Vector3 WallHoleCenter = new Vector3(-SideWallX, 1f, -2.5f);
        private static readonly Vector3 RestroomDoor = new Vector3(-SideWallX, 1.05f, -7f);
        private static readonly Vector3 DishwasherPosition = new Vector3(12f, 0f, 9.5f);
        private static readonly Vector3 SlotMachinePosition = new Vector3(-1f, 0f, -9.4f);
        private static readonly Vector3 FortuneWallPosition = new Vector3(-SideWallX + 0.17f, 1.5f, -5.2f);
        private static readonly Vector3 FountainPosition = new Vector3(-7.5f, 0f, -7.6f);
        private static readonly int[] DrainTables = { 1, 3, 5 };

        private static readonly Vector2[] TablePositions =
        {
            new Vector2(-9f, -0.5f), new Vector2(-4.5f, -0.5f), new Vector2(0f, -0.5f), new Vector2(4.5f, -0.5f), new Vector2(9f, -0.5f),
            new Vector2(-9f, -4.5f), new Vector2(9f, -4.5f),
        };

        private Font _font;
        private System.Random _rng;
        private BuffetFloor _floor;
        private CustomerQueue _queue;
        private BreakablePanel _window;
        private BreakablePanel _wallHole;
        private readonly System.Collections.Generic.List<Vector3> _drainPoints = new System.Collections.Generic.List<Vector3>();

        private Material _wood;
        private Material _steel;
        private Material _darkSteel;
        private Material _wall;
        private Material _floorMat;

        public EconomyConfig EconomyConfig => economyConfig;
        public FoodCatalog FoodCatalog => foodCatalog;

        private void Awake()
        {
            GameEvents.ClearAll();
            MaterialLibrary.Initialize(baseMaterial);
            PropLibrary.Clear();

            if (economyConfig == null) economyConfig = EconomyConfig.CreateDefault();
            if (foodCatalog == null) foodCatalog = FoodCatalog.CreateDefault();
            _font = PrimitiveFactory.DefaultFont();
            _rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

            _wood = MaterialLibrary.Get(new Color(0.55f, 0.35f, 0.2f));
            _steel = MaterialLibrary.Get(new Color(0.75f, 0.77f, 0.8f));
            _darkSteel = MaterialLibrary.Get(new Color(0.35f, 0.37f, 0.4f));
            _wall = MaterialLibrary.Get(new Color(0.82f, 0.25f, 0.2f));
            _floorMat = MaterialLibrary.Get(new Color(0.45f, 0.3f, 0.28f));
        }

        private void Start()
        {
            Transform level = new GameObject("Level").transform;
            level.SetParent(transform, false);

            _floor = new GameObject("Buffet Floor Registry").AddComponent<BuffetFloor>();
            _floor.transform.SetParent(transform, false);

            BuildLighting();
            BuildShell(level);
            BuildFrontDesk(level);
            BuildBuffetLine(level);
            BuildDining(level);
            BuildKitchen(level);
            BakeNavMesh(level);

            // Player and customers come after the bake so their colliders don't end up in the NavMesh.
            PlayerInventory inventory = BuildPlayer();
            BuildSystems(inventory);
        }

        private void BuildLighting()
        {
            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            sun.AddComponent<LightingDirector>();
        }

        private void BuildShell(Transform level)
        {
            Transform shell = new GameObject("Shell").transform;
            shell.SetParent(level, false);

            float roomDepth = BackWallZ - FrontWallZ;
            float roomCenterZ = (BackWallZ + FrontWallZ) * 0.5f;
            PrimitiveFactory.Solid("Floor", PrimitiveType.Cube, shell, new Vector3(0f, -0.05f, roomCenterZ), new Vector3(SideWallX * 2f, 0.1f, roomDepth), _floorMat);
            PrimitiveFactory.Solid("Sidewalk", PrimitiveType.Cube, shell, new Vector3(0f, -0.05f, FrontWallZ - 1.5f), new Vector3(SideWallX * 2f, 0.1f, 3f), MaterialLibrary.Get(new Color(0.6f, 0.6f, 0.6f)));

            float wallY = WallHeight * 0.5f;
            PrimitiveFactory.Solid("Back Wall", PrimitiveType.Cube, shell, new Vector3(0f, wallY, BackWallZ), new Vector3(SideWallX * 2f + WallThickness, WallHeight, WallThickness), _wall);
            PrimitiveFactory.Solid("Left Wall", PrimitiveType.Cube, shell, new Vector3(-SideWallX, wallY, roomCenterZ), new Vector3(WallThickness, WallHeight, roomDepth), _wall);
            PrimitiveFactory.Solid("Right Wall", PrimitiveType.Cube, shell, new Vector3(SideWallX, wallY, roomCenterZ), new Vector3(WallThickness, WallHeight, roomDepth), _wall);

            float leftLength = DoorMinX + SideWallX;
            float rightLength = SideWallX - DoorMaxX;
            PrimitiveFactory.Solid("Front Wall Left", PrimitiveType.Cube, shell, new Vector3(-SideWallX + leftLength * 0.5f, wallY, FrontWallZ), new Vector3(leftLength, WallHeight, WallThickness), _wall);
            PrimitiveFactory.Solid("Front Wall Right", PrimitiveType.Cube, shell, new Vector3(DoorMaxX + rightLength * 0.5f, wallY, FrontWallZ), new Vector3(rightLength, WallHeight, WallThickness), _wall);
            float doorCenterX = (DoorMinX + DoorMaxX) * 0.5f;
            PrimitiveFactory.Solid("Door Header", PrimitiveType.Cube, shell, new Vector3(doorCenterX, WallHeight - 0.3f, FrontWallZ), new Vector3(DoorMaxX - DoorMinX, 0.6f, WallThickness), _wall);

            // Half wall between the dining room and the kitchen, with a gap in the middle for the player.
            PrimitiveFactory.Solid("Kitchen Divider Left", PrimitiveType.Cube, shell, new Vector3(-9f, 0.55f, 7f), new Vector3(12f, 1.1f, 0.25f), _darkSteel);
            PrimitiveFactory.Solid("Kitchen Divider Right", PrimitiveType.Cube, shell, new Vector3(9f, 0.55f, 7f), new Vector3(12f, 1.1f, 0.25f), _darkSteel);

            AddSign(shell, new Vector3(0f, 2.5f, BackWallZ - 0.3f), "CHINESE BUFFET SIMULATOR\nplayable demo", 0.4f, new Color(1f, 0.85f, 0.3f));
            AddSign(shell, new Vector3(doorCenterX, 2.2f, FrontWallZ - 0.4f), "ENTRANCE", 0.3f, Color.white);
            AddSign(shell, new Vector3(0f, 1.9f, 7f), "KITCHEN", 0.3f, Color.white);

            BuildFrontWindow(shell);
            BuildWallHole(shell);
            BuildRestroomDoor(shell);
        }

        /// <summary>The front window: glass on the inside face of the wall, a dark hole with shards when a rock comes through, fixed with the glass pane.</summary>
        private void BuildFrontWindow(Transform shell)
        {
            var windowGo = new GameObject("Front Window");
            windowGo.transform.SetParent(shell, false);
            windowGo.transform.position = WindowCenter;
            float innerZ = FrontWallZ + WallThickness * 0.5f + 0.02f;

            GameObject frame = PrimitiveFactory.Visual("Frame", PrimitiveType.Cube, windowGo.transform, new Vector3(0f, 0f, innerZ - WindowCenter.z), new Vector3(2.7f, 1.7f, 0.04f), _darkSteel);
            GameObject glass = PrimitiveFactory.Visual("Glass", PrimitiveType.Cube, windowGo.transform, new Vector3(0f, 0f, innerZ - WindowCenter.z + 0.03f), new Vector3(2.4f, 1.4f, 0.03f), MaterialLibrary.Get(new Color(0.65f, 0.85f, 0.95f)));
            var broken = new GameObject("Broken");
            broken.transform.SetParent(windowGo.transform, false);
            PrimitiveFactory.Visual("Hole", PrimitiveType.Cube, broken.transform, new Vector3(0f, 0f, innerZ - WindowCenter.z + 0.03f), new Vector3(2.4f, 1.4f, 0.03f), MaterialLibrary.Get(new Color(0.05f, 0.05f, 0.08f)));
            Material shard = MaterialLibrary.Get(new Color(0.75f, 0.9f, 1f));
            PrimitiveFactory.Visual("Shard 1", PrimitiveType.Cube, broken.transform, new Vector3(-0.9f, 0.45f, innerZ - WindowCenter.z + 0.06f), new Vector3(0.6f, 0.5f, 0.02f), shard).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            PrimitiveFactory.Visual("Shard 2", PrimitiveType.Cube, broken.transform, new Vector3(1f, -0.4f, innerZ - WindowCenter.z + 0.06f), new Vector3(0.5f, 0.6f, 0.02f), shard).transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            PrimitiveFactory.Visual("Shard 3", PrimitiveType.Cube, broken.transform, new Vector3(0.3f, -1.5f, innerZ - WindowCenter.z + 0.6f), new Vector3(0.4f, 0.02f, 0.3f), shard);
            AddSign(shell, new Vector3(WindowCenter.x, WindowCenter.y + 1.05f, innerZ + 0.05f), "OPEN 24 HRS (10-9)", 0.14f, new Color(1f, 0.5f, 0.5f));

            _window = windowGo.AddComponent<BreakablePanel>();
            _window.Configure("Fit the window pane", CarryItems.GlassPane, "a glass pane", "the maintenance shelf in the kitchen", 4f, glass, broken,
                new Vector3(WindowCenter.x, 1.4f, innerZ + 0.4f), new Vector3(2.6f, 1.8f, 0.9f),
                economyConfig.LandlordPatchFee, "The landlord boarded up the window overnight");
        }

        /// <summary>Where the pitcher boy comes through: nothing to see until he does, then a ragged hole and rubble, fixed with duct tape.</summary>
        private void BuildWallHole(Transform shell)
        {
            var holeGo = new GameObject("Wall Hole");
            holeGo.transform.SetParent(shell, false);
            holeGo.transform.position = WallHoleCenter;
            float innerX = WallThickness * 0.5f + 0.02f;

            var broken = new GameObject("Broken");
            broken.transform.SetParent(holeGo.transform, false);
            Material dark = MaterialLibrary.Get(new Color(0.04f, 0.03f, 0.03f));
            PrimitiveFactory.Visual("Hole", PrimitiveType.Cube, broken.transform, new Vector3(innerX, 0.1f, 0f), new Vector3(0.03f, 2.2f, 1.7f), dark);
            GameObject edge = PrimitiveFactory.Visual("Edge", PrimitiveType.Cube, broken.transform, new Vector3(innerX + 0.01f, 0.1f, 0f), new Vector3(0.03f, 2.5f, 1.3f), dark);
            edge.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Material rubble = MaterialLibrary.Get(new Color(0.75f, 0.3f, 0.25f));
            PrimitiveFactory.Visual("Rubble 1", PrimitiveType.Cube, broken.transform, new Vector3(0.6f, -0.85f, 0.4f), new Vector3(0.35f, 0.25f, 0.3f), rubble).transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            PrimitiveFactory.Visual("Rubble 2", PrimitiveType.Cube, broken.transform, new Vector3(0.9f, -0.9f, -0.5f), new Vector3(0.25f, 0.18f, 0.4f), rubble).transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
            PrimitiveFactory.Visual("Rubble 3", PrimitiveType.Cube, broken.transform, new Vector3(1.4f, -0.93f, 0.1f), new Vector3(0.2f, 0.12f, 0.2f), rubble);

            _wallHole = holeGo.AddComponent<BreakablePanel>();
            _wallHole.Configure("Tape up the hole in the wall", CarryItems.DuctTape, "duct tape", "the maintenance shelf in the kitchen", 6f, null, broken,
                new Vector3(WallHoleCenter.x + 0.6f, 1f, WallHoleCenter.z), new Vector3(0.9f, 2.2f, 1.9f),
                economyConfig.LandlordPatchFee, "The landlord patched the hole in the wall overnight");
        }

        private void BuildRestroomDoor(Transform shell)
        {
            float innerX = -SideWallX + WallThickness * 0.5f + 0.03f;
            PrimitiveFactory.Visual("Restroom Door", PrimitiveType.Cube, shell, new Vector3(innerX, RestroomDoor.y, RestroomDoor.z), new Vector3(0.06f, 2.1f, 1f), MaterialLibrary.Get(new Color(0.35f, 0.22f, 0.15f)));
            PrimitiveFactory.Visual("Restroom Handle", PrimitiveType.Sphere, shell, new Vector3(innerX + 0.05f, RestroomDoor.y, RestroomDoor.z + 0.35f), Vector3.one * 0.08f, _steel);
            AddSign(shell, new Vector3(innerX + 0.1f, 2.45f, RestroomDoor.z), "RESTROOM\n(one toilet, shared)", 0.13f, Color.white);
        }

        private void BuildFrontDesk(Transform level)
        {
            Transform desk = new GameObject("Front Desk").transform;
            desk.SetParent(level, false);

            PrimitiveFactory.Solid("Counter", PrimitiveType.Cube, desk, new Vector3(-3f, 0.5f, -6f), new Vector3(3.2f, 1f, 1f), _wood);
            PrimitiveFactory.Visual("Cash Register", PrimitiveType.Cube, desk, new Vector3(-3f, 1.2f, -6f), new Vector3(0.6f, 0.4f, 0.5f), _darkSteel);
            AddSign(desk, new Vector3(-3f, 1.8f, -6f), "REGISTER", 0.22f, Color.white);
            BuildPhone(desk, new Vector3(-4.2f, 1f, -6f));
            BuildToGoBoxStack(desk, new Vector3(-1.8f, 1f, -6f));
            BuildPickupRack(level, new Vector3(6.2f, 0f, FrontWallZ + 0.7f));
            BuildSlotMachine(level);
            BuildFortuneWall(level);
            BuildFountain(level);

            var queueGo = new GameObject("Customer Queue");
            queueGo.transform.SetParent(desk, false);
            queueGo.transform.position = QueueStart;
            _queue = queueGo.AddComponent<CustomerQueue>();
            _queue.Configure(QueueDirection, 1.0f);

            Material marker = MaterialLibrary.Get(new Color(0.9f, 0.85f, 0.2f));
            for (int i = 0; i < economyConfig.MaxLineLength; i++)
            {
                Vector3 slot = _queue.SlotPosition(i);
                PrimitiveFactory.Visual($"Line Marker {i + 1}", PrimitiveType.Cube, desk, new Vector3(slot.x, 0.005f, slot.z), new Vector3(0.5f, 0.01f, 0.5f), marker);
            }
        }

        /// <summary>A pachislo cabinet by the front door with a lucky cat on top. Pulls come out of your own wallet.</summary>
        private void BuildSlotMachine(Transform level)
        {
            var go = new GameObject("Slot Machine");
            go.transform.SetParent(level, false);
            go.transform.position = SlotMachinePosition;
            Material cabinet = MaterialLibrary.Get(new Color(0.55f, 0.1f, 0.12f));
            Material gold = MaterialLibrary.Get(new Color(0.85f, 0.7f, 0.25f));
            PrimitiveFactory.Solid("Cabinet", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.6f), cabinet);
            PrimitiveFactory.Visual("Trim", PrimitiveType.Cube, go.transform, new Vector3(0f, 1.62f, 0f), new Vector3(0.84f, 0.04f, 0.64f), gold);
            PrimitiveFactory.Visual("Reel Window", PrimitiveType.Cube, go.transform, new Vector3(0f, 1.1f, -0.31f), new Vector3(0.66f, 0.3f, 0.02f), MaterialLibrary.Get(new Color(0.1f, 0.1f, 0.12f)));
            var reels = new Renderer[3];
            for (int i = 0; i < 3; i++)
                reels[i] = PrimitiveFactory.Visual($"Reel {i + 1}", PrimitiveType.Cube, go.transform, new Vector3(-0.2f + i * 0.2f, 1.1f, -0.33f), new Vector3(0.16f, 0.22f, 0.02f), MaterialLibrary.Get(Color.white)).GetComponent<Renderer>();
            PrimitiveFactory.Visual("Lever Arm", PrimitiveType.Cylinder, go.transform, new Vector3(0.5f, 1.25f, 0f), new Vector3(0.04f, 0.2f, 0.04f), _darkSteel);
            PrimitiveFactory.Visual("Lever Knob", PrimitiveType.Sphere, go.transform, new Vector3(0.5f, 1.47f, 0f), Vector3.one * 0.1f, MaterialLibrary.Get(new Color(0.9f, 0.2f, 0.2f)));
            GameObject happy = PrimitiveFactory.Visual("Happy Hour Plate", PrimitiveType.Cylinder, go.transform, new Vector3(0.5f, 1.05f, -0.05f), new Vector3(0.22f, 0.005f, 0.22f), MaterialLibrary.Get(new Color(0.98f, 0.98f, 0.95f)));
            happy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh happyText = PrimitiveFactory.Label("Happy Hour", happy.transform, Vector3.zero, "HAPPY\nHOUR", 0.3f, _font, new Color(0.2f, 0.2f, 0.2f));
            happyText.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            happyText.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            happy.SetActive(false);
            var tray = new GameObject("Tray").transform;
            tray.SetParent(go.transform, false);
            tray.localPosition = new Vector3(0f, 0.45f, -0.5f);
            PrimitiveFactory.Visual("Tray Lip", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.4f, -0.34f), new Vector3(0.6f, 0.08f, 0.1f), gold);

            // The lucky cat: a body, a head, and a paw that waves around its shoulder.
            var catRoot = new GameObject("Lucky Cat").transform;
            catRoot.SetParent(go.transform, false);
            catRoot.localPosition = new Vector3(0f, 1.64f, 0f);
            Renderer catBody = PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, catRoot, new Vector3(0f, 0.18f, 0f), new Vector3(0.24f, 0.18f, 0.22f), MaterialLibrary.Get(new Color(0.98f, 0.98f, 0.95f))).GetComponent<Renderer>();
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, catRoot, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.2f, MaterialLibrary.Get(new Color(0.98f, 0.98f, 0.95f)));
            PrimitiveFactory.Visual("Collar", PrimitiveType.Cylinder, catRoot, new Vector3(0f, 0.3f, 0f), new Vector3(0.18f, 0.015f, 0.18f), MaterialLibrary.Get(new Color(0.9f, 0.2f, 0.2f)));
            var paw = new GameObject("Paw Pivot").transform;
            paw.SetParent(catRoot, false);
            paw.localPosition = new Vector3(0.13f, 0.3f, -0.05f);
            PrimitiveFactory.Visual("Paw", PrimitiveType.Capsule, paw, new Vector3(0f, 0.09f, 0f), new Vector3(0.07f, 0.09f, 0.07f), MaterialLibrary.Get(new Color(0.98f, 0.98f, 0.95f)));

            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0f, 2.35f, 0f), "SLOT MACHINE", 0.16f, _font, new Color(1f, 0.85f, 0.4f));
            label.gameObject.AddComponent<Billboard>();
            var machine = go.AddComponent<SlotMachine>();
            machine.Initialize(economyConfig, _rng, reels, paw, catBody, label, tray, happy);
        }

        /// <summary>A corkboard with forty pushpins on the left wall, between the hole and the restroom.</summary>
        private void BuildFortuneWall(Transform level)
        {
            var go = new GameObject("Fortune Wall");
            go.transform.SetParent(level, false);
            go.transform.position = FortuneWallPosition;
            PrimitiveFactory.Visual("Cork", PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.05f, 1.2f, 1.7f), MaterialLibrary.Get(new Color(0.6f, 0.45f, 0.25f)));
            PrimitiveFactory.Visual("Frame", PrimitiveType.Cube, go.transform, new Vector3(-0.01f, 0f, 0f), new Vector3(0.04f, 1.3f, 1.8f), MaterialLibrary.Get(new Color(0.3f, 0.2f, 0.12f)));
            var pins = new System.Collections.Generic.List<Renderer>(40);
            Material pinMat = MaterialLibrary.Get(new Color(0.45f, 0.42f, 0.4f));
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var pin = PrimitiveFactory.Visual($"Pin {row * 8 + col + 1}", PrimitiveType.Sphere, go.transform,
                        new Vector3(0.04f, 0.42f - row * 0.21f, -0.7f + col * 0.2f), Vector3.one * 0.06f, pinMat);
                    pins.Add(pin.GetComponent<Renderer>());
                }
            }
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.6f, 1.4f, 1.9f);
            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0.3f, 0.95f, 0f), "WALL OF FORTUNE", 0.14f, _font, new Color(1f, 0.85f, 0.4f));
            label.gameObject.AddComponent<Billboard>();
            FortuneCatalog fortunes = fortuneCatalog != null ? fortuneCatalog : (fortuneCatalog = FortuneCatalog.CreateDefault());
            var wall = go.AddComponent<FortuneWall>();
            wall.Configure(fortunes.Count, pins, label);
        }

        /// <summary>The wishing fountain by the front window; it opens for a few seconds at close.</summary>
        private void BuildFountain(Transform level)
        {
            var go = new GameObject("Fountain");
            go.transform.SetParent(level, false);
            go.transform.position = FountainPosition;
            Material stone = MaterialLibrary.Get(new Color(0.55f, 0.55f, 0.5f));
            PrimitiveFactory.Solid("Basin", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.25f, 0f), new Vector3(2.2f, 0.25f, 2.2f), stone);
            GameObject water = PrimitiveFactory.Visual("Water", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.46f, 0f), new Vector3(1.9f, 0.02f, 1.9f), MaterialLibrary.Get(new Color(0.25f, 0.55f, 0.7f)));
            PrimitiveFactory.Visual("Column", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.3f, 0.4f, 0.3f), stone);
            PrimitiveFactory.Visual("Upper Bowl", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 0.06f, 0.9f), stone);
            PrimitiveFactory.Visual("Spout", PrimitiveType.Sphere, go.transform, new Vector3(0f, 1.35f, 0f), Vector3.one * 0.2f, MaterialLibrary.Get(new Color(0.3f, 0.6f, 0.75f)));
            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0f, 1.9f, 0f), "WISHING\nFOUNTAIN", 0.14f, _font, new Color(0.7f, 0.9f, 1f));
            label.gameObject.AddComponent<Billboard>();
            var fountain = go.AddComponent<Fountain>();
            fountain.Initialize(economyConfig, _rng, water.transform, 0.9f, label);
        }

        /// <summary>The wall phone on the counter: it rings, you answer, the order is in your head. It owns the phone-order logic.</summary>
        private void BuildPhone(Transform desk, Vector3 position)
        {
            var go = new GameObject("Phone");
            go.transform.SetParent(desk, false);
            go.transform.position = position;
            Material body = MaterialLibrary.Get(new Color(0.85f, 0.82f, 0.7f));
            PrimitiveFactory.Visual("Base", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.12f, 0.32f), body);
            GameObject handset = PrimitiveFactory.Visual("Handset", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.09f, 0.07f, 0.3f), MaterialLibrary.Get(new Color(0.75f, 0.72f, 0.6f)));
            PrimitiveFactory.Visual("Cord", PrimitiveType.Cylinder, go.transform, new Vector3(0.16f, 0.2f, -0.1f), new Vector3(0.02f, 0.2f, 0.02f), _darkSteel);
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(0.5f, 0.5f, 0.5f);
            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0f, 0.6f, 0f), "PHONE", 0.14f, _font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            var service = go.AddComponent<PhoneOrderService>();
            service.Initialize(economyConfig, foodCatalog, _rng, label, handset.transform);
        }

        private void BuildToGoBoxStack(Transform desk, Vector3 position)
        {
            var go = new GameObject("To-Go Boxes");
            go.transform.SetParent(desk, false);
            go.transform.position = position;
            Material card = MaterialLibrary.Get(new Color(0.95f, 0.95f, 0.9f));
            for (int i = 0; i < 4; i++)
                PrimitiveFactory.Visual($"Box {i + 1}", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.09f + i * 0.18f, 0f), new Vector3(0.36f, 0.17f, 0.3f), card);
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.4f, 0f);
            trigger.size = new Vector3(0.6f, 0.9f, 0.6f);
            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0f, 1.05f, 0f), "TO-GO BOXES", 0.12f, _font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            go.AddComponent<ToGoBoxStack>();
        }

        /// <summary>The door-dash style shelf by the front door; a racked box is paid for and gone.</summary>
        private void BuildPickupRack(Transform level, Vector3 position)
        {
            var go = new GameObject("To-Go Pickup Shelf");
            go.transform.SetParent(level, false);
            go.transform.position = position;
            PrimitiveFactory.Solid("Shelf", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 0.5f), _wood);
            PrimitiveFactory.Solid("Upper Shelf", PrimitiveType.Cube, go.transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.4f, 0.06f, 0.5f), _wood);
            PrimitiveFactory.Visual("Back", PrimitiveType.Cube, go.transform, new Vector3(0f, 1.25f, 0.22f), new Vector3(1.4f, 0.5f, 0.04f), _wood);
            var spot = new GameObject("Box Spot").transform;
            spot.SetParent(go.transform, false);
            spot.localPosition = new Vector3(0f, 1.0f, 0f);
            AddSign(go.transform, position + new Vector3(0f, 1.9f, 0f), "TO-GO PICKUP\n(rack the box, get paid)", 0.16f, new Color(1f, 0.85f, 0.4f));
            var rack = go.AddComponent<PickupRack>();
            rack.Configure(spot);
        }

        private void BuildBuffetLine(Transform level)
        {
            Transform line = new GameObject("Buffet Line").transform;
            line.SetParent(level, false);

            int trayCount = Mathf.Max(1, foodCatalog.Unlocked.Count);
            float counterLength = trayCount * BuffetSlotSpacing + 0.5f;
            bool useStationModel = PropLibrary.IsAvailable(StationModel);
            if (!useStationModel)
                PrimitiveFactory.Solid("Counter", PrimitiveType.Cube, line, new Vector3(0f, CounterHeight * 0.5f, BuffetZ), new Vector3(counterLength, CounterHeight, 1.2f), _steel);

            float startX = -(trayCount - 1) * BuffetSlotSpacing * 0.5f;
            for (int i = 0; i < trayCount; i++)
            {
                FoodDefinition food = foodCatalog.Unlocked[i];
                float x = startX + i * BuffetSlotSpacing;

                // One Meshy buffet station per slot when the model is in the project; its counter surface
                // is scaled to CounterHeight and only the counter body gets a collider (not the sneeze guard).
                float counterTop = CounterHeight;
                if (useStationModel)
                {
                    PlacedProp station = PropLibrary.Place(StationModel, line, new Vector3(x, 0f, BuffetZ), 0f,
                        targetHeight: CounterHeight / StationCounterFraction, colliderHeightFraction: StationCounterFraction);
                    if (station != null) counterTop = station.WorldBounds.min.y + station.Height * StationCounterFraction;
                }

                var trayGo = new GameObject($"Tray - {food.DisplayName}");
                trayGo.transform.SetParent(line, false);
                trayGo.transform.position = new Vector3(x, counterTop, BuffetZ);

                float fullHeight;
                float fillBase;
                Vector3 fillSize;
                float panHeight;
                PlacedProp pan = PropLibrary.Place(TrayModel, trayGo.transform, trayGo.transform.position + Vector3.up * 0.02f, 0f, targetWidth: 1.4f);
                if (pan != null)
                {
                    panHeight = pan.Height + 0.02f;
                    fillBase = 0.02f + pan.Height * 0.25f;
                    fullHeight = pan.Height * 0.65f;
                    fillSize = new Vector3(pan.Width * 0.82f, fullHeight, pan.Depth * 0.78f);
                }
                else
                {
                    PrimitiveFactory.Solid("Pan", PrimitiveType.Cube, trayGo.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.9f, 0.1f, 1f), _darkSteel);
                    PrimitiveFactory.Visual("Rim Left", PrimitiveType.Cube, trayGo.transform, new Vector3(-0.92f, 0.2f, 0f), new Vector3(0.06f, 0.3f, 1f), _darkSteel);
                    PrimitiveFactory.Visual("Rim Right", PrimitiveType.Cube, trayGo.transform, new Vector3(0.92f, 0.2f, 0f), new Vector3(0.06f, 0.3f, 1f), _darkSteel);
                    panHeight = 0.35f;
                    fillBase = 0.1f;
                    fullHeight = 0.3f;
                    fillSize = new Vector3(1.7f, fullHeight, 0.8f);
                }

                GameObject fill = PrimitiveFactory.Visual("Food", PrimitiveType.Cube, trayGo.transform, new Vector3(0f, fillBase + fullHeight * 0.5f, 0f), fillSize, MaterialLibrary.Get(food.Color));

                TextMesh label = PrimitiveFactory.Label("Label", trayGo.transform, new Vector3(0f, panHeight + 0.45f, 0f), string.Empty, 0.17f, _font, Color.white);
                label.gameObject.AddComponent<Billboard>();
                TextMesh warning = PrimitiveFactory.Label("Low Warning", trayGo.transform, new Vector3(0f, panHeight + 0.9f, 0f), "!", 0.6f, _font, new Color(1f, 0.25f, 0.2f));
                warning.gameObject.AddComponent<Billboard>();

                var stand = new GameObject("Stand Point");
                stand.transform.SetParent(trayGo.transform, false);
                stand.transform.localPosition = new Vector3(0f, -counterTop, -1.3f);

                var tray = trayGo.AddComponent<BuffetTray>();
                int initialUnits = _rng.Next(Mathf.Min(8, economyConfig.TrayCapacity), economyConfig.TrayCapacity + 1);
                tray.Configure(food, economyConfig.TrayCapacity, initialUnits, economyConfig.LowTrayWarning, stand.transform);
                tray.SetVisuals(fill.transform, fullHeight, fillBase, label, warning.gameObject);
                tray.SetToGoLimits(economyConfig.ToGoBoxMaxPerItem, economyConfig.ToGoBoxMaxUnits);
                _floor.RegisterSource(tray);
            }
        }

        private void BuildDining(Transform level)
        {
            Transform dining = new GameObject("Dining Room").transform;
            dining.SetParent(level, false);
            Material chair = MaterialLibrary.Get(new Color(0.3f, 0.2f, 0.15f));

            for (int i = 0; i < TablePositions.Length; i++)
            {
                Vector2 p = TablePositions[i];
                var tableGo = new GameObject($"Table {i + 1}");
                tableGo.transform.SetParent(dining, false);
                tableGo.transform.position = new Vector3(p.x, 0f, p.y);

                PrimitiveFactory.Solid("Top", PrimitiveType.Cube, tableGo.transform, new Vector3(0f, 0.74f, 0f), new Vector3(1.3f, 0.08f, 1.3f), _wood);
                PrimitiveFactory.Solid("Leg", PrimitiveType.Cube, tableGo.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.15f, 0.7f, 0.15f), _darkSteel);
                PrimitiveFactory.Solid("Chair", PrimitiveType.Cube, tableGo.transform, new Vector3(-1f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), chair);

                var seat = new GameObject("Seat Point");
                seat.transform.SetParent(tableGo.transform, false);
                seat.transform.localPosition = new Vector3(1.05f, 0f, 0f);

                var plates = new GameObject("Plates");
                plates.transform.SetParent(tableGo.transform, false);
                plates.transform.localPosition = new Vector3(0.3f, 0.78f, 0.3f);

                GameObject statusLight = PrimitiveFactory.Visual("Status Light", PrimitiveType.Sphere, tableGo.transform, new Vector3(-0.4f, 0.88f, -0.4f), Vector3.one * 0.18f, MaterialLibrary.Get(Color.green));

                // A lamp on a cord over every table (the booth-lamp event kills one of them).
                PrimitiveFactory.Visual("Lamp Cord", PrimitiveType.Cube, tableGo.transform, new Vector3(0f, 2.75f, 0f), new Vector3(0.02f, 0.9f, 0.02f), _darkSteel);
                PrimitiveFactory.Visual("Lamp Shade", PrimitiveType.Cylinder, tableGo.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0.55f, 0.1f, 0.55f), MaterialLibrary.Get(new Color(0.6f, 0.15f, 0.12f)));
                GameObject bulb = PrimitiveFactory.Visual("Lamp", PrimitiveType.Sphere, tableGo.transform, new Vector3(0f, 2.15f, 0f), Vector3.one * 0.22f, MaterialLibrary.Get(new Color(1f, 0.95f, 0.7f)));

                bool hasDrain = System.Array.IndexOf(DrainTables, i) >= 0;
                if (hasDrain)
                {
                    // A shower drain under the seat. Nobody asks why.
                    PrimitiveFactory.Visual("Drain", PrimitiveType.Cylinder, tableGo.transform, new Vector3(1.05f, 0.006f, 0f), new Vector3(0.6f, 0.005f, 0.6f), MaterialLibrary.Get(new Color(0.2f, 0.2f, 0.22f)));
                    PrimitiveFactory.Visual("Drain Grate", PrimitiveType.Cylinder, tableGo.transform, new Vector3(1.05f, 0.012f, 0f), new Vector3(0.4f, 0.005f, 0.4f), MaterialLibrary.Get(new Color(0.08f, 0.08f, 0.08f)));
                    _drainPoints.Add(tableGo.transform.position);
                }

                var table = tableGo.AddComponent<DiningTable>();
                table.Configure(seat.transform, plates.transform, statusLight.GetComponent<Renderer>(), bulb.GetComponent<Renderer>());
                _floor.RegisterTable(table);
            }
        }

        private void BuildKitchen(Transform level)
        {
            Transform kitchen = new GameObject("Kitchen").transform;
            kitchen.SetParent(level, false);

            int boxCount = Mathf.Max(1, foodCatalog.Unlocked.Count);
            float shelfLength = boxCount * BuffetSlotSpacing + 0.5f;
            const float shelfZ = 11.5f;
            PrimitiveFactory.Solid("Cooler Shelf", PrimitiveType.Cube, kitchen, new Vector3(0f, 0.5f, shelfZ), new Vector3(shelfLength, 1f, 1f), _steel);
            string coolerSign = "STORAGE COOLER\n$" + economyConfig.WholesaleBoxCost.ToString("0") + " per box of " + economyConfig.UnitsPerBox
                + (economyConfig.CookingEnabled ? "\nraw boxes go through a cooker" : "");
            AddSign(kitchen, new Vector3(0f, 2.2f, shelfZ), coolerSign, 0.22f, new Color(0.7f, 0.9f, 1f));

            float startX = -(boxCount - 1) * BuffetSlotSpacing * 0.5f;
            for (int i = 0; i < boxCount; i++)
            {
                FoodDefinition food = foodCatalog.Unlocked[i];
                var boxGo = new GameObject($"Storage Box - {food.DisplayName}");
                boxGo.transform.SetParent(kitchen, false);
                boxGo.transform.position = new Vector3(startX + i * BuffetSlotSpacing, 1f, shelfZ);

                PrimitiveFactory.Solid("Box", PrimitiveType.Cube, boxGo.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.5f, 0.8f), MaterialLibrary.Get(food.Color));
                PrimitiveFactory.Visual("Lid", PrimitiveType.Cube, boxGo.transform, new Vector3(0f, 0.52f, 0f), new Vector3(1.45f, 0.05f, 0.85f), MaterialLibrary.Get(new Color(0.9f, 0.95f, 1f)));
                TextMesh label = PrimitiveFactory.Label("Label", boxGo.transform, new Vector3(0f, 0.95f, 0f), food.DisplayName, 0.14f, _font, Color.white);
                label.gameObject.AddComponent<Billboard>();

                var box = boxGo.AddComponent<StorageBox>();
                box.Configure(food, economyConfig.WholesaleBoxCost, economyConfig.UnitsPerBox, economyConfig.CookingEnabled, economyConfig.StartingCookedStock, label);
            }

            var dishwasherGo = new GameObject("Dishwasher");
            dishwasherGo.transform.SetParent(kitchen, false);
            dishwasherGo.transform.position = DishwasherPosition;
            PrimitiveFactory.Solid("Body", PrimitiveType.Cube, dishwasherGo.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.4f), _steel);
            PrimitiveFactory.Visual("Handle", PrimitiveType.Cube, dishwasherGo.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.8f, 0.08f, 0.08f), _darkSteel);
            TextMesh dishLabel = PrimitiveFactory.Label("Label", dishwasherGo.transform, new Vector3(0f, 1.7f, 0f), string.Empty, 0.18f, _font, Color.white);
            dishLabel.gameObject.AddComponent<Billboard>();
            var dishwasher = dishwasherGo.AddComponent<Dishwasher>();
            dishwasher.Configure(economyConfig.DishwasherCapacity, dishLabel);

            BuildTrashCan(kitchen, new Vector3(-12.5f, 0f, 9.5f));
            BuildTrashCan(level, new Vector3(13f, 0f, -2.5f));
            BuildMaintenanceShelf(kitchen);
            BuildCookers(kitchen);
        }

        /// <summary>Lightbulbs, duct tape, the wrench and a glass pane on a shelf against the kitchen's left wall.</summary>
        private void BuildMaintenanceShelf(Transform kitchen)
        {
            const float shelfX = -14.4f;
            const float shelfZ = 9.4f;
            PrimitiveFactory.Solid("Maintenance Shelf", PrimitiveType.Cube, kitchen, new Vector3(shelfX, 0.5f, shelfZ), new Vector3(0.8f, 1f, 3.8f), _darkSteel);
            AddSign(kitchen, new Vector3(shelfX + 0.3f, 2.3f, shelfZ), "MAINTENANCE", 0.2f, new Color(1f, 0.85f, 0.4f));

            BuildSupply(kitchen, new Vector3(shelfX, 1f, shelfZ - 1.4f), "Lightbulbs", CarryItems.Lightbulb, "a lightbulb", 0f, 1, economyConfig.LightbulbsPerDay, false, true,
                PrimitiveType.Sphere, new Vector3(0.25f, 0.25f, 0.25f), new Color(1f, 0.97f, 0.75f));
            BuildSupply(kitchen, new Vector3(shelfX, 1f, shelfZ - 0.45f), "Duct Tape", CarryItems.DuctTape, "duct tape", economyConfig.DuctTapeCost, economyConfig.DuctTapeStrips, -1, false, false,
                PrimitiveType.Cylinder, new Vector3(0.28f, 0.06f, 0.28f), new Color(0.6f, 0.62f, 0.65f));
            BuildSupply(kitchen, new Vector3(shelfX, 1f, shelfZ + 0.5f), "Wrench", CarryItems.Wrench, "the wrench", 0f, 1, -1, false, false,
                PrimitiveType.Cube, new Vector3(0.08f, 0.05f, 0.5f), new Color(0.5f, 0.52f, 0.55f));
            BuildSupply(kitchen, new Vector3(shelfX, 1f, shelfZ + 1.45f), "Glass Pane", CarryItems.GlassPane, "a glass pane", economyConfig.GlassPaneCost, 1, -1, true, true,
                PrimitiveType.Cube, new Vector3(0.04f, 1.1f, 0.7f), new Color(0.7f, 0.85f, 0.95f));
        }

        private void BuildSupply(Transform parent, Vector3 position, string name, string itemId, string displayName, float cost, int uses, int dailyStock, bool heavy, bool fragile,
            PrimitiveType shape, Vector3 size, Color color)
        {
            var go = new GameObject($"Supply - {name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            PrimitiveFactory.Visual("Visual", shape, go.transform, new Vector3(0f, size.y * 0.5f, 0f), size, MaterialLibrary.Get(color));
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, Mathf.Max(0.2f, size.y * 0.5f), 0f);
            trigger.size = new Vector3(Mathf.Max(0.5f, size.x), Mathf.Max(0.4f, size.y), Mathf.Max(0.6f, size.z));
            TextMesh label = PrimitiveFactory.Label("Label", go.transform, new Vector3(0.45f, size.y + 0.35f, 0f), string.Empty, 0.11f, _font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            var item = go.AddComponent<SupplyItem>();
            item.Configure(itemId, displayName, cost, uses, dailyStock, heavy, fragile, label);
        }

        /// <summary>The four cookers along the back wall: the Meshy fryer and stove where the models exist, primitives otherwise.</summary>
        private void BuildCookers(Transform kitchen)
        {
            const float cookerZ = 11.5f;
            BuildCooker(kitchen, CookerKind.Fryer, "Deep Fryer", new Vector3(-11f, 0f, cookerZ));
            BuildCooker(kitchen, CookerKind.Steamer, "Steamer", new Vector3(-9.2f, 0f, cookerZ));
            BuildCooker(kitchen, CookerKind.RiceCooker, "Rice Cooker", new Vector3(9.2f, 0f, cookerZ));
            BuildCooker(kitchen, CookerKind.Wok, "Wok", new Vector3(11f, 0f, cookerZ));
        }

        private void BuildCooker(Transform kitchen, CookerKind kind, string cookerName, Vector3 position)
        {
            var go = new GameObject($"Cooker - {cookerName}");
            go.transform.SetParent(kitchen, false);
            go.transform.position = position;
            Transform root = go.transform;

            Material iron = MaterialLibrary.Get(new Color(0.2f, 0.2f, 0.22f));
            Material cream = MaterialLibrary.Get(new Color(0.93f, 0.9f, 0.82f));
            Material lightMat = MaterialLibrary.Get(new Color(0.25f, 0.25f, 0.27f));
            Transform foodVisual = null;
            float foodHeight = 0.15f;
            Renderer indicator = null;
            Transform basket = null;
            float basketDrop = 0f;
            float top;

            switch (kind)
            {
                case CookerKind.Fryer:
                {
                    PlacedProp body = PropLibrary.Place(FryerModel, root, position, 0f, targetHeight: 1.0f);
                    if (body != null)
                    {
                        top = body.Top;
                    }
                    else
                    {
                        PrimitiveFactory.Solid("Body", PrimitiveType.Cube, root, new Vector3(0f, 0.5f, 0f), new Vector3(1.1f, 1f, 0.9f), _steel);
                        PrimitiveFactory.Visual("Oil", PrimitiveType.Cube, root, new Vector3(0f, 0.98f, 0f), new Vector3(0.9f, 0.04f, 0.7f), MaterialLibrary.Get(new Color(0.75f, 0.6f, 0.2f)));
                        top = 1f;
                    }

                    // The basket (and the food in it) hang off a rig the station lowers into the oil.
                    var rig = new GameObject("Basket Rig").transform;
                    rig.SetParent(root, false);
                    rig.localPosition = new Vector3(0f, top + 0.2f, 0f);
                    PlacedProp basketProp = PropLibrary.Place(FryerBasketModel, rig, new Vector3(position.x, top - 0.02f, position.z), 0f, targetWidth: 0.9f, addCollider: false);
                    if (basketProp == null)
                    {
                        PrimitiveFactory.Visual("Basket", PrimitiveType.Cube, rig, new Vector3(0f, -0.1f, 0f), new Vector3(0.7f, 0.22f, 0.55f), _darkSteel);
                        PrimitiveFactory.Visual("Basket Handle", PrimitiveType.Cube, rig, new Vector3(0f, 0.05f, -0.6f), new Vector3(0.1f, 0.04f, 0.5f), _darkSteel);
                    }
                    foodVisual = PrimitiveFactory.Visual("Food", PrimitiveType.Cube, rig, new Vector3(0f, 0.02f, 0f), new Vector3(0.55f, foodHeight, 0.42f), _steel).transform;
                    basket = rig;
                    basketDrop = 0.22f;
                    indicator = PrimitiveFactory.Visual("Light", PrimitiveType.Sphere, root, new Vector3(0.35f, top - 0.15f, -0.46f), Vector3.one * 0.1f, lightMat).GetComponent<Renderer>();
                    break;
                }

                case CookerKind.Wok:
                {
                    PlacedProp stove = PropLibrary.Place(StoveModel, root, position, 0f, targetHeight: 0.9f);
                    if (stove != null)
                    {
                        top = stove.Top;
                        for (int k = -1; k <= 1; k++)
                        {
                            var knobPosition = new Vector3(position.x + k * 0.25f, top * 0.8f, stove.WorldBounds.min.z - 0.01f);
                            PropLibrary.Place(StoveKnobModel, root, knobPosition, 0f, targetWidth: 0.08f, addCollider: false);
                        }
                    }
                    else
                    {
                        PrimitiveFactory.Solid("Stove", PrimitiveType.Cube, root, new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.9f), _darkSteel);
                        top = 0.9f;
                    }
                    indicator = PrimitiveFactory.Visual("Burner", PrimitiveType.Cylinder, root, new Vector3(0f, top + 0.015f, 0f), new Vector3(0.55f, 0.015f, 0.55f), lightMat).GetComponent<Renderer>();
                    PrimitiveFactory.Visual("Wok", PrimitiveType.Sphere, root, new Vector3(0f, top + 0.16f, 0f), new Vector3(0.9f, 0.32f, 0.9f), iron);
                    PrimitiveFactory.Visual("Handle", PrimitiveType.Cube, root, new Vector3(0f, top + 0.2f, -0.65f), new Vector3(0.06f, 0.06f, 0.5f), iron);
                    foodVisual = PrimitiveFactory.Visual("Food", PrimitiveType.Sphere, root, new Vector3(0f, top + 0.26f, 0f), new Vector3(0.6f, foodHeight, 0.6f), _steel).transform;
                    break;
                }

                case CookerKind.Steamer:
                {
                    PrimitiveFactory.Solid("Tower", PrimitiveType.Cylinder, root, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.55f, 0.9f), _steel);
                    PrimitiveFactory.Visual("Tier 1", PrimitiveType.Cylinder, root, new Vector3(0f, 0.4f, 0f), new Vector3(0.96f, 0.02f, 0.96f), _darkSteel);
                    PrimitiveFactory.Visual("Tier 2", PrimitiveType.Cylinder, root, new Vector3(0f, 0.75f, 0f), new Vector3(0.96f, 0.02f, 0.96f), _darkSteel);
                    PrimitiveFactory.Visual("Scale", PrimitiveType.Cube, root, new Vector3(0.2f, 0.9f, -0.44f), new Vector3(0.25f, 0.12f, 0.03f), MaterialLibrary.Get(new Color(0.8f, 0.75f, 0.6f)));
                    foodVisual = PrimitiveFactory.Visual("Food", PrimitiveType.Cylinder, root, new Vector3(0f, 1.14f, 0f), new Vector3(0.6f, foodHeight, 0.6f), _steel).transform;
                    PrimitiveFactory.Visual("Lid", PrimitiveType.Sphere, root, new Vector3(0f, 1.3f, 0f), new Vector3(0.88f, 0.28f, 0.88f), _steel);
                    PrimitiveFactory.Visual("Dial", PrimitiveType.Cylinder, root, new Vector3(-0.25f, 0.3f, -0.46f), new Vector3(0.12f, 0.02f, 0.12f), iron).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    indicator = PrimitiveFactory.Visual("Light", PrimitiveType.Sphere, root, new Vector3(0.25f, 0.3f, -0.46f), Vector3.one * 0.1f, lightMat).GetComponent<Renderer>();
                    top = 1.45f;
                    break;
                }

                default:
                {
                    PrimitiveFactory.Solid("Cabinet", PrimitiveType.Cube, root, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.9f), _darkSteel);
                    PrimitiveFactory.Solid("Pot", PrimitiveType.Cylinder, root, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 0.3f, 0.8f), cream);
                    foodVisual = PrimitiveFactory.Visual("Food", PrimitiveType.Cylinder, root, new Vector3(0f, 1.3f, 0f), new Vector3(0.6f, foodHeight, 0.6f), _steel).transform;
                    PrimitiveFactory.Visual("Lid", PrimitiveType.Sphere, root, new Vector3(0f, 1.46f, 0f), new Vector3(0.8f, 0.22f, 0.8f), cream);
                    PrimitiveFactory.Visual("Lid Knob", PrimitiveType.Sphere, root, new Vector3(0f, 1.58f, 0f), Vector3.one * 0.08f, iron);
                    PrimitiveFactory.Visual("COOK", PrimitiveType.Cube, root, new Vector3(0f, 0.95f, -0.41f), new Vector3(0.14f, 0.06f, 0.02f), MaterialLibrary.Get(new Color(0.7f, 0.1f, 0.1f)));
                    indicator = PrimitiveFactory.Visual("Light", PrimitiveType.Sphere, root, new Vector3(0.22f, 1.05f, -0.4f), Vector3.one * 0.08f, lightMat).GetComponent<Renderer>();
                    top = 1.6f;
                    break;
                }
            }

            GameObject smoke = PrimitiveFactory.Visual("Smoke", PrimitiveType.Sphere, root, new Vector3(0f, top + 0.6f, 0f), Vector3.one * 0.4f, MaterialLibrary.Get(new Color(0.3f, 0.3f, 0.3f)));
            smoke.SetActive(false);
            TextMesh label = PrimitiveFactory.Label("Label", root, new Vector3(0f, top + 0.95f, 0f), cookerName, 0.16f, _font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            if (foodVisual != null) foodVisual.gameObject.SetActive(false);

            var station = go.AddComponent<CookingStation>();
            station.Configure(kind, cookerName, economyConfig, label, foodVisual, foodHeight, indicator, basket, basketDrop, smoke.transform);
        }

        private void BuildTrashCan(Transform parent, Vector3 position)
        {
            var canGo = new GameObject("Trash Can");
            canGo.transform.SetParent(parent, false);
            canGo.transform.position = position;
            PrimitiveFactory.Solid("Can", PrimitiveType.Cylinder, canGo.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.45f, 0.7f), MaterialLibrary.Get(new Color(0.2f, 0.4f, 0.25f)));
            TextMesh label = PrimitiveFactory.Label("Label", canGo.transform, new Vector3(0f, 1.25f, 0f), "TRASH", 0.16f, _font, Color.white);
            label.gameObject.AddComponent<Billboard>();
            canGo.AddComponent<TrashCan>();
        }

        private void AddSign(Transform parent, Vector3 worldPosition, string text, float height, Color color)
        {
            TextMesh sign = PrimitiveFactory.Label("Sign", parent, Vector3.zero, text, height, _font, color);
            sign.transform.position = worldPosition;
            sign.gameObject.AddComponent<Billboard>();
        }

        private void BakeNavMesh(Transform level)
        {
            NavMeshSurface surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
        }

        private PlayerInventory BuildPlayer()
        {
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = PlayerSpawn;
            playerGo.layer = ignoreRaycast;

            CharacterController controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            var cameraGo = new GameObject("Player Camera");
            cameraGo.transform.SetParent(playerGo.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 75f;
            cameraGo.AddComponent<AudioListener>();

            // Crude body so the player casts a shadow and a GLB can replace it later.
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(playerGo.transform, false);
            PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, placeholder.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), MaterialLibrary.Get(new Color(0.2f, 0.4f, 0.8f)));
            PrimitiveFactory.Visual("Apron", PrimitiveType.Cube, placeholder.transform, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.5f, 0.7f, 0.08f), MaterialLibrary.Get(Color.white));
            SetLayerRecursively(placeholder, ignoreRaycast);
            GlbModelLoader loader = playerGo.AddComponent<GlbModelLoader>();
            loader.Configure("Models/player.glb", placeholder);

            var player = playerGo.AddComponent<PlayerController>();
            player.CameraPivot = cameraGo.transform;

            PlayerInventory inventory = playerGo.AddComponent<PlayerInventory>();
            inventory.Configure(economyConfig.PlayerPlateCapacity);
            playerGo.AddComponent<PlayerPocket>();
            playerGo.AddComponent<PlayerEffects>();

            PlayerInteractor interactor = playerGo.AddComponent<PlayerInteractor>();
            interactor.ViewCamera = camera;
            interactor.Inventory = inventory;

            PlayerThrower thrower = playerGo.AddComponent<PlayerThrower>();
            thrower.ViewCamera = camera;

            // Hand-held visuals hang off the camera.
            var hands = new GameObject("Hands");
            hands.transform.SetParent(cameraGo.transform, false);
            hands.transform.localPosition = new Vector3(0.3f, -0.32f, 0.65f);
            hands.transform.localRotation = Quaternion.Euler(10f, -10f, 0f);

            var trayVisual = new GameObject("Tray Visual");
            trayVisual.transform.SetParent(hands.transform, false);
            GameObject trayFood;
            PlacedProp handPan = PropLibrary.Place(TrayModel, trayVisual.transform, trayVisual.transform.position, 0f, targetWidth: 0.5f, addCollider: false);
            if (handPan != null)
            {
                handPan.Root.transform.localPosition = Vector3.zero;
                handPan.Root.transform.localRotation = Quaternion.identity;
                float panHeight = handPan.Height;
                trayFood = PrimitiveFactory.Visual("Tray Food", PrimitiveType.Cube, trayVisual.transform, new Vector3(0f, panHeight * 0.3f, 0f), new Vector3(0.4f, 0.08f, handPan.Depth * 0.75f), MaterialLibrary.Get(Color.white));
            }
            else
            {
                PrimitiveFactory.Visual("Tray", PrimitiveType.Cube, trayVisual.transform, Vector3.zero, new Vector3(0.5f, 0.03f, 0.35f), _darkSteel);
                trayFood = PrimitiveFactory.Visual("Tray Food", PrimitiveType.Cube, trayVisual.transform, new Vector3(0f, 0.055f, 0f), new Vector3(0.42f, 0.08f, 0.28f), MaterialLibrary.Get(Color.white));
            }
            GameObject plateStack = PrimitiveFactory.Visual("Plate Stack", PrimitiveType.Cylinder, hands.transform, Vector3.zero, new Vector3(0.3f, 0.02f, 0.3f), MaterialLibrary.Get(new Color(0.92f, 0.92f, 0.9f)));
            var itemRoot = new GameObject("Held Items");
            itemRoot.transform.SetParent(hands.transform, false);
            SetLayerRecursively(hands, ignoreRaycast);

            PlayerHandVisual handVisual = playerGo.AddComponent<PlayerHandVisual>();
            handVisual.Configure(inventory, trayVisual.transform, trayFood.GetComponent<Renderer>(), plateStack.transform, itemRoot.transform);

            return inventory;
        }

        private void BuildSystems(PlayerInventory inventory)
        {
            // HUD subscribes first so it sees the ledger's opening balance.
            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(transform, false);
            HudController hud = hudGo.AddComponent<HudController>();
            hud.Initialize(_font, inventory, inventory.GetComponent<PlayerPocket>(), inventory.GetComponent<PlayerEffects>());

            var ledgerGo = new GameObject("Economy Ledger");
            ledgerGo.transform.SetParent(transform, false);
            EconomyLedger ledger = ledgerGo.AddComponent<EconomyLedger>();
            ledger.Initialize(economyConfig);

            // Satisfaction and the day clock come after the ledger and HUD (so both see the opening
            // reputation and DayStarted(1)) and before the spawner (which defaults to Open / 50).
            var reputationGo = new GameObject("Store Reputation");
            reputationGo.transform.SetParent(transform, false);
            StoreReputation reputation = reputationGo.AddComponent<StoreReputation>();
            reputation.Initialize(economyConfig);

            var clockGo = new GameObject("Day Clock");
            clockGo.transform.SetParent(transform, false);
            DayClock dayClock = clockGo.AddComponent<DayClock>();
            dayClock.Initialize(economyConfig);

            var context = new CustomerContext
            {
                Config = economyConfig,
                Buffet = _floor,
                Tables = _floor,
                Queue = _queue,
                RegisterPoint = RegisterPoint,
                ExitPoint = ExitPoint,
                Rng = _rng,
            };

            var customersGo = new GameObject("Customers");
            customersGo.transform.SetParent(transform, false);
            CustomerSpawner spawner = customersGo.AddComponent<CustomerSpawner>();
            spawner.Initialize(context, foodCatalog, SpawnPoint, _font, 1.5f);

            // Chaos events: data-driven definitions, one running at a time. The scheduler only needs landmarks
            // and a way to look at customers; it never holds the systems themselves.
            var chaosContext = new ChaosEventContext
            {
                Config = economyConfig,
                Rng = _rng,
                Font = _font,
                DoorOutside = SpawnPoint,
                DoorInside = new Vector3(4f, 0f, -8.5f),
                RegisterPoint = RegisterPoint,
                FloorBounds = new Bounds(new Vector3(0f, 0f, -2f), new Vector3(28f, 2f, 12f)),
                Queue = _queue,
                CustomersInStore = () => spawner.Customers,
                FoodSources = () => _floor.Sources,
                Player = inventory.transform,
                WindowPoint = new Vector3(WindowCenter.x, 0f, FrontWallZ + 0.6f),
                WallHolePoint = new Vector3(WallHoleCenter.x + 0.9f, 0f, WallHoleCenter.z),
                RestroomPoint = new Vector3(RestroomDoor.x + 1f, 0f, RestroomDoor.z),
                BuffetPoint = new Vector3(0f, 0f, BuffetZ - 1.8f),
                KitchenPoint = new Vector3(0f, 0f, 7f),
                DishwasherPoint = DishwasherPosition + new Vector3(0f, 0f, -1.1f),
                DrainPoints = _drainPoints.ToArray(),
                Window = _window,
                WallHole = _wallHole,
            };
            var chaosGo = new GameObject("Chaos Events");
            chaosGo.transform.SetParent(transform, false);
            ChaosEventScheduler chaos = chaosGo.AddComponent<ChaosEventScheduler>();
            ChaosEventCatalog catalog = chaosCatalog != null ? chaosCatalog : ChaosEventCatalog.CreateDefault();
            chaos.Initialize(catalog, chaosContext);

            // Robberies run off their own customer counter and ask the scheduler for the event by id.
            var robberyGo = new GameObject("Robbery Counter");
            robberyGo.transform.SetParent(transform, false);
            robberyGo.AddComponent<RobberyScheduler>().Initialize(economyConfig, _rng);

            // Fortunes: the teller applies a cracked cookie's effect through the bus; the wall and the machine listen.
            var tellerGo = new GameObject("Fortune Teller");
            tellerGo.transform.SetParent(transform, false);
            tellerGo.AddComponent<FortuneTeller>().Initialize(fortuneCatalog != null ? fortuneCatalog : FortuneCatalog.CreateDefault(), economyConfig, _rng, inventory.transform);

            var debugGo = new GameObject("Debug Tools");
            debugGo.transform.SetParent(transform, false);
            debugGo.AddComponent<DebugTools>().Initialize(catalog, inventory.GetComponent<PlayerPocket>(), inventory.transform);

            GameEvents.RaiseNotice($"{foodCatalog.Unlocked.Count} foods unlocked, {_floor.TableCount} tables, ${economyConfig.StartingMoney:0.00} in the till.");
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
