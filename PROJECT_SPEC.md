# Evolution Lab — Project Specification

## 1. Product vision

Evolution Lab is an artificial-life observation game in which body, brain, behavior, and ecological role evolve across generations. The player does not merely optimize a score; they watch a world produce its own natural history.

The long-term target is:

> A simulation game where bodies, brains, behavior, and ecological roles co-evolve, and the player can observe and inspect that history across generations.

The initial locomotion prototype is evidence toward that target, not the target itself. A prototype feature is successful only when it preserves a path toward emergent morphology, co-evolution, natural selection, lineage history, and an enjoyable observation UI.

## 2. Design invariants

These rules are higher priority than short-term implementation convenience.

1. `CreatureGenome` is pure genetic data, not a `GameObject`. It must be possible to clone, mutate, serialize, and rebuild an organism from it without keeping the original scene object alive.
2. Runtime embodiment is separate from genetic data. `CreatureBuilder` turns a genome into physical Unity objects; `Creature` owns the live embodiment; `Brain` evaluates a controller from the genome.
3. Body and controller are selected together. A body mutation is not evaluated independently from the brain that controls it.
4. Environment exposes physical properties rather than semantic instructions. Future rocks, water, hiding spaces, and movable objects must not be tagged with goals such as “hide here” or “use as defense.”
5. Selection must be able to move from prototype fitness toward survival, energy, reproduction, and death without replacing the genome/lineage model.
6. Simulation-facing code must not depend on UI presentation. Drawing can later be disabled or replaced by Jobs/Burst/ECS-oriented views.
7. Temporary prototype shortcuts must be explicitly marked and must not become assumptions in the long-term data model.

## 3. Long-term feature direction

### 3.1 Morphology

Genomes will eventually encode variable body topology and parameters including body-part count, size, length, thickness, connection position, joint count and direction, joint limits, appendages, sensors, organs, mass, and balance. No fixed “biped”, “quadruped”, “herbivore”, or “carnivore” class should be required for these forms to appear.

### 3.2 Brain and behavior

Controllers will eventually consume visual/distance signals, food and other individuals, contact, internal energy, and joint state. Outputs may drive joints, movement, feeding, and other actions. Brain topology or parameters must remain mutable genetic data so the body and controller can co-evolve.

### 3.3 Ecology and selection

The intended direction is a population whose size emerges from energy acquisition, survival, reproduction, and death. Predation, defense, escape, ambush, speed, and group-like behavior should be possible outcomes rather than fixed species roles.

### 3.4 Environment use

The environment will grow from a flat test surface into terrain, rocks, walls, narrow spaces, height differences, water, and movable objects. The physics of an object is part of the environment; its intended use is not supplied to the organism.

### 3.5 Natural history

Every lineage should retain ancestry, branching, extinction, and representative bodies/behaviors. Species are a later analysis or classification layer based on genetic distance and/or morphology, not a mandatory inheritance class.

### 3.6 Observation game

The player should be able to click an individual, inspect its age/generation/energy/body/sensors/behavior/parents/descendants/lineage, follow it with the camera, control time, inspect an ancestry tree, review major evolution events, and browse extinct lineages in an automatically generated natural-history catalogue.

## 4. Prototype 1 — Morphological Locomotion

### Purpose

Verify that a variable physical body and its neural controller can evolve together toward improved locomotion under selection.

### Environment

- One flat physical ground surface.
- No food, predators, reproduction, energy economy, or ecological roles yet.
- Independent lanes are used to keep individuals from interfering with one another while retaining physical rigidbodies and joints.

### Organisms

- A genome-defined tree of multiple physical body parts.
- Rigidbodies and colliders for each part.
- Configurable joints with genome-defined orthogonal axes, three angular limits, and drive strength. Linear axes remain locked so body parts stay physically attached while angular freedom can evolve.
- A small feed-forward neural controller whose weights are stored in the genome.
- A runtime `Creature` that can be destroyed and rebuilt from the genome.

### Evolution

- Generation 1 starts with varied founder genomes.
- At the end of an evaluation window, fitness is measured from forward displacement.
- Selection keeps high-performing parents, performs parent mixing where compatible, and mutates body and brain genes together.
- Population size is fixed for this prototype only. The evolution engine must leave a seam for future natural population changes.
- Lineage IDs, parent IDs, generation numbers, and generation summaries are recorded from the beginning.

