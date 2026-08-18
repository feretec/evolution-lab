# Evolution Lab — Current Architecture

This document maps the long-term product defined in `PROJECT_SPEC.md` to the current Unity GameObject/C# implementation. The implementation is intentionally bounded, but its data contracts preserve a path to a faster simulation backend and richer evolution.

## 1. Runtime data flow

```text
CreatureGenome (pure inherited data, schema 6)
        │
        ├── body / joints / sensors / mouth ──> CreatureBuilder ──> Creature embodiment
        ├── BrainGene + learning-rule genes ──> Brain ───────────> motor + behavior intent
        └── lineage metadata ─────────────────> SimulationHistory / NaturalHistoryCatalog

EvolutionSimulation
        ├── EvolutionEngine ───────────> crossover, mutation, births, cycle reports
        ├── EnvironmentController ─────> resources, neutral geometry, water, movable objects
        ├── EcologyInteractionSystem ──> generic encounters, damage, energy transfer
        ├── WorldSnapshotArchive ──────> deterministic live-world persistence
        └── EvolutionLabUI / camera ───> observation and player controls
```

The UI only reads simulation state and sends commands. It does not choose parents, mutate genomes, assign ecological roles, or supply semantic goals to a brain.

## 2. Core boundaries

### `CreatureGenome`

Pure serializable inherited information. Schema 6 contains lineage identity, a variable ordered body-part tree, multi-axis joint geometry and limits, up to three positioned sensors, a positioned mouth, continuous ecological traits, neural parameters, an active hidden-neuron count, and inherited plasticity-rule parameters. It never references a `GameObject`, renderer, rigidbody, or UI object.

### `CreatureBuilder`

The only genome-to-embodiment translator. It creates rigidbodies, colliders, visible organ markers, and `ConfigurableJoint` connections with coincident world-space anchors. Self-collision is disabled inside an individual to keep articulated mutations stable. The same genome can later be consumed by a Jobs/Burst/ECS builder.

### `Creature`

Owns one lifetime: physical state, energy, age, movement cost, damage, offspring, controller observations, current intents, selection state, and death. It also creates the homeostatic learning signal from energy change, damage, actuator cost, and continued survival. Acquired neural state is runtime-only.

### `Brain`

A compact tanh controller with fixed-capacity arrays, 22 observation channels, 2–8 active hidden neurons, 8 motor outputs, and 4 ecological intent outputs. Inference combines inherited base weights with per-life fast weights and short-term memory.

Lifetime learning is a reward-modulated local plasticity rule:

1. Pre/post activations update eligibility traces.
2. A homeostatic signal reinforces or weakens recently eligible connections.
3. A moving reward baseline turns the absolute signal into an advantage signal.
4. Fast weights decay slightly to prevent permanent saturation.
5. Learning rate, trace decay, memory retention, fast-weight limit, reward scales, baseline rate, decay, and enable state are inherited and mutable.

Fast weights, eligibility traces, memory, and reward baseline are saved in live-world snapshots but are never copied into offspring genomes. Evolution therefore selects an inherited ability to learn while each individual adapts during its own life: a Baldwinian design.

This runtime does not depend on ML-Agents. ML-Agents remains useful later as a benchmark or offline experiment, but it is not the in-world lifetime-learning mechanism.

### `EvolutionEngine`

Owns inherited population state, deterministic random state, crossover/mutation, offspring creation, evaluation capture, and cycle reports. It has no scene, camera, or UI dependencies. The original fixed-generation locomotion path remains available as an experimental baseline; the ecology path uses energy, birth, and death to vary population size.

### Environment and ecology

`EnvironmentController` owns deterministic resources and neutral physical features: flat ground, walls/corridors, ramps, movable rigidbodies, and water volumes. Organisms receive geometry, proximity, contact, and internal state—not labels such as “hide here” or “eat this way.”

`EcologyInteractionSystem` uses a spatial hash to bound neighborhood work. Continuous inherited traits and neural intents determine pursuit, avoidance, social proximity, damage, and energy transfer. There are no fixed predator, prey, herbivore, or species classes.

### History and species analysis

