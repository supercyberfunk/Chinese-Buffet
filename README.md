# Chinese Buffet Simulator

A chaotic first-person buffet simulator. This repository holds the Unity project; the
design notes live in Milanote and the architecture rules for contributors (human or Claude) are in
[CLAUDE.md](CLAUDE.md).

## Playable demo

The demo covers the first to-do list from the design notes, single player:

- Crude first-person player with movement, mouse look, sprint and jump.
- Interactions with 3D objects: buffet trays, the storage cooler, tables, the dishwasher and trash cans.
- NPC customers with NavMesh pathing running the core loop from the notes: walk in, roll what they
  want from the unlocked foods, wait in line until a table is free, grab from each tray, sit, eat,
  pay at the register and leave. Anything they couldn't get is deducted from their bill.
- Money visibly coming in and out: a running balance, floating `+$` / `-$` popups in the world, and
  a day summary (customers served, walked out, revenue, food cost, deductions).

The numbers are the ones from the notes: $35 base bill, $15 per box of 20 units ($0.75/unit),
$1.25 deducted per unfulfilled unit, 5 to 20 units wanted across 2 to 6 dishes with a max of 10 of
any one item, 2 to 5 dirty plates per table visit, 4 plates carried, 50 plates in the dishwasher.
All of them live in `EconomyConfig` and can be tuned in the Inspector.

### Opening the project

1. Install **Unity 6000.0 LTS** (the project was authored against `6000.0.47f1`; any 6000.0.x
   should open it, and Unity Hub will offer to upgrade the version string).
2. Open the folder in Unity Hub. On first import Unity will resolve the packages in
   `Packages/manifest.json`:
   - `com.unity.cloud.gltfast` for native `.glb` loading
   - `com.unity.ai.navigation` for the runtime NavMesh bake
   - `com.unity.inputsystem` for input (the player code also falls back to the legacy Input Manager)
   - `com.unity.ugui` for the HUD
3. If Unity asks whether to enable the new Input System backend and restart, say **Yes**. (The
   player scripts compile and run either way.)
4. Open `Assets/Scenes/BuffetDemo.unity` and press **Play**.

If the scene file ever gets mangled, regenerate it with **Buffet Sim > Create Demo Scene**. The
scene only contains one object, `Demo Scene Builder`; everything else is built from primitives at
runtime by `DemoSceneBuilder`, so the level is reproducible from code until real art lands.

**Buffet Sim > Create Default Config Assets** turns the runtime defaults into real
`ScriptableObject` assets under `Assets/Data` (economy numbers, the food catalog, one asset per
food, and a base material) and assigns them to the builder in the open scene. Do this once you want
to tune values in the Inspector or make a standalone build (the base material guarantees the lit
shader ships with the build).

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump |
| E | Interact with what you're looking at |
| Q | Drop what you're holding (plates break, food is lost) |
| Esc | Free the cursor; click to lock it again |

### The loop

1. Customers spawn on the sidewalk, line up at the register and wait for a free table.
2. They visit each tray they rolled, take what's there, and walk to their table.
3. Watch the `!` above a tray: it means fewer than 5 units are left.
4. Walk to the **storage cooler** in the kitchen, press E on a box to buy 20 units for $15
   (money out), then press E on the matching buffet tray to fill it to 20.
5. When a customer leaves they pay at the register (money in) and leave 2 to 5 dirty plates on
   the table. The table's light turns yellow; press E to pick up plates (4 at a time), carry them
   to the **dishwasher** and press E to load it. A table with plates on it can't be reused.
6. The customer's bill is $35 minus $1.25 per unit you couldn't serve; a customer who got nothing
   at all leaves without paying. Customers who wait too long in line walk out.

### Dropping in real models

`GlbModelLoader` looks for `.glb` files under `Assets/StreamingAssets/Models/` and replaces the
placeholder primitives when it finds them:

- `Models/player.glb` for the player body
- `Models/customer.glb` for customers

Place the files, press Play, and the placeholders are hidden. Nothing else needs to change.

## Project layout

```
Assets/BuffetSim/Runtime
  Core/         GameEvents (event bus) and the message structs
  Economy/      EconomyConfig (ScriptableObject), CustomerBill, EconomyLedger
  Food/         FoodDefinition, FoodCatalog (ScriptableObjects)
  Buffet/       BuffetTray (20 units of one food), IFoodSource, BuffetFloor registry
  Customers/    CustomerOrder (the RNG), CustomerAgent (state machine), CustomerQueue, CustomerSpawner
  Tables/       DiningTable (reservation + dirty plates)
  Stations/     StorageBox, Dishwasher, TrashCan
  Player/       PlayerController, PlayerInteractor, PlayerInventory, PlayerHandVisual, InputReader
  UI/           HudController, FloatingText, Billboard
  Models/       GlbModelLoader (gltfast)
  Bootstrap/    DemoSceneBuilder, PrimitiveFactory, MaterialLibrary
Assets/BuffetSim/Editor
  BuffetSimMenu  "Buffet Sim" menu: Create Demo Scene, Create Default Config Assets
```

Systems talk through `GameEvents` rather than holding references to each other, per CLAUDE.md:
customers publish receipts, stations publish purchase requests, the ledger answers them, and the
HUD only listens.

## Not in this demo (yet)

Random events, robberies, dine and dash, to-go orders, cooking minigames, multiplayer, fortune
cookies, recipes, the fountain, customer satisfaction and the day/night cycle are all out of scope
for this first pass. The event bus and the ScriptableObject configs are there so those can be added
without rewiring the systems above.