### Expected observation

Generation 1 should contain many unstable or ineffective bodies. Across tens to hundreds of generations, the player should be able to observe different locomotion strategies such as sliding, rolling, crawling, or limb-like contact patterns, with statistics providing evidence that the population is changing.

### Minimum UI

- Current generation.
- Population count.
- Best and average fitness.
- Pause/resume.
- Simulation speed controls.
- Generation skip controls.
- Clickable individuals with genome ID, fitness, body-part count, joint count, generation, and parent ID.

## 5. Prototype 1 acceptance criteria

1. Opening the configured build scene starts a visible simulation without hand-created organism prefabs.
2. At least one generation can complete, spawn a new population, and update statistics without a compile/runtime error.
3. A clicked organism exposes the required genome and fitness information.
4. The displayed body is built from multiple physical parts and joints, not a single animated mesh.
5. Mutation can change both morphology and controller parameters, and a new generation keeps lineage metadata.
6. Pause, speed, and generation-skip controls affect the simulation.
7. Existing URP template assets remain usable; the implementation does not require a new input-action asset with conflicting names.
8. The project can be opened and compiled in Unity `6000.5.8f1` with no known compile errors.

## 6. Prototype 2 — Ecological Survival

Prototype 2 extends the locomotion experiment toward the long-term ecology target without introducing fixed species roles or predation yet.

### Purpose

Verify that a population can change through energy acquisition, metabolism, survival, reproduction, and death while retaining genome/brain/body co-evolution and lineage history.

### Environment

- The flat physical ground remains the temporary test surface.
- Replenishing `EnergyResource` nodes are physical environment entities. They expose position and energy availability; no “food-seeking” or semantic action is supplied to a creature.
- Resource placement is deterministic from the simulation seed, with initial resources distributed near founder lanes so the first ecology cycles are observable.

### Life cycle

- A live `Creature` owns runtime-only age, energy, metabolism, movement cost, maturity, reproduction cooldown, offspring count, and death reason.
- Energy is acquired from nearby resources and consumed by basal metabolism and movement.
- Starvation and maximum age remove an individual from the live population.
- Mature, energy-rich individuals reproduce with a nearby compatible live partner when possible; asexual fallback keeps the prototype observable when no partner is close.
- `EvolutionEngine.CreateOffspring` crosses and mutates parent genomes, while `RecordEcologyCycle` records the current live population. The old fixed-size `BreedNextGeneration` path remains as a compatibility seam for the locomotion prototype.
- Carrying capacity bounds population growth, but actual population size is determined by births and deaths rather than selecting a fixed number of survivors.

### Prototype 2 UI and history

- Statistics include cycle, population/carrying capacity, survival fitness, births/deaths, average energy, resource availability, and ecology status.
- Selected individuals expose energy, age, offspring count, and alive/death status in addition to morphology and lineage.
- Ecology-cycle history records average energy/age and birth/death counts. Individual history records retain lifecycle snapshots for ancestry and extinct-lineage observation.
- The current brain contract uses 22 observations and 12 outputs (8 motor and 4 behavior-intent channels). Older serialized genomes are repaired into the expanded fixed-capacity shape without changing lineage identity.

### Explicit limits

- Multi-world execution and a data-oriented simulation backend remain later phases; predation-like interaction, neutral terrain features, movable objects, water, and post-hoc species analysis are now present in the integrated runtime.
- Lane collision isolation remains temporarily enabled so Prototype 2 measures resource-driven survival without adding inter-individual physics as a confounding variable. It must be removed or replaced when interaction/predation becomes a selection pressure.
- History archive load remains observation-only. A separate live-world snapshot restores physics/life state, resources, acquired brain state, and deterministic random streams.

### Prototype 2 acceptance criteria

