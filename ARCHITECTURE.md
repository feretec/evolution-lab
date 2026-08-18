# Evolution Lab — Prototype 2 Architecture

This document turns `PROJECT_SPEC.md` into the current GameObject/C# implementation while keeping the long-term ecology and high-performance replacement seams visible.

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

EvolutionSimulation ──> EvolutionEngine ──> offspring genomes / cycle reports
        │                         │
        ├── EnvironmentController ──> EnergyResource entities
        ├── EvolutionLabUI        └── SimulationHistory
        └── lifecycle / selection / camera / input
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

The prototype uses `FixedUpdate` for controller actuation and life accounting so physics remains the source of movement. Runtime-only age, energy, metabolism, reproduction cooldown, offspring count, and death reason live here; they are not written into `CreatureGenome`. The class must not contain population selection or UI layout logic.

### `Brain`

Runtime evaluator for the brain gene. The first prototypes use a small tanh feed-forward network with fourteen inputs: ten locomotion observations plus energy ratio, nearest-resource direction x/z, and resource proximity. It retains up to eight actuator outputs. The fixed output ceiling and expanded input shape are prototype constraints; the genome still owns the values so future variable topology can replace them without changing `CreatureGenome`'s role.

### `EvolutionEngine`

Pure-ish population logic. It initializes founders, ranks evaluation results, computes survival/energy statistics, keeps elites, mixes compatible parent genes, mutates children, advances ecology cycles, and appends cycle records to `SimulationHistory`. `CreateOffspring` is the natural-birth boundary and `RecordEcologyCycle` records the live population. The older `BreedNextGeneration` method remains for Prototype 1 compatibility.

The engine does not instantiate GameObjects and does not know about cameras or UI. Genome/lineage changes are separate from runtime life state, so a future ECS/Burst simulation can reuse the same data boundary.

### `EnvironmentController`

Creates the flat ground and owns environment-level setup. It spawns deterministic, replenishing `EnergyResource` entities and answers only physical queries: nearest available resource position and energy consumed within a radius. It exposes no semantic target to the organisms. Future terrain, water, obstacles, and movable objects should be added here or in separate environment components.

### `EnergyResource`

Runtime environment entity with an energy amount, trigger collider, renderer state, and respawn timer. Consumption is resolved by `EnvironmentController` using physical distance so the organism is not handed a goal label or a special “food” behavior.

### `SimulationHistory`

Stores cycle-level records and bounded per-individual genome snapshots in a lightweight in-memory form. Each evaluated individual keeps its genome ID, parent IDs, generation, morphology counts, survival fitness, energy, age, offspring count, alive/death status, and a cloned genome, so an active or extinct creature can resolve a primary ancestry chain without depending on destroyed GameObjects. Generation records are capped at 2000 and individual snapshots at 8192 for long observation runs. The bounded observation history serializes lifecycle data as JSON but does not serialize live GameObjects, physics state, resource timers, or the engine's random stream.

### `EvolutionSimulation`

Scene-facing coordinator. It boots the engine, spawns and destroys embodiments, isolates lanes, completes evaluation windows, applies time controls, handles world clicks, and publishes data to `EvolutionLabUI`.

### `EvolutionLabUI`

Prototype presentation layer implemented with runtime IMGUI. It renders ecology statistics, a best/average survival history graph, time controls, ecology-cycle skip controls, life/reproduction tuning, selected-individual lifecycle details, a compact ancestry chain, historical-genome preview controls, and history archive save/load controls. It never mutates a genome directly.

## 3. Prototype genome model

The body gene list is an ordered tree. Entry `0` is the root. Every later entry has a `parentIndex` smaller than its own index, which makes building and repairing the topology deterministic. Removing the last part preserves this invariant; adding a part chooses an existing parent.

The brain uses fourteen observations in this phase: the original ten locomotion observations (root velocity, angular state, tilt/height, aggregate joint state, a phase oscillator, and a constant bias) plus energy ratio, nearest-resource direction x/z, and resource proximity. Eight hidden units feed eight possible actuator outputs. Actual joints use the first outputs in joint order. This mapping is intentionally documented as a replaceable prototype limitation rather than a species/behavior rule. `CreatureGenome` schema version 2 repairs old ten-input genomes into the expanded array shape.

## 4. Physics model

- Each body gene becomes a scaled cube with a `Rigidbody` and `BoxCollider`.
- Each non-root part receives a `ConfigurableJoint` connected to its gene parent.
- Prototype 1 locks all three linear axes, limits the primary angular axis, and locks the other angular axes for stability. The builder clamps each attachment point inside the parent box and derives both joint anchors from the same world-space point, so mutated length/thickness and angled branches cannot create a visible gap. Self-collision is explicitly disabled within each creature; solver iterations and a bounded projection envelope are tuned to absorb mutated spawn poses without large corrective impulses. The joint type leaves a seam for future multi-axis joint genes.
- Joint limits, drive strength, target angular speed, damping, and the initial settling window are tunable from `EvolutionSimulation`.
- A neural output controls target angular velocity; no hand-authored walking gait is supplied.
- Bodies are spawned in independent lanes and cross-creature collisions are ignored. This is a temporary experimental control that makes displacement attributable to the individual. It can be removed when ecology is introduced.

## 5. Evolution model

1. Founder genomes are varied in body count, branch direction, dimensions, joint limits, and brain weights.
2. The simulation evaluates each embodiment for a fixed duration.
3. Fitness is the greatest forward displacement from the lane's start position.
4. The engine records best/average fitness and lineage metadata.
5. A small elite set is copied; remaining children mix compatible genes from two high-ranked parents and mutate both body and brain values.
6. In the fixed locomotion path, the next generation is rebuilt from data, so no physical object survives as hidden genetic state.

