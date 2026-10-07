# CLAUDE.md

Guidance for Claude Code (and any contributor) working in this repository.

## Project Overview

**Working title:** Buffet Chaos (placeholder)

A chaotic first-person strip-mall buffet simulator. The player experiences
the absurdity of an all-you-can-eat buffet from a first-person physics
perspective — restocking trays, dodging other patrons, and reacting to
escalating chaos events, all under a flat-rate entry economy that rewards
(or punishes) greed.

## Tech Stack

- **Engine:** Unity (LTS version — confirm exact version in `ProjectSettings/ProjectVersion.txt` before assuming)
- **Language:** C#
- **Physics:** Unity's built-in physics (`Rigidbody`/`PhysX`) for first-person interaction — no custom physics engine
- **Input:** Unity Input System package (new input system, not legacy `Input.GetKey`) unless the project is confirmed to still use the old system
- **Version control:** Git, with Unity-specific `.gitignore` (Library/, Temp/, Obj/, Build/, .vs/ excluded) and Git LFS for binary assets (models, textures, audio)

## Architecture Rules

The project must stay **decoupled and modular**. This is a hard constraint,
not a suggestion — the buffet's systems (trays, economy, chaos events, AI
patrons) are expected to grow independently and need to be testable/tunable
in isolation.

- **No monolithic "GodManager" classes.** Each system (Tray, Economy, ChaosEvent,
  PatronAI, PlayerController) owns its own state and exposes a narrow public
  interface.
- **Favor composition over inheritance.** Use interfaces and `ScriptableObject`-based
  configs over deep MonoBehaviour inheritance chains.
- **Communicate via events/messaging, not direct references.** Systems should
  not reach into each other's internals. Use C# events, UnityEvents, or a
  lightweight event bus so e.g. the Chaos system can trigger effects on the
  Economy or Tray systems without a hard reference.
- **Data-driven design.** Tray contents, prices, chaos event definitions, and
  patron archetypes should live in `ScriptableObject` assets, not hardcoded
  in scripts, so designers can tune the game without touching code.
- **Separate simulation from presentation.** Gameplay logic (tray depletion,
  economy math, event triggering) should be testable independently of
  animation/VFX/UI, which subscribe to state changes rather than drive them.
- **First-person physics mechanics** (grabbing, carrying, spilling, colliding)
  live in dedicated player-interaction scripts, decoupled from the systems
  they affect — the player controller triggers events/interfaces, it doesn't
  directly mutate tray or economy state.

## Core Game Scope

### Trays
- Buffet food is served in **batch trays**, each holding **20 units** of a
  given item.
- Trays deplete as patrons (and the player) take units. Food arrives in
  batches of 20 (a wholesale box, a cooked tray); a refill pours what the
  tray has room for, up to 20, and the rest stays in the player's hands
  (top-ups chosen over whole-batch-only refills on 2026-10-07).
- Tray state (current units, item type, empty/full/spilled) should be a
  self-contained component other systems can query and subscribe to.

### Economy
- **Flat-rate entry**: the player (and NPC patrons) pay one fixed price to
  enter and eat as much as they want. There is no per-item pricing during
  play.
- The core tension is volume vs. cost: profitability is driven by how much
  gets eaten/wasted per flat-rate entry, not by transactional pricing.
- Economy logic should track consumption/waste per session and expose it
  cleanly for UI, scoring, and chaos-event triggers to consume.

### Chaos Events
- Interactive, escalating events disrupt the buffet floor (e.g. spills,
  stampedes, tray collapses, health-inspector visits, food fights).
- Chaos events are **interactive**, not just scripted cutscenes — the player
  can influence, cause, or mitigate them via first-person physics
  interactions.
- Chaos events should be defined as data (ScriptableObjects) with triggers,
  effects, and resolution conditions, dispatched through the event
  system/bus rather than hardcoded into level scripts.

## Conventions

- Follow standard C# / Unity naming conventions (PascalCase for public
  members and types, camelCase for private fields, `_camelCase` or
  `m_CamelCase` per team preference — confirm which before large refactors).
- Prefer `[SerializeField] private` over public fields for Inspector-exposed
  state.
- Keep MonoBehaviours thin; push logic into plain C# classes where possible
  for testability.

