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

The second pass adds the first slice of the "chaotic humor game" on top of that loop:

- A **business day**: 12 minutes on the clock, last call for the final minute, a closing grace period,
  lights off, an end-of-day receipt with the 60/40 store/player split, then the next day opens.
- **Customer satisfaction**: a 0–100 store value on the HUD. Customers who get everything push it up,
  short-changed plates, walkouts, slips and ignored events push it down, and it scales how fast
  customers arrive.
- **Dine and dash**: 25% of customers who finish eating stare at the door with a red `!!`, then bolt for
  the exit. Press **E** on a runner to tackle them; their bill plus 10% sprays out as coins that magnet
  into you when you walk near. Let them reach the sidewalk and the bill is gone.
- **Chaos events**: a data-driven scheduler (`ChaosEventCatalog` of `ChaosEvent` ScriptableObjects
  with weights, a per-day cap and cooldowns) rolls every 40 seconds at 25% while customers are in the
  building. Four events ship: **Business is booming** (half the line turns into suits paying 1.5x),
  the **Rat Summoner** (catch three rats and toss them out for a bounty before satisfaction drops),
  the **Spill** (three puddles: mop with E, slip through one while carrying and you drop everything,
  customers who slip cost a $10 bribe) and the **Leprechaun** (robs the till, wanders, deck him with E
  to get the gold back as coins).

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
| E | Interact with what you're looking at (trays, cooler, tables, dishwasher, tackle a dasher, catch a rat, mop a spill, deck the leprechaun) |
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
7. Watch for a red `!!` over a customer who just finished eating: they're about to run. Sprint, look
   at them and press E to tackle; walk over the coins to collect the bill.
8. The top of the screen shows the day timer and the satisfaction value; an event banner appears
   when something starts and tells you the outcome when it ends. At 0:00 the doors close, stragglers
   finish, the lights go off and the receipt shows the day's take and your cut. The next day opens
   on its own.

### Art: the Meshy models

The sample props made in Meshy (buffet food station, empty buffet tray, deep fryer body and basket,
stove base and knob) are in the scene. The raw Meshy exports are ~1.9 million triangles each, so
they were run through [`Tools/meshy_to_glb`](Tools/meshy_to_glb/README.md) to get ~60k-triangle
`.glb` files with 2K textures; those live in `Assets/Resources/Models/` (plain git files, 6 to 10 MB each) and
are imported by com.unity.cloud.gltfast. Raw Meshy exports should stay out of the repo or go through Git LFS.

`DemoSceneBuilder` places them through `PropLibrary`, which scales each model to a target size and
fits a collider:

- one **Buffet Food Station** per tray slot, scaled so its counter sits at 0.9 m (the sneeze guard
  has no collider so you can still reach the trays);
- an **Empty Buffet Tray** on each station holding the coloured food fill, and a small one in the
  player's hands when carrying food;
- the **Fryer** (body + basket) and **Stove** (base + three knobs) as kitchen set dressing.

If a model is missing from `Resources/Models`, the builder silently falls back to the primitive
version, so the project never breaks on missing art. To add a prop: convert it with the tool, drop
the `.glb` into `Assets/Resources/Models/`, and call `PropLibrary.Place("Name", ...)`.

### Dropping in character models

`GlbModelLoader` looks for `.glb` files under `Assets/StreamingAssets/Models/` at runtime and
replaces the placeholder capsules when it finds them:

- `Models/player.glb` for the player body
- `Models/customer.glb` for customers

Place the files, press Play, and the placeholders are hidden. Nothing else needs to change.

## Project layout

```
Assets/BuffetSim/Runtime
  Core/         GameEvents (event bus) and the message structs
  Economy/      EconomyConfig (ScriptableObject), CustomerBill, EconomyLedger, StoreReputation (satisfaction)
  Food/         FoodDefinition, FoodCatalog (ScriptableObjects)
  Buffet/       BuffetTray (20 units of one food), IFoodSource, BuffetFloor registry
  Customers/    CustomerOrder (the RNG), CustomerAgent (state machine, dine and dash, knock-outs), CustomerQueue, CustomerSpawner
  Tables/       DiningTable (reservation + dirty plates)
  Stations/     StorageBox, Dishwasher, TrashCan
  Day/          DayClock (open, last call, closing, closed, next day)
  Items/        CoinPickup (the money that sprays out of a tackled dasher or thief)
  Events/       ChaosEvent + ChaosEventRunner base classes, ChaosEventCatalog, ChaosEventScheduler,
                WanderingNpc, and the four events (BusinessIsBooming, RatSummoner, Spill, Leprechaun)
  Player/       PlayerController, PlayerInteractor, PlayerInventory, PlayerHandVisual, InputReader
  UI/           HudController, FloatingText, Billboard
  Models/       GlbModelLoader (gltfast, runtime StreamingAssets loading)
  Bootstrap/    DemoSceneBuilder, PropLibrary, PrimitiveFactory, MaterialLibrary
Assets/BuffetSim/Editor
  BuffetSimMenu  "Buffet Sim" menu: Create Demo Scene, Create Default Config Assets
Assets/Resources/Models   Converted Meshy props (.glb)
Tools/meshy_to_glb        FBX -> decimated GLB converter for Meshy exports
```

Systems talk through `GameEvents` rather than holding references to each other, per CLAUDE.md:
customers publish receipts, stations publish purchase requests, the ledger answers them, and the
HUD only listens.

### Adding a chaos event

Subclass `ChaosEvent` (a ScriptableObject holding the tunables) and `ChaosEventRunner` (the
MonoBehaviour that plays it out: spawn actors under the runner, `Finish(resolved, outcome)` when it's
over, clean up in `Abort()` for day end). Add it to `ChaosEventCatalog.CreateDefault()` or to a
catalog asset assigned to the builder's **Chaos Catalog** slot. Runners get a `ChaosEventContext`
(door, register, floor bounds, the queue, the live customers, the player transform) and talk to the
rest of the game only through `GameEvents`.

## Not in this demo (yet)

The remaining notes events, robberies, to-go orders, cooking minigames, multiplayer, fortune cookies,
recipes, the fountain and AI workers are still out of scope. The event bus, the ScriptableObject
configs and the chaos event framework are there so those can be added without rewiring the systems
above. The design doc's "What the demo has now" section lists the suggested build order.
