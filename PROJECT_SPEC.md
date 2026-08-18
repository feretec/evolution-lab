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
- Revolute/hinge-style joints with genome-defined limits and drive strength.
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

## 6. Current project baseline (audited before implementation)

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
- The project root is not currently a Git worktree (`git status` reports that it is not a repository). This task does not initialize Git or delete generated folders.

## 7. Implementation policy for this phase

- Keep `SampleScene` as the build entry point for now to avoid a scene-registration omission. A runtime bootstrap creates the prototype world, so the template scene can remain recoverable.
- Put prototype code under `Assets/EvolutionLab/Scripts` and keep responsibilities separated even though the project currently has no asmdefs.
- Prefer small, explicit C# classes over speculative framework layers. Add a new abstraction only when it protects a current boundary or a stated future requirement.
- Mark prototype-only assumptions in comments/TODOs: fixed population, displacement fitness, flat ground, lane isolation, and fixed-size neural output mapping.
- Do not add food, predator, energy, species, or ecological behavior before the locomotion loop is observable and stable.

## 8. Verification policy

After changes, verify in this order:

1. C# compilation in Unity or the generated Unity project where available.
2. Opening `SampleScene` and entering Play Mode.
3. Visible multi-part organisms on the ground.
4. At least one generation transition and UI statistic update.
5. Pause/speed/skip controls and individual selection.
6. Console/log review for errors and warnings caused by the new code.

The current prototype is complete only when it is observable and interactable, not merely when data classes compile.

## 9. Current implementation status

Prototype 1 is implemented under `Assets/EvolutionLab/Scripts` with runtime bootstrapping from `SampleScene`.

- Genome-defined body-part trees, dimensions, joint limits, drive strength, and brain weights are implemented.
- Physical rigidbodies, colliders, configurable joints, neural actuation, forward-displacement fitness, selection, crossover, mutation, lineage IDs, and generation history are implemented.
- Runtime IMGUI exposes generation/population/best/average fitness, pause, x1/x10/x100 speed controls, generation skips, individual inspection, generation-duration controls, and joint-drive tuning.
- A perspective free camera is available for observation: WASD movement, Q/E vertical movement, right-mouse look, mouse-wheel dolly, and a reset-view command. Camera input is independent of the simulation time scale.
- Default evaluation duration is 20 seconds. Joint drive force, target angular speed, damping, and settling duration are serialized simulation parameters and the main values can also be adjusted while running.
- Unity `6000.5.8f1` Play Mode was exercised through multiple generations, including selection and fast-forward controls.
- Fixed-population displacement fitness, flat ground, lane isolation, and the fixed-size brain are still explicit Prototype 1 constraints; they are not the final ecology design.