1. Resources are visible and can be consumed and respawned without compile/runtime errors.
2. Energy changes over time, starvation/age can kill individuals, and dead bodies leave the live population.
3. Mature individuals can create mutated offspring with parent IDs and a new lineage generation.
4. Population count is allowed to move below or above the initial population within carrying capacity.
5. Pause, speed, cycle interval, ecology skip, and life-tuning controls remain usable.
6. Cycle and individual history include lifecycle data and remain loadable from older Prototype 1.5 JSON archives.

## 7. Current project baseline (audited before implementation)

### Unity and rendering

- Unity Editor: `6000.5.8f1`.
- Render pipeline: Universal Render Pipeline `17.5.0`.
- PC and Mobile URP assets/renderers are present. Graphics settings point to the PC URP asset; quality settings contain platform-specific variants.
- Active color space is Linear.

### Existing assets and scenes

- `Assets/Scenes/SampleScene.unity` is the only build scene and contains the template camera, directional light, and global volume.
- `Assets/Settings` contains the URP template assets.
- `Assets/TutorialInfo` contains the template readme and editor helper.
- `Assets/InputSystem_Actions.inputactions` already exists; Prototype 1 should use the existing Input System package without introducing duplicate action names.
- No existing gameplay scripts, prefabs, ScriptableObjects, or asmdefs were found.

### Packages

The project already includes Input System, UGUI, AI Navigation, Timeline, Visual Scripting, Test Framework, and URP-related packages. Existing packages are not removed for Prototype 1; unused packages are a later cleanup decision.

### Repository hygiene

- `.gitignore` was missing and is added at the project root with Unity-generated folders/files excluded.
- The project is Git-managed on the `main` branch with the Unity-oriented `.gitignore`; generated folders remain excluded from version control.

## 8. Implementation policy for this phase

- Keep `SampleScene` as the build entry point for now to avoid a scene-registration omission. A runtime bootstrap creates the prototype world, so the template scene can remain recoverable.
- Put prototype code under `Assets/EvolutionLab/Scripts` and keep responsibilities separated even though the project currently has no asmdefs.
- Prefer small, explicit C# classes over speculative framework layers. Add a new abstraction only when it protects a current boundary or a stated future requirement.
- Mark prototype-only assumptions in comments/TODOs: flat ground, lane isolation, fixed-size neural output mapping, and the simplified resource/reproduction rules.
- Add ecological entities only through physical state and explicit simulation boundaries; do not encode future predator/prey or species roles as fixed classes.

## 9. Verification policy

After changes, verify in this order:

1. C# compilation in Unity or the generated Unity project where available.
2. Opening `SampleScene` and entering Play Mode.
3. Visible multi-part organisms on the ground.
4. At least one ecology-cycle transition and UI statistic update.
5. Pause/speed/skip controls and individual selection.
6. Console/log review for errors and warnings caused by the new code.

The current prototype is complete only when it is observable and interactable, not merely when data classes compile.

## 10. Current implementation status

Prototype 1 is implemented under `Assets/EvolutionLab/Scripts` with runtime bootstrapping from `SampleScene`.

- Genome-defined body-part trees, dimensions, joint limits, drive strength, and brain weights are implemented.
- Physical rigidbodies, colliders, configurable joints, neural actuation, forward-displacement fitness, selection, crossover, mutation, lineage IDs, and generation history are implemented.
- Runtime IMGUI exposes generation/population/best/average fitness, pause, x1/x10/x100 speed controls, generation skips, individual inspection, generation-duration controls, and joint-drive tuning.
- Prototype 1.5 adds a bounded per-individual genome history, a best/average fitness graph, primary-parent ancestry display, a Follow/Unfollow camera command for the selected individual, historical-genome preview reconstruction, and JSON save/load for the bounded observation archive.
- Historical previews are rebuilt from cloned `CreatureGenome` data as kinematic, non-colliding displays. History archive loading is observation-only; live-world snapshots are the resumable format.
- Prototype 2 adds deterministic energy resources, runtime metabolism and movement cost, age/starvation death, mature reproduction with crossover/mutation, carrying capacity, natural population fluctuation, ecology-cycle reports, lifecycle-aware individual history, and four ecological brain observations.
- Prototype 2 keeps runtime life state on `Creature`, not on `CreatureGenome`; genome data remains serializable and rebuildable. `EnergyResource` is an environment entity rather than a semantic “food” instruction.
- A perspective free camera is available for observation: WASD movement, Q/E vertical movement, right-mouse look, mouse-wheel dolly, reset-view, and selected-individual follow. Camera input is independent of the simulation time scale.
- Default evaluation duration is 20 seconds. Joint drive force, target angular speed, damping, and settling duration are serialized simulation parameters and the main values can also be adjusted while running.
- Unity `6000.5.8f1` Play Mode was exercised through multiple ecology cycles, x10 fast-forward, pause/resume, live individual selection, and lifecycle observation; the Unity Console showed no errors caused by the prototype.
- Fixed neural channel/body capacity, GameObject/PhysX execution, bounded history, and simplified resource/reproduction rules remain explicit implementation constraints; they are not ecological role definitions.