## Notes for Claude

- Before adding a new system, check whether it can be expressed as a
  ScriptableObject config + a small MonoBehaviour/interface, per the
  decoupling rules above.
- Don't introduce direct cross-system references (e.g. ChaosEvent directly
  calling into TrayManager's internals) — route through the event
  system/bus.
- Tray batch size (20) and the flat-rate economy model are core design
  constraints — don't quietly change them when refactoring; flag any
  design-affecting change instead of assuming it's fine.

## Where things are (demo)

- `Assets/BuffetSim/Runtime` holds the gameplay code, one folder per system; `Assets/BuffetSim/Editor` holds the "Buffet Sim" menu.
- `Assets/Scenes/BuffetDemo.unity` contains a single `DemoSceneBuilder` object that assembles the level from primitives at runtime; regenerate it with **Buffet Sim > Create Demo Scene**.
- Private serialized fields use `camelCase`; private runtime fields use `_camelCase`.
- The event bus is `BuffetSim.Core.GameEvents`. Add a new event there rather than passing component references around.
- Chaos events live in `Assets/BuffetSim/Runtime/Events`: one `ChaosEvent` ScriptableObject subclass (tunables) plus one `ChaosEventRunner` MonoBehaviour per event; `ChaosEventScheduler` picks them by weight, per-day cap and cooldown from `EconomyConfig`. New events go in the catalog, never in the scheduler. Weight 0 means "only by request": the fortune teller, `RobberyScheduler` and the F1 debug panel start those through `GameEvents.RaiseChaosEventRequested(id)`, which answers false when the doors are closed or nothing started.
- Event helpers: `ChaosActors` (spawn an agent, sample the NavMesh, pick a customer), `WanderingNpc` (laps + one-shot trips with a callback that always fires), `EventActor` (an E prompt and a throw target on anything the event spawned), `RepairPoint` (hold E with a tool from the maintenance shelf), `BreakablePanel` (the window and the wall). Spawn everything under the runner's transform so `Finish`/`Abort` clean it up.
- Things the player holds are one load at a time in `Player/PlayerInventory` (tray, raw box, plates, to-go box, or a tool); small things go in the apron pocket (`Player/PlayerPocket`, thrown by `PlayerThrower`). Stations get the `PlayerInventory` and read/change it through its methods; nothing else touches it.
- Cooking is `Stations/CookingStation` (one per `CookerKind`; a `FoodDefinition` says which cooker makes it) fed by raw boxes from `Stations/StorageBox`, which also keeps the cooked pile. To-go orders are `ToGo/PhoneOrderService` (the phone, the ticket, the scoring) and `ToGo/PickupRack`; the two only meet through `ToGoDeliveryRequest` on the bus.
- Gambling is `Gambling/`: `SlotMachine` spends the player's wallet through `WalletSpendRequest`, `FortuneTeller` turns a cracked cookie into bus requests (player effects, order boosts, cooker speed, events by id) from the `FortuneCatalog`, `FortuneWall` counts pins, `Fountain` pays out at close.
- Anything that needs an answer (a purchase, a theft, a wallet spend, a delivery, an event by id) is a request object raised on the bus and filled in synchronously by exactly one listener; never read another system's balance directly. The ledger also publishes `CustomerCharged` with what a bill actually put in the till, which is what thieves listen to.
- No one has run this project in the Unity Editor yet. A C# compile against the Unity reference assemblies was the only check, so the first Play in Unity is the real runtime test.
- The day cycle (`Day/DayClock`), satisfaction (`Economy/StoreReputation`) and the ledger only ever meet through the bus; the HUD listens and never calls into anything, not even the player: `PlayerInventory`, `PlayerPocket` and `PlayerEffects` publish their one-line descriptions as `PlayerCarryChanged`, `PlayerPocketChanged` and `PlayerStatusChanged`.
- Meshy props are converted to `.glb` with `Tools/meshy_to_glb`, live in `Assets/Resources/Models/` and are placed by `PropLibrary` (missing files fall back to primitives). Only the character models (`player.glb`, `customer.glb`) go in `Assets/StreamingAssets/Models/`, where `GlbModelLoader` loads them at runtime. Both paths use com.unity.cloud.gltfast.