Prototype 2 adds a separate natural cycle:

1. `EnvironmentController.Tick` respawns available resources.
2. Each live `Creature` consumes nearby resource energy, pays metabolism and movement cost, and ages.
3. Death removes the embodiment and its genome from the live engine population while recording the lifecycle snapshot.
4. Reproduction spends parent energy, creates a crossed/mutated child genome, and spawns a child until carrying capacity is reached.
5. `RecordEcologyCycle` records the live population and advances the observation cycle without forcing a fixed survivor count.

## 6. Presentation and input

The runtime bootstrap attaches `EvolutionSimulation` to a generated root object after `SampleScene` loads. It configures the existing camera/light and creates the ground, avoiding a large serialized scene diff during the blank-template phase. The camera is perspective-based and receives a separate `FreeCameraController`: WASD moves, Q/E move vertically, right-mouse drag looks, and the mouse wheel dollies. Camera input is unscaled; pointer-based camera input is disabled over the IMGUI panels so observation remains possible while paused.

The UI uses Unity IMGUI so the prototype does not need a second input-action asset or serialized Canvas prefab. World selection uses the existing Input System mouse position and a physics raycast. The controls panel can be scrolled and exposes cycle interval, life/reproduction, and joint drive tuning values at runtime. Selecting an individual exposes energy, age, offspring/status, and a Follow/Unfollow camera command; following stores a camera offset relative to the selected root body and remains independent of simulation time scale. The history graph and ancestry display consume `SimulationHistory` snapshots rather than live scene references. A selected ancestry record can be rebuilt as a non-physical observation preview, while the live population remains responsible for evaluation. JSON archive load is intentionally history-only; it does not resume the current physics world, resource timers, or random sequence. This is a presentation choice only; a future UGUI/UI Toolkit front end can consume the same simulation snapshots.

## 7. Known prototype constraints and replacement seams

| Prototype constraint | Why it exists now | Future replacement seam |
| --- | --- | --- |
| Fixed-count generation path | Keeps the locomotion baseline reproducible | Natural `CreateOffspring`/`RecordEcologyCycle` path |
| Displacement fitness | Isolates locomotion | Fitness/effects system driven by environment and energy |
| Flat ground | Keeps physics debugging bounded | `EnvironmentController` and environment entities |
| Fixed brain input/output shape | Avoids dynamic tensor allocation in first pass | Variable brain graph/actuator mapping in `BrainGene` |
| Configurable joints and GameObjects | More drive/axis flexibility while remaining inspectable | Multi-axis joint genes or alternate `CreatureBuilder` backend |
| IMGUI | No prefab/scene/UI asset setup | Separate view model plus UGUI/UI Toolkit |
| Lane collision isolation | Prevents population interference | Remove when interaction/predation becomes a selection pressure |
| Simplified resource and reproduction rules | Makes natural population change observable before predation | General effects, mating, predation, and species/lineage analysis |

Any code using one of these constraints should include a nearby TODO or comment when the assumption is not obvious.

## 8. Final runtime integration

The current scene still uses the small GameObject bootstrap, but the simulation boundary now contains the major Final observation loop:

```text
CreatureGenome (body + brain weights + continuous ecology traits)
        │
        ├── Brain ───────────────> motor / interaction intent
        ├── CreatureBuilder ─────> Rigidbody + ConfigurableJoint embodiment
        └── SimulationHistory ───> events / ancestry / extinct branches

EnvironmentController ──> EnergyResource + neutral EnvironmentFeature colliders
EcologyInteractionSystem ──> generic proximity / avoidance / energy transfer
NaturalHistoryCatalog ─────> post-hoc lineages and morphotype summaries
EvolutionLabUI ────────────> observation, time, camera, archive and world controls
```

`EcologyGene` is deliberately continuous. `predationDrive` is not a predator type; it participates with body mass, defense, sensor range, brain output, distance, and energy state in the interaction calculation. The same code path can therefore produce pursuing, evasive, resource-oriented, or clustering phenotypes without introducing a role class.

`EcologyInteractionSystem.Observe` supplies only generic directions, distances, obstacle proximity, and nearby-body information. `Tick` records outcomes separately from the controller, which keeps the genome/body/brain boundary reusable for a later data-oriented simulation. The current collision-isolation switch is a physics stability control; it does not disable spatial encounters.

`SimulationHistory` now serializes `EvolutionEventRecord` values alongside generation and individual records. `NaturalHistoryCatalog` derives lineage groups by walking parent IDs with cycle protection, and derives morphotype keys from morphology, brain magnitude, and ecology traits after the simulation has produced records. This is classification as an observation layer rather than a fixed inheritance hierarchy.

`WorldSnapshotArchive` is the current persistence seam for live observation. It stores cloned genomes, live runtime state, poses, and the history JSON. Loading rebuilds GameObjects through `CreatureBuilder` and restores runtime state; resource timers and the random stream are regenerated rather than silently pretending to be deterministic. A future complete save system can add those environment/random-state fields without changing the genome schema.

## 9. Verification notes

- Unity MCP recompile completed with no C# errors after the Final integration.
- Play Mode was exercised with visible articulated creatures, resources, neutral physical features, UI statistics, births, spatial encounter counts, and history events.
- The live-world save path successfully wrote a snapshot from Play Mode.
- Console review found no new errors. The remaining warning is the existing Visual Studio integration UDP-port warning (`Unable to use UDP port 56382`), unrelated to the simulation scripts.