## 11. Final runtime implementation status

The runtime now crosses the Prototype 2 boundary into an integrated artificial-life observation world while preserving the genome/body/brain/history seams.

- `CreatureGenome` schema 6 contains mutable body topology, three-axis joint geometry, positioned sensors and mouth, continuous ecological traits, active neural topology, and inherited lifetime-plasticity parameters. They are traits, not fixed predator/prey or species classes.
- `BrainGene` receives twenty-two observations, evolves 2–8 active hidden neurons, and exposes twelve controller outputs (8 motor, 4 behavior intent). The original locomotion inputs remain stable at the front of the vector; generic nearby-body, threat, obstacle, organ, and ecological observations are appended for forward-compatible repair.
- Each individual performs online reward-modulated learning during its lifetime using eligibility traces, short-term memory, a moving homeostatic reward baseline, and decaying fast weights. The plasticity rule is inherited and evolves with body and base brain; acquired fast weights are not inherited.
- ML-Agents is not a runtime dependency. The current mechanism is custom online plasticity because learning must occur independently inside dynamically generated organisms; ML-Agents may later serve as an offline benchmark or experimental trainer.
- `EcologyInteractionSystem` resolves spatial encounters from inherited traits and neural outputs. Energy transfer, pursuit, avoidance, social proximity, damage, kills, and predation are emergent outcomes of continuous values. No organism is assigned a carnivore/herbivore class.
- `EnvironmentFeature` adds deterministic neutral walls, corridors, ramps, movable rigidbodies, and water to the world. Features expose physical state only; no hiding, defense, or food-use semantics are sent to controllers.
- `SimulationHistory` stores consequential natural-history events, learning telemetry, combat statistics, descendant queries, and a `NaturalHistoryCatalog` that derives lineage summaries, extinct branches, and bounded post-hoc genome-distance groups.
- The compact-first observation UI includes encounters, kills, physical features, lineages, extinct branches, species/morphotype analysis, recent events, lifetime-learning telemetry, ecological traits, neural intent, descendants, and save/load controls. F1 opens the full dashboard while the default HUD leaves the world visible.
- Live-world snapshot schema 5 restores current genomes, full body-part poses and velocities, generation/cycle, energy, age, offspring, combat counters, resource and movable-object state, acquired neural fast weights/traces/memory/reward baseline, deterministic random streams, and the observation archive.

The remaining explicitly bounded seams are fixed neural I/O capacity, a maximum body/organ budget, handcrafted physical world generation, bounded in-memory history, and GameObject/PhysX execution. Collision isolation between articulated individuals remains enabled by default as a stability guard and can be toggled; generic spatial encounters work independently. These are implementation boundaries, not ecological role definitions, and the genome/history contracts remain replaceable by Jobs/Burst/ECS and multi-world execution.

## 12. Current verification record

- Unity compilation succeeds on `6000.5.8f1`; all 47 EditMode tests pass.
- In Play Mode, lifetime-learning fast weights changed during a single life while inherited base arrays remained unchanged under tests.
- Live save/load restored generation and population in the running world, including the acquired brain-state contract.
- A render-off x100 soak advanced from generation 1 to generation 17, produced natural population decline/extinction, paused at extinction, and emitted no Unity Console errors.
- A clean `StandaloneWindows64` Player build succeeded through an ASCII junction (used to avoid a Unity Pipeline string-conversion bug with the Japanese project path), and a 10-second headless Player smoke run emitted no runtime errors.