`SimulationHistory` stores bounded generation, individual, lifecycle, learning, combat, and event records. Revision-aware caches avoid rebuilding catalogues when history has not changed. `NaturalHistoryCatalog` reconstructs ancestry, descendant branches, extinction, lineage summaries, and post-hoc species/morphotype groups.

`GenomeDistance` describes body topology, joint frames, organs, brain topology/weights, plasticity rules, and ecology traits in normalized categories. Candidate buckets limit representative comparisons; full genetic distance makes the classification decision. Species are therefore analysis results, never inherited role identifiers.

### Persistence

History archives are observation-only JSON. Live-world snapshots (schema 5) additionally preserve genomes, generation/cycle state, body-part transforms and velocities, resource transforms/timers, movable environment state, life state, combat counters, complete per-life brain state, and deterministic engine/environment random streams. Loading rebuilds embodiments through `CreatureBuilder` and then restores runtime state.

## 3. Morphology and physics invariants

- Body parts form an ordered tree: the root is index 0 and each later parent index precedes its child.
- Each non-root part has a `ConfigurableJoint`; linear motion is locked and all three angular degrees can be inherited within bounded limits.
- Parent and child anchors are derived from one world-space attachment point, preventing visible gaps.
- Joint axes are repaired to a valid orthogonal frame.
- Internal colliders do not collide with one another. Cross-individual collision isolation remains a runtime stability switch; generic ecology interactions work in either mode.
- A neural output controls each active joint channel. No gait is authored by hand.
- Maximums (12 body parts, 3 sensors, 8 motor channels) are explicit capacity limits, not morphology templates.

## 4. Evolution and lifetime learning

Two timescales operate together:

```text
within one life: observations → actions → outcome → homeostatic signal → fast-weight adaptation
between lives:   survival/reproduction → inherited genome crossover + mutation → next lineage branch
```

The acquired fast weights are deliberately reset for newborns. What can evolve is the base controller, morphology, sensors/organs, and the parameters that determine how quickly and stably the individual learns. This avoids Lamarckian leakage while allowing selection to favor useful plasticity.

Learning telemetry is captured before ecology-cycle records and before death cleanup. Individual, generation, lineage, and species summaries expose whether learning was enabled, the recent signal, and adaptation magnitude. These values make lifetime change observable rather than an invisible implementation detail.

## 5. Presentation and controls

The runtime IMGUI dashboard uses a compact default HUD so the world remains the focus. F1 or the Dashboard button opens the full responsive dashboard. It includes population/fitness/ecology statistics, Pause, x1/x10/x100 time controls, generation skip, physics/life tuning, render-off acceleration, history graph, events, lineage/species catalogue, learning telemetry, selected-individual details, ancestry preview, and save/load controls.

The free camera supports WASD, Q/E vertical movement, right-mouse look, mouse-wheel dolly, reset, and selected-individual follow. Camera controls use unscaled time and remain usable while paused.

## 6. Explicit replacement seams

| Current bound | Reason | Preserved replacement path |
| --- | --- | --- |
| GameObject/PhysX world | Inspectable playable implementation | Data-oriented builder and Jobs/Burst/ECS simulation |
| Fixed 22/12 neural channel capacity | Stable serialized tensors and cheap runtime | Variable graph/controller schema with channel descriptors |
| 2–8 active hidden neurons | Evolvable complexity without dynamic allocation | Larger or sparse neural topology |
| Local online plasticity | Works inside every individual without a trainer process | Neuromodulated recurrent controllers, meta-learning, or benchmark ML-Agents policies |
| Hand-authored physical world generator | Provides neutral selection pressure now | Procedural biomes and multiple parallel worlds |
| Runtime IMGUI | Zero prefab/scene coupling | UI Toolkit/UGUI view over the same simulation snapshots |
| Bounded history | Long runs cannot grow memory without limit | Indexed disk archive/database |

## 7. Verification contract

Every change that touches a genome or snapshot must retain legacy repair, deterministic reconstruction, finite physics values, and separation of inherited versus acquired state. Required checks are Unity compilation, all EditMode tests, Play Mode observation, console review, save/load restoration, time/render controls, and a multi-cycle soak run.
