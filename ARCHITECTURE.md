# Evolution Lab — Prototype 1 Architecture

This document turns `PROJECT_SPEC.md` into a concrete first implementation while keeping the long-term boundaries visible.

## 1. Runtime flow

```text
CreatureGenome (pure data)
        │
        ├── BrainGene ──> Brain (runtime controller)
        │
        └───────────────> CreatureBuilder ──> Creature + Rigidbody/Collider/Joint objects
                                                    │
                                                    ├── physical observation
                                                    └── forward displacement result

EvolutionSimulation ──> EvolutionEngine ──> next generation genomes
        │                         │
        ├── EnvironmentController │
        ├── EvolutionLabUI        └── SimulationHistory
        └── selection/camera/input
```

The simulation owns the evaluation loop. The UI reads snapshots and sends commands; it does not select or mutate genomes itself.

## 2. Responsibility boundaries

### `CreatureGenome`

Pure, serializable genetic information. It contains:

- stable genome ID, parent ID, and generation;
- an ordered body-part tree (parents always precede children);
- dimensions, connection direction, local orientation, joint limit, mass, and drive strength;
- a fixed small brain shape for this prototype and its weight/bias arrays.

It provides cloning, validation/repair, crossover-compatible copying, and mutation. It must not reference a scene object, renderer, rigidbody, or UI.

### `CreatureBuilder`

The only class that translates a genome into a Unity embodiment. It creates the creature container, physical body parts, rigidbodies, colliders, renderers, and joints. It returns a configured `Creature` and the collider list used to isolate lanes.

The builder is deliberately replaceable: a future ECS/Burst builder can consume the same genome without changing lineage or selection code.

### `Creature`

Runtime embodiment and evaluation adapter. It owns the live rigidbodies/joints, constructs controller observations, applies brain outputs to joint drives, tracks start position and best forward progress, exposes an immutable evaluation result, and routes selection clicks.

The prototype uses `FixedUpdate` for controller actuation so physics remains the source of movement. The class must not contain population selection or UI layout logic.

### `Brain`

Runtime evaluator for the brain gene. Prototype 1 uses a small tanh feed-forward network with a fixed input count and up to eight actuator outputs. The fixed output ceiling is a prototype constraint; the genome still owns the values so future variable topology can replace it without changing `CreatureGenome`'s role.

### `EvolutionEngine`

Pure-ish population logic. It initializes founders, ranks evaluation results, computes best/average statistics, keeps elites, mixes compatible parent genes, mutates children, advances generations, and appends generation records to `SimulationHistory`.

The engine does not instantiate GameObjects and does not know about cameras or UI. Prototype 1 uses fixed population size and displacement fitness; those policies are isolated here for later replacement.

### `EnvironmentController`

Creates the flat ground and owns environment-level setup. It exposes no semantic target to the organisms. Future terrain, water, obstacles, and movable objects should be added here or in separate environment components.

### `SimulationHistory`

Stores generation-level records and event hooks in a lightweight in-memory form. It is intentionally present from Prototype 1 so future ancestry, extinction, and event views do not need to retrofit IDs into old code. Persistence/graph databases are out of scope for this phase.

### `EvolutionSimulation`

Scene-facing coordinator. It boots the engine, spawns and destroys embodiments, isolates lanes, completes evaluation windows, applies time controls, handles world clicks, and publishes data to `EvolutionLabUI`.

### `EvolutionLabUI`

Prototype presentation layer implemented with runtime IMGUI. It renders statistics, time controls, generation skip controls, and selected-individual details. It never mutates a genome directly.

## 3. Prototype genome model

The body gene list is an ordered tree. Entry `0` is the root. Every later entry has a `parentIndex` smaller than its own index, which makes building and repairing the topology deterministic. Removing the last part preserves this invariant; adding a part chooses an existing parent.

The brain uses ten observations in this phase: root velocity, angular state, tilt/height, aggregate joint state, a phase oscillator, and a constant bias. Eight hidden units feed eight possible actuator outputs. Actual joints use the first outputs in joint order. This mapping is intentionally documented as a replaceable prototype limitation rather than a species/behavior rule.

## 4. Physics model

- Each body gene becomes a scaled cube with a `Rigidbody` and `BoxCollider`.
- Each non-root part receives a `ConfigurableJoint` connected to its gene parent.
- Prototype 1 locks all three linear axes, limits the primary angular axis, and locks the other angular axes for stability; the joint type leaves a seam for future multi-axis joint genes.
- Joint limits, drive strength, target angular speed, damping, and the initial settling window are tunable from `EvolutionSimulation`.
- A neural output controls target angular velocity; no hand-authored walking gait is supplied.
- Bodies are spawned in independent lanes and cross-creature collisions are ignored. This is a temporary experimental control that makes displacement attributable to the individual. It can be removed when ecology is introduced.

## 5. Evolution model

1. Founder genomes are varied in body count, branch direction, dimensions, joint limits, and brain weights.
2. The simulation evaluates each embodiment for a fixed duration.
3. Fitness is the greatest forward displacement from the lane's start position.
4. The engine records best/average fitness and lineage metadata.
5. A small elite set is copied; remaining children mix compatible genes from two high-ranked parents and mutate both body and brain values.
6. The next generation is rebuilt from data, so no physical object survives as hidden genetic state.

Natural population dynamics are not implemented here. The engine's `BreedNextGeneration` boundary is where later survival, energy, reproduction, and death rules will replace fixed-count ranking.

## 6. Presentation and input

The runtime bootstrap attaches `EvolutionSimulation` to a generated root object after `SampleScene` loads. It configures the existing camera/light and creates the ground, avoiding a large serialized scene diff during the blank-template phase. The camera is perspective-based and receives a separate `FreeCameraController`: WASD moves, Q/E move vertically, right-mouse drag looks, and the mouse wheel dollies. Camera input is unscaled; pointer-based camera input is disabled over the IMGUI panels so observation remains possible while paused.

The UI uses Unity IMGUI so the prototype does not need a second input-action asset or serialized Canvas prefab. World selection uses the existing Input System mouse position and a physics raycast. The controls panel can be scrolled and exposes generation duration plus the main joint drive tuning values at runtime. This is a presentation choice only; a future UGUI/UI Toolkit front end can consume the same simulation snapshots.

## 7. Known prototype constraints and replacement seams

| Prototype constraint | Why it exists now | Future replacement seam |
| --- | --- | --- |
| Fixed population | Makes generation comparison easy | `EvolutionEngine` population/reproduction policy |
| Displacement fitness | Isolates locomotion | Fitness/effects system driven by environment and energy |
| Flat ground | Keeps physics debugging bounded | `EnvironmentController` and environment entities |
| Fixed brain output ceiling | Avoids dynamic tensor allocation in first pass | Variable brain graph/actuator mapping in `BrainGene` |
| Configurable joints and GameObjects | More drive/axis flexibility while remaining inspectable | Multi-axis joint genes or alternate `CreatureBuilder` backend |
| IMGUI | No prefab/scene/UI asset setup | Separate view model plus UGUI/UI Toolkit |
| Lane collision isolation | Prevents population interference | Remove when interaction/predation becomes a selection pressure |

Any code using one of these constraints should include a nearby TODO or comment when the assumption is not obvious.
