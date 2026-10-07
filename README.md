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
  building. The full list is in [Chaos events](#chaos-events) below.

The third pass fills in the rest of the notes that fit a single-player demo:

- **Cooking**: the cooler now sells raw boxes. Carry one to the matching cooker in the kitchen: the
  **deep fryer** (hold E to lower the basket), the **wok** (press E each time the gauge crosses the hot
  zone; miss and a unit burns), the **steamer** and the **rice cooker** (press E and wait). A green light
  means done; leave it under the lamp and it burns a quarter of the batch every 30 s. Take the cooked
  tray to the buffet, or Q sends it to the cooler's cooked pile.
- **Phone to-go orders**: the phone on the front desk rings; press E to answer, read the ticket at the top
  right, grab a to-go box from the stack, scoop from the trays (max 3 of one item, 10 per box) and drop
  the box on the pickup rack by the door. Missed calls and late orders cost satisfaction.
- **The slot machine, fortune cookies, the wall of fortune and the fountain**: $5 a pull ($3 at happy
  hour, 3 to 5 PM), payouts in quarters, and sometimes a fortune cookie. Press **R** to crack one: 40 fortunes that
  help, hurt, or start an event; every new one gets pinned on the wall, and pinning all 40 spills the
  machine's cash box. Customers toss quarters in the fountain; scoop it out after close.
- **The maintenance shelf and repairs**: lightbulbs, duct tape, the wrench and a glass pane for the
  events that break things; hold E with the right tool to fix a lamp, a wall, a window or a pipe.
- **The apron pocket**: rocks, dodgeballs, cigarettes and cookies you pick up off the floor. **Tab**
  selects one, **left click** throws or uses it. A rock homes on whoever deserves it.
- **Robberies** on the notes' table (every 15 to 20 paying customers, 25%, once a day), a **debug panel**
  on F1 for playtesting, and twenty-two more chaos events.

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
| E | Interact with what you're looking at (trays, cooler, cookers, tables, dishwasher, phone, slot machine, tackle a dasher, catch a rat, mop a spill, deck whoever needs decking) |
| Hold E | Jobs with a bar: lower the fryer basket, fix a lamp, tape a hole, fit a window, tighten a pipe, scoop the fountain, speak to the manager |
| Q | Drop what you're holding (plates break, food is lost); at a cooker, take the batch back out; at the cooler, buy a raw box |
| Tab | Pick the next item in the apron pocket |
| Left click | Throw or use the selected pocket item (rock, dodgeball, cigarette, cookie) |
| R | Crack a fortune cookie |
| Esc | Free the cursor; click to lock it again |
| F1 | Debug panel: start any event, ring the phone, add money, skip time, spawn customers and items |

### The loop

1. Customers spawn on the sidewalk, line up at the register and wait for a free table.
2. They visit each tray they rolled, take what's there, and walk to their table.
3. Watch the `!` above a tray: it means fewer than 5 units are left.
4. Walk to the **storage cooler** in the kitchen. E takes a cooked tray if the pile has one; Q buys a raw
   box for $15 (money out). Carry the raw box to the cooker whose name is on it, cook it (see Controls),
   take the cooked tray out when the light turns green, and press E on the matching buffet tray: it
   pours what the tray has room for, up to 20, and the rest stays in your hands. (The notes say a tray
   takes whole batches only; whether to enforce that is an open design question.)
5. When a customer leaves they pay at the register (money in) and leave 2 to 5 dirty plates on
   the table. The table's light turns yellow; press E to pick up plates (4 at a time), carry them
   to the **dishwasher** and press E to load it (a full machine runs itself first). A table with
   plates on it can't be reused.
6. The customer's bill is $35 minus $1.25 per unit you couldn't serve; a customer who got nothing
   at all leaves without paying. Customers who wait too long in line walk out.
7. Watch for a red `!!` over a customer who just finished eating: they're about to run. Sprint, look
   at them and press E to tackle; walk over the coins to collect the bill.
8. When the phone rings, answer it, pack the order in a to-go box and leave it on the pickup rack.
   A box with none of the order in it is handed straight back.
9. The top of the screen shows the day timer and the satisfaction value; an event banner appears
   when something starts and tells you the outcome when it ends. At 0:00 the doors close, stragglers
   finish, the lights go off and the receipt shows the day's take and your cut. Your cut goes in your
   wallet, which is also what the slot machine takes. The next day opens on its own.

### Chaos events

Twenty on the random roll, one on the robbery counter, five only a fortune can start. Each one is a
`ChaosEvent` asset (tunables) plus a runner; the catalog in `ChaosEventCatalog.CreateDefault()` is the
only place they are listed.

| Event | What happens | What you can do |
| --- | --- | --- |
| Business is booming | Half the line turns into suits | Nothing; they pay 1.5x |
| Rat summoner | A hooded figure chants three rats into the room | E to catch a rat, E again to toss it out for the bounty |
| Spill | A kid knocks over the sweet and sour: three puddles | E to mop; don't run through one carrying plates |
| Leprechaun | Robs the till and wanders | E or a rock to deck him; the gold sprays out |
| Pitcher boy | A blue pitcher comes through the wall, leaves spills, money and a hole | Mop, pocket the cash, tape the hole with duct tape |
| Rock through the window | A rock (or a can of beans) comes through the window and flattens a diner | Keep the rock; fit a glass pane before the draft costs you |
| Clown drain | A gloved hand comes up out of the drain under a table and takes the diner | Nothing; the bill pays itself and the table is clean |
| Cigarette man | Walks in, makes a noise, drops a pack and leaves | Pocket the cigarettes: stand still 5 s, +20% speed for a minute |
| God damn Mongolians | Riders empty a few trays and leave | Nothing; insurance pays cost |
| Reddit moderator | A diner in a fedora explains authenticity, satisfaction drains | Knock him out for double his bill, then throw him out |
| Booth lamp | One table goes dark, the customer under it stops eating | Change the bulb (hold E) for +10% on their bill |
| Ceiling tile | A tile lands on a customer | Tape it back up before their lawyer calls |
| Robbery | A man in a ski mask grabs a cut of the till and runs | Tackle him (E) or a rock; the take comes back plus 20% |
| Alien abduction | A saucer takes you and drops you at the door | Nothing; 30 s of your life |
| Health inspector (day 2+) | Writes up the nearest unmopped spill | Mop first; hand him 5 cooked units for an A |
| Tupperware guy | Drains a tray every 30 s into cargo shorts | E or a rock: he pays and the tray comes back |
| Speak to the manager | Someone at the register wants the manager; you are the manager | Hold E 20 s to listen: they pay 1.25x |
| Dinosaur kids | 6 to 12 kids in costumes run laps; dodgeballs roll in | Dodgeball (left click) sends one home crying |
| Pipe burst | The restroom toilet lets go; water creeps across the floor | Wrench, hold E; then mop |
| Parade dragon | Torches one tray, eats off the next, does laps | E to pull the tarp off; it sells for $20 at the register |
| Mystery meat | A tray turns, grows teeth and scares the line | Three hits (E or rocks); the salvage pays 130% |
| The cousin (fortune) | A silent cousin clears tables for three minutes | Nothing; enjoy it |
| Cookie factory (fortune) | 200 blank cookies block the front door | Scoop them 20 at a time into the trash |
| Lucky number 8 (fortune) | $88 in quarters across the floor, 8 walk-ins | Grab the quarters first |
| Grandma (fortune) | Someone's grandmother serves the wrong things | Set a plate of 5 cooked units down for her |
| The stranger (fortune) | A man in a windbreaker pockets every bill at the register | Knock him out; it all falls out, plus $20 |

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
  Core/         GameEvents (event bus) and the message structs and request objects
  Economy/      EconomyConfig (ScriptableObject), CustomerBill, EconomyLedger, StoreReputation (satisfaction)
  Food/         FoodDefinition (incl. which cooker makes it), FoodCatalog (ScriptableObjects)
  Buffet/       BuffetTray (20 units of one food, to-go scooping), IFoodSource, BuffetFloor registry
  Customers/    CustomerOrder (the RNG), CustomerAgent (state machine, dine and dash, knock-outs, holds), CustomerQueue, CustomerSpawner
  Tables/       DiningTable (reservation + dirty plates + the lamp)
  Stations/     StorageBox (cooked pile + raw boxes), CookingStation (fryer, wok, steamer, rice cooker), Dishwasher, TrashCan,
                SupplyItem (the maintenance shelf), CashRegister
  ToGo/         PhoneOrderService (the phone and the ticket), ToGoBoxStack, PickupRack
  Gambling/     SlotMachine, FortuneCatalog + FortuneTeller (the 40 fortunes and their effects), FortuneWall, Fountain
  Day/          DayClock (open, last call, closing, closed, next day)
  Items/        CoinPickup, PocketPickup (rocks, dodgeballs, cigarettes, cookies), ThrownObject, FoodTrayPickup
  Interaction/  IInteractable, IHoldInteractable, ISecondaryInteractable (Q), IThrowTarget
  Events/       ChaosEvent + ChaosEventRunner base classes, ChaosEventCatalog, ChaosEventScheduler, RobberyScheduler,
                ChaosActors, WanderingNpc, EventActor, RepairPoint, BreakablePanel, and one file per event
  Player/       PlayerController, PlayerInteractor, PlayerInventory (hands), PlayerPocket + PlayerThrower (apron pocket),
                PlayerEffects (fortunes, aliens, cigarettes), PlayerHandVisual, InputReader
  UI/           HudController, FloatingText, Billboard
  Debugging/    DebugTools (the F1 panel)
  Models/       GlbModelLoader (gltfast, runtime StreamingAssets loading)
  Bootstrap/    DemoSceneBuilder, PropLibrary, PrimitiveFactory, MaterialLibrary, LightingDirector
Assets/BuffetSim/Editor
  BuffetSimMenu  "Buffet Sim" menu: Create Demo Scene, Create Default Config Assets
Assets/Resources/Models   Converted Meshy props (.glb)
Tools/meshy_to_glb        FBX -> decimated GLB converter for Meshy exports
```

Systems talk through `GameEvents` rather than holding references to each other, per CLAUDE.md:
customers publish receipts, stations publish purchase requests, the ledger answers them, the
HUD only listens. Anything that needs an answer (a purchase, a theft, a wallet spend, a to-go
delivery) is a request object that travels over the bus and comes back filled in.

### Adding a chaos event

Subclass `ChaosEvent` (a ScriptableObject holding the tunables) and `ChaosEventRunner` (the
MonoBehaviour that plays it out: spawn actors under the runner, `Finish(resolved, outcome)` when it's
over, clean up in `Abort()` for day end). Add it to `ChaosEventCatalog.CreateDefault()` or to a
catalog asset assigned to the builder's **Chaos Catalog** slot. Runners get a `ChaosEventContext`
(door, register, restroom, buffet line, dishwasher, floor bounds, the queue, the live customers, the
player transform) and talk to the rest of the game only through `GameEvents`. `EventActor` gives a
spawned person an E prompt and a throw target in one line; `RepairPoint` is a hold-E job that needs a
tool from the shelf. An event with weight 0 never rolls; the fortune teller, the robbery counter and
the debug panel start those by id with `GameEvents.RaiseChaosEventRequested`.

## Not in this demo (yet)

Multiplayer, the AI workers, recipes and secret ingredients, store and player progression, the strip
mall, the gun and the melee weapons, decorations and real audio (every event notice carries an
"(audio cue: ...)" hint where a sound should go). The event bus, the ScriptableObject configs and the
chaos event framework are there so those can be added without rewiring the systems above. The design
doc's "What the demo has now" section lists the suggested build order.
