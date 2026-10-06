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
- Trays deplete as patrons (and the player) take units, and must be
  restocked in batches of 20 — no partial/arbitrary restock amounts.
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
- Chaos events live in `Assets/BuffetSim/Runtime/Events`: one `ChaosEvent` ScriptableObject subclass (tunables) plus one `ChaosEventRunner` MonoBehaviour per event; `ChaosEventScheduler` picks them by weight, per-day cap and cooldown from `EconomyConfig`. New events go in the catalog, never in the scheduler.
- The day cycle (`Day/DayClock`), satisfaction (`Economy/StoreReputation`) and the ledger only ever meet through the bus; the HUD listens and never calls into them.
- Meshy props are converted to `.glb` with `Tools/meshy_to_glb`, live in `Assets/Resources/Models/` and are placed by `PropLibrary` (missing files fall back to primitives). Only the character models (`player.glb`, `customer.glb`) go in `Assets/StreamingAssets/Models/`, where `GlbModelLoader` loads them at runtime. Both paths use com.unity.cloud.gltfast.
