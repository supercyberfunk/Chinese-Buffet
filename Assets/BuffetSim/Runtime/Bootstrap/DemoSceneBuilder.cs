using BuffetSim.Buffet;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Day;
using BuffetSim.Economy;
using BuffetSim.Events;
using BuffetSim.Food;
using BuffetSim.Models;
using BuffetSim.Player;
using BuffetSim.Stations;
using BuffetSim.Tables;
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

        private static readonly Vector2[] TablePositions =
        {
            new Vector2(-9f, -0.5f), new Vector2(-4.5f, -0.5f), new Vector2(0f, -0.5f), new Vector2(4.5f, -0.5f), new Vector2(9f, -0.5f),
            new Vector2(-9f, -4.5f), new Vector2(9f, -4.5f),
        };

        private Font _font;
        private System.Random _rng;
        private BuffetFloor _floor;
        private CustomerQueue _queue;

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
        }

        private void BuildFrontDesk(Transform level)
        {
            Transform desk = new GameObject("Front Desk").transform;
            desk.SetParent(level, false);

            PrimitiveFactory.Solid("Counter", PrimitiveType.Cube, desk, new Vector3(-3f, 0.5f, -6f), new Vector3(3.2f, 1f, 1f), _wood);
            PrimitiveFactory.Visual("Cash Register", PrimitiveType.Cube, desk, new Vector3(-3f, 1.2f, -6f), new Vector3(0.6f, 0.4f, 0.5f), _darkSteel);
            AddSign(desk, new Vector3(-3f, 1.8f, -6f), "REGISTER", 0.22f, Color.white);

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

                var table = tableGo.AddComponent<DiningTable>();
                table.Configure(seat.transform, plates.transform, statusLight.GetComponent<Renderer>());
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
            AddSign(kitchen, new Vector3(0f, 2.2f, shelfZ), "STORAGE COOLER\n$" + economyConfig.WholesaleBoxCost.ToString("0") + " per box of " + economyConfig.UnitsPerBox, 0.22f, new Color(0.7f, 0.9f, 1f));

            float startX = -(boxCount - 1) * BuffetSlotSpacing * 0.5f;
            for (int i = 0; i < boxCount; i++)
            {
                FoodDefinition food = foodCatalog.Unlocked[i];
                var boxGo = new GameObject($"Storage Box - {food.DisplayName}");
                boxGo.transform.SetParent(kitchen, false);
                boxGo.transform.position = new Vector3(startX + i * BuffetSlotSpacing, 1f, shelfZ);

                PrimitiveFactory.Solid("Box", PrimitiveType.Cube, boxGo.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.5f, 0.8f), MaterialLibrary.Get(food.Color));
                PrimitiveFactory.Visual("Lid", PrimitiveType.Cube, boxGo.transform, new Vector3(0f, 0.52f, 0f), new Vector3(1.45f, 0.05f, 0.85f), MaterialLibrary.Get(new Color(0.9f, 0.95f, 1f)));
                TextMesh label = PrimitiveFactory.Label("Label", boxGo.transform, new Vector3(0f, 0.95f, 0f), food.DisplayName, 0.16f, _font, Color.white);
                label.gameObject.AddComponent<Billboard>();

                var box = boxGo.AddComponent<StorageBox>();
                box.Configure(food, economyConfig.WholesaleBoxCost, economyConfig.UnitsPerBox);
            }

            var dishwasherGo = new GameObject("Dishwasher");
            dishwasherGo.transform.SetParent(kitchen, false);
            dishwasherGo.transform.position = new Vector3(12f, 0f, 9.5f);
            PrimitiveFactory.Solid("Body", PrimitiveType.Cube, dishwasherGo.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.4f), _steel);
            PrimitiveFactory.Visual("Handle", PrimitiveType.Cube, dishwasherGo.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.8f, 0.08f, 0.08f), _darkSteel);
            TextMesh dishLabel = PrimitiveFactory.Label("Label", dishwasherGo.transform, new Vector3(0f, 1.7f, 0f), string.Empty, 0.18f, _font, Color.white);
            dishLabel.gameObject.AddComponent<Billboard>();
            var dishwasher = dishwasherGo.AddComponent<Dishwasher>();
            dishwasher.Configure(economyConfig.DishwasherCapacity, dishLabel);

            BuildTrashCan(kitchen, new Vector3(-12.5f, 0f, 9.5f));
            BuildTrashCan(level, new Vector3(13f, 0f, -2.5f));
            BuildCookingProps(kitchen);
        }

        /// <summary>Meshy fryer and stove models as set dressing; cooking itself is out of scope for the demo.</summary>
        private void BuildCookingProps(Transform kitchen)
        {
            var fryerPosition = new Vector3(-11f, 0f, 11.5f);
            PlacedProp fryer = PropLibrary.Place(FryerModel, kitchen, fryerPosition, 0f, targetHeight: 1.0f);
            if (fryer != null)
            {
                PropLibrary.Place(FryerBasketModel, kitchen, new Vector3(fryerPosition.x, fryer.Top - 0.02f, fryerPosition.z), 0f, targetWidth: 0.9f, addCollider: false);
                AddSign(kitchen, new Vector3(fryerPosition.x, fryer.Top + 0.9f, fryerPosition.z), "DEEP FRYER\n(cooking: coming soon)", 0.16f, Color.white);
            }

            var stovePosition = new Vector3(11f, 0f, 11.5f);
            PlacedProp stove = PropLibrary.Place(StoveModel, kitchen, stovePosition, 0f, targetHeight: 0.9f);
            if (stove != null)
            {
                for (int k = -1; k <= 1; k++)
                {
                    var knobPosition = new Vector3(stovePosition.x + k * 0.25f, stove.Top * 0.8f, stove.WorldBounds.min.z - 0.01f);
                    PropLibrary.Place(StoveKnobModel, kitchen, knobPosition, 0f, targetWidth: 0.08f, addCollider: false);
                }
                AddSign(kitchen, new Vector3(stovePosition.x, stove.Top + 0.9f, stovePosition.z), "WOK STOVE\n(cooking: coming soon)", 0.16f, Color.white);
            }
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
                Player = inventory.transform,
            };
            var chaosGo = new GameObject("Chaos Events");
            chaosGo.transform.SetParent(transform, false);
            ChaosEventScheduler chaos = chaosGo.AddComponent<ChaosEventScheduler>();
            chaos.Initialize(chaosCatalog != null ? chaosCatalog : ChaosEventCatalog.CreateDefault(), chaosContext);

            GameEvents.RaiseNotice($"{foodCatalog.Unlocked.Count} foods unlocked, {_floor.TableCount} tables, ${economyConfig.StartingMoney:0.00} in the till.");
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
