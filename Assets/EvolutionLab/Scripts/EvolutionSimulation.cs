using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionLab
{
    /// <summary>
    /// Scene-facing coordinator for the Prototype 2 ecological evolution experiment.
    /// </summary>
    public sealed class EvolutionSimulation : MonoBehaviour
    {
        [Header("Prototype 2 settings")]
        [SerializeField] private int populationSize = 24;
        [SerializeField] private float generationDuration = 20f;
        [SerializeField] private int randomSeed = 172903;

        [Header("Ecology")]
        [SerializeField] private int resourceCount = 36;
        [SerializeField] private float resourceEnergy = 30f;
        [SerializeField] private float resourceRespawnSeconds = 12f;
        [SerializeField] private float resourceConsumeRadius = 0.95f;
        [SerializeField] private float resourceIntakePerSecond = 14f;
        [SerializeField] private int carryingCapacity = 64;
        [SerializeField] private float initialEnergy = 62f;
        [SerializeField] private float offspringInitialEnergy = 40f;
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float metabolismPerSecond = 0.45f;
        [SerializeField] private float movementEnergyCost = 0.02f;
        [SerializeField] private float maxAgeSeconds = 60f;
        [SerializeField] private float maturityAgeSeconds = 4f;
        [SerializeField] private float reproductionEnergyThreshold = 78f;
        [SerializeField] private float reproductionCost = 42f;
        [SerializeField] private float reproductionCooldownSeconds = 8f;
        [SerializeField] private float reproductionRadius = 3.5f;

        [Header("Physics tuning")]
        [SerializeField] private float jointDriveForce = 110f;
        [SerializeField] private float jointTargetSpeedDegrees = 240f;
        [SerializeField] private float jointDamping = 8f;
        [SerializeField] private float settlingDuration = 0.35f;

        private readonly List<Creature> creatures = new List<Creature>();
        private EvolutionEngine engine;
        private EnvironmentController environment;
        private EvolutionLabUI ui;
        private Camera mainCamera;
        private FreeCameraController freeCamera;
        private Creature selectedCreature;
        private float evaluationElapsed;
        private float simulationSpeed = 1f;
        private bool paused;
        private int pendingGenerationSkips;
        private bool skipMode;
        private float speedBeforeSkip = 1f;
        private float initialTimeScale = 1f;
        private bool initialized;
        private readonly List<IndividualHistoryRecord> selectedAncestry = new List<IndividualHistoryRecord>();
        private readonly List<Creature> pendingDeaths = new List<Creature>();
        private readonly List<Creature> reproductionCandidates = new List<Creature>();
        private Creature previewCreature;
        private int ancestryCursor;
        private string historyStatus = string.Empty;
        private string historyArchivePath;
        private int birthsThisCycle;
        private int deathsThisCycle;
        private string ecologyStatus = "Population is adapting to the environment.";

        public int Generation
        {
            get { return engine == null ? 0 : engine.Generation; }
        }

        public int PopulationCount
        {
            get { return creatures.Count; }
        }

        public float BestFitness
        {
            get { return engine == null || engine.LastReport == null ? 0f : engine.LastReport.bestFitness; }
        }

        public float AverageFitness
        {
            get { return engine == null || engine.LastReport == null ? 0f : engine.LastReport.averageFitness; }
        }

        public int CarryingCapacity
        {
            get { return carryingCapacity; }
        }

        public int BirthsThisCycle
        {
            get { return birthsThisCycle; }
        }

        public int DeathsThisCycle
        {
            get { return deathsThisCycle; }
        }

        public float AverageEnergy
        {
            get
            {
                if (creatures.Count == 0)
                {
                    return 0f;
                }

                float total = 0f;
                for (int i = 0; i < creatures.Count; i++)
                {
                    if (creatures[i] != null)
                    {
                        total += creatures[i].Energy;
                    }
                }

                return total / Mathf.Max(1, creatures.Count);
            }
        }

        public int AvailableResourceCount
        {
            get { return environment == null ? 0 : environment.AvailableResourceCount; }
        }

        public int ResourceCount
        {
            get { return environment == null ? resourceCount : environment.ResourceCount; }
        }

        public float MetabolismPerSecond
        {
            get { return metabolismPerSecond; }
        }

        public float MaxAgeSeconds
        {
            get { return maxAgeSeconds; }
        }

        public float ReproductionEnergyThreshold
        {
            get { return reproductionEnergyThreshold; }
        }

        public string EcologyStatus
        {
            get { return ecologyStatus; }
        }

        public IReadOnlyList<GenerationRecord> GenerationHistory
        {
            get { return engine == null || engine.History == null ? null : engine.History.Generations; }
        }

        public IReadOnlyList<IndividualHistoryRecord> SelectedAncestry
        {
            get { return selectedAncestry; }
        }

        public int AncestryCursor
        {
            get { return ancestryCursor; }
        }

        public string HistoryStatus
        {
            get { return historyStatus; }
        }

        public float EvaluationElapsed
        {
            get { return evaluationElapsed; }
        }

        public float GenerationDuration
        {
            get { return generationDuration; }
        }

        public float JointDriveForce
        {
            get { return jointDriveForce; }
        }

        public float JointTargetSpeedDegrees
        {
            get { return jointTargetSpeedDegrees; }
        }

        public float JointDamping
        {
            get { return jointDamping; }
        }

        public float SettlingDuration
        {
            get { return settlingDuration; }
        }

        public bool IsPaused
        {
            get { return paused; }
        }

        public float SimulationSpeed
        {
            get { return simulationSpeed; }
        }

        public string SpeedLabel
        {
            get
            {
                if (Mathf.Approximately(simulationSpeed, 1f))
                {
                    return "x1";
                }

                if (Mathf.Approximately(simulationSpeed, 10f))
                {
                    return "x10";
                }

                if (Mathf.Approximately(simulationSpeed, 100f))
                {
                    return "x100";
                }

                return "x" + simulationSpeed.ToString("0");
            }
        }

        public int PendingGenerationSkips
        {
            get { return pendingGenerationSkips; }
        }

        public bool IsFollowingSelected
        {
            get { return freeCamera != null && freeCamera.IsFollowing; }
        }

        private void Start()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            initialTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            paused = false;
            simulationSpeed = 1f;
            historyArchivePath = Path.Combine(Application.persistentDataPath, "EvolutionLabHistory.json");

            populationSize = Mathf.Clamp(populationSize, 4, 64);
            generationDuration = Mathf.Clamp(generationDuration, 4f, 120f);
            resourceCount = Mathf.Clamp(resourceCount, 4, 128);
            resourceEnergy = Mathf.Clamp(resourceEnergy, 1f, 200f);
            resourceRespawnSeconds = Mathf.Clamp(resourceRespawnSeconds, 0f, 120f);
            resourceConsumeRadius = Mathf.Clamp(resourceConsumeRadius, 0.25f, 3f);
            resourceIntakePerSecond = Mathf.Clamp(resourceIntakePerSecond, 0.1f, 60f);
            carryingCapacity = Mathf.Clamp(carryingCapacity, populationSize, 128);
            maxEnergy = Mathf.Clamp(maxEnergy, 10f, 300f);
            initialEnergy = Mathf.Clamp(initialEnergy, 1f, maxEnergy);
            offspringInitialEnergy = Mathf.Clamp(offspringInitialEnergy, 1f, maxEnergy);
            metabolismPerSecond = Mathf.Clamp(metabolismPerSecond, 0f, 10f);
            movementEnergyCost = Mathf.Clamp(movementEnergyCost, 0f, 1f);
            maxAgeSeconds = Mathf.Clamp(maxAgeSeconds, 10f, 600f);
            maturityAgeSeconds = Mathf.Clamp(maturityAgeSeconds, 0f, maxAgeSeconds);
            reproductionEnergyThreshold = Mathf.Clamp(reproductionEnergyThreshold, 1f, maxEnergy);
            reproductionCost = Mathf.Clamp(reproductionCost, 1f, maxEnergy);
            reproductionCooldownSeconds = Mathf.Clamp(reproductionCooldownSeconds, 0f, 120f);
            reproductionRadius = Mathf.Clamp(reproductionRadius, 0.5f, 20f);
            jointDriveForce = Mathf.Clamp(jointDriveForce, 10f, 500f);
            jointTargetSpeedDegrees = Mathf.Clamp(jointTargetSpeedDegrees, 20f, 720f);
            jointDamping = Mathf.Clamp(jointDamping, 0f, 60f);
            settlingDuration = Mathf.Clamp(settlingDuration, 0f, 3f);
            environment = gameObject.AddComponent<EnvironmentController>();
            environment.Initialize(
                randomSeed,
                resourceCount,
                resourceEnergy,
                resourceRespawnSeconds,
                populationSize,
                1.5f);
            ConfigureCamera();

            engine = new EvolutionEngine(populationSize, randomSeed);
            engine.Initialize();
            ui = gameObject.AddComponent<EvolutionLabUI>();
            ui.Bind(this);
            if (freeCamera != null)
            {
                freeCamera.BindUI(ui);
            }
            SpawnPopulation(engine.CurrentPopulation);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (!paused)
            {
                evaluationElapsed += Time.deltaTime;
                int completedCycles = 0;
                while (evaluationElapsed >= generationDuration && completedCycles < 8)
                {
                    evaluationElapsed -= generationDuration;
                    CompleteEcologyCycle();
                    completedCycles++;
                }
            }

            HandleWorldSelection();
        }

        private void FixedUpdate()
        {
            if (!initialized || paused || environment == null)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            environment.Tick(deltaTime);
            pendingDeaths.Clear();
            reproductionCandidates.Clear();
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature creature = creatures[i];
                if (creature == null || !creature.IsAlive || creature.RootBody == null)
                {
                    continue;
                }

                float energyGained = environment.TryConsumeEnergy(
                    creature.RootBody.position,
                    resourceConsumeRadius,
                    resourceIntakePerSecond * deltaTime);
                creature.TickLife(deltaTime, energyGained);
                if (!creature.IsAlive)
                {
                    pendingDeaths.Add(creature);
                }
                else if (creature.CanReproduce)
                {
                    reproductionCandidates.Add(creature);
                }
            }

            ProcessDeaths();
            ProcessReproduction();
            ecologyStatus = creatures.Count == 0
                ? "Population extinct. Reset the experiment to start a new world."
                : "Resources shape survival and reproduction.";
        }

        public void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : simulationSpeed;
        }

        public void SetSimulationSpeed(float speed)
        {
            simulationSpeed = Mathf.Clamp(speed, 1f, 100f);
            if (!paused)
            {
                Time.timeScale = simulationSpeed;
            }
        }

        public void SetGenerationDuration(float duration)
        {
            generationDuration = Mathf.Clamp(duration, 4f, 120f);
        }

        public void AdjustGenerationDuration(float amount)
        {
            SetGenerationDuration(generationDuration + amount);
        }

        public void AdjustMetabolism(float amount)
        {
            metabolismPerSecond = Mathf.Clamp(metabolismPerSecond + amount, 0f, 10f);
            ApplyLifeTuning();
        }

        public void AdjustReproductionEnergyThreshold(float amount)
        {
            reproductionEnergyThreshold = Mathf.Clamp(
                reproductionEnergyThreshold + amount,
                1f,
                maxEnergy);
            ApplyLifeTuning();
        }

        public void AdjustMaxAge(float amount)
        {
            maxAgeSeconds = Mathf.Clamp(maxAgeSeconds + amount, 10f, 600f);
            maturityAgeSeconds = Mathf.Min(maturityAgeSeconds, maxAgeSeconds);
            ApplyLifeTuning();
        }

        public void SetJointDriveForce(float force)
        {
            jointDriveForce = Mathf.Clamp(force, 10f, 500f);
            ApplyPhysicsTuning();
        }

        public void AdjustJointDriveForce(float amount)
        {
            SetJointDriveForce(jointDriveForce + amount);
        }

        public void SetJointTargetSpeedDegrees(float speed)
        {
            jointTargetSpeedDegrees = Mathf.Clamp(speed, 20f, 720f);
            ApplyPhysicsTuning();
        }

        public void AdjustJointTargetSpeedDegrees(float amount)
        {
            SetJointTargetSpeedDegrees(jointTargetSpeedDegrees + amount);
        }

        public void SetJointDamping(float damping)
        {
            jointDamping = Mathf.Clamp(damping, 0f, 60f);
            ApplyPhysicsTuning();
        }

        public void AdjustJointDamping(float amount)
        {
            SetJointDamping(jointDamping + amount);
        }

        public void SetSettlingDuration(float duration)
        {
            settlingDuration = Mathf.Clamp(duration, 0f, 3f);
            ApplyPhysicsTuning();
        }

        public void AdjustSettlingDuration(float amount)
        {
            SetSettlingDuration(settlingDuration + amount);
        }

        public void ResetCameraView()
        {
            if (freeCamera != null)
            {
                freeCamera.ResetView();
            }
        }

        public void ToggleFollowSelected()
        {
            if (freeCamera == null || selectedCreature == null || selectedCreature.RootBody == null)
            {
                return;
            }

            if (freeCamera.IsFollowing)
            {
                freeCamera.StopFollowing();
            }
            else
            {
                freeCamera.Follow(selectedCreature.RootBody.transform);
            }
        }

        public void StepAncestry(int amount)
        {
            if (selectedAncestry.Count == 0)
            {
                return;
            }

            int nextCursor = Mathf.Clamp(ancestryCursor + amount, 0, selectedAncestry.Count - 1);
            if (nextCursor != ancestryCursor)
            {
                ancestryCursor = nextCursor;
                DestroyHistoryPreview();
                historyStatus = "Selected G" + selectedAncestry[ancestryCursor].generation + " for preview.";
            }
        }

        public void PreviewSelectedAncestry()
        {
            if (ancestryCursor < 0 || ancestryCursor >= selectedAncestry.Count)
            {
                return;
            }

            IndividualHistoryRecord record = selectedAncestry[ancestryCursor];
            if (record == null || record.genome == null)
            {
                return;
            }

            DestroyHistoryPreview();
            previewCreature = CreatureBuilder.Build(
                record.genome,
                new Vector3(5f, 2.4f, 0f),
                new Color(0.25f, 0.9f, 1f, 1f),
                jointDriveForce,
                jointTargetSpeedDegrees,
                jointDamping,
                settlingDuration);
            previewCreature.SetObservationPreview();
            previewCreature.gameObject.name = "HistoricalPreview_G" + record.generation;
            historyStatus = "Previewing G" + record.generation + " " + record.genomeId + ".";
        }

        public void ClearHistoryPreview()
        {
            DestroyHistoryPreview();
            historyStatus = "Historical preview cleared.";
        }

        public void SaveHistoryArchive()
        {
            if (engine == null || engine.History == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(historyArchivePath, engine.History.ToJson());
                historyStatus = "Saved " + engine.History.Generations.Count + " generations to the history archive.";
            }
            catch (System.Exception exception)
            {
                historyStatus = "History save failed: " + exception.Message;
            }
        }

        public void LoadHistoryArchive()
        {
            if (engine == null || engine.History == null)
            {
                return;
            }

            try
            {
                if (!File.Exists(historyArchivePath))
                {
                    historyStatus = "No history archive found yet.";
                    return;
                }

                string json = File.ReadAllText(historyArchivePath);
                if (!engine.History.TryLoadJson(json))
                {
                    historyStatus = "History archive is invalid.";
                    return;
                }

                ClearSelectedCreature();
                paused = true;
                Time.timeScale = 0f;
                historyStatus = "Loaded " + engine.History.Generations.Count + " generations from the history archive.";
            }
            catch (System.Exception exception)
            {
                historyStatus = "History load failed: " + exception.Message;
            }
        }

        public void RequestGenerationSkip(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (!skipMode)
            {
                speedBeforeSkip = simulationSpeed;
                skipMode = true;
            }

            pendingGenerationSkips += count;
            if (paused)
            {
                paused = false;
            }

            SetSimulationSpeed(100f);
        }

        public void ResetSimulation()
        {
            if (!initialized)
            {
                return;
            }

            pendingGenerationSkips = 0;
            skipMode = false;
            paused = false;
            simulationSpeed = 1f;
            Time.timeScale = 1f;
            evaluationElapsed = 0f;
            birthsThisCycle = 0;
            deathsThisCycle = 0;
            ecologyStatus = "Population is adapting to the environment.";
            ClearSelectedCreature();
            DestroyCreatures();

            if (environment != null)
            {
                environment.ResetResources(randomSeed, populationSize, 1.5f);
            }

            engine = new EvolutionEngine(populationSize, randomSeed);
            engine.Initialize();
            SpawnPopulation(engine.CurrentPopulation);
        }

        private void CompleteEcologyCycle()
        {
            if (engine == null)
            {
                return;
            }

            var results = new List<CreatureEvaluationResult>(creatures.Count);
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i] == null)
                {
                    continue;
                }

                results.Add(creatures[i].CaptureEvaluation());
            }

            engine.RecordEcologyCycle(results, birthsThisCycle, deathsThisCycle);
            birthsThisCycle = 0;
            deathsThisCycle = 0;
            if (pendingGenerationSkips > 0)
            {
                pendingGenerationSkips--;
            }

            if (pendingGenerationSkips == 0 && skipMode)
            {
                skipMode = false;
                SetSimulationSpeed(speedBeforeSkip);
            }

            if (creatures.Count == 0)
            {
                paused = true;
                Time.timeScale = 0f;
            }
        }

        private void SpawnPopulation(IReadOnlyList<CreatureGenome> genomes)
        {
            DestroyCreatures();
            ClearSelectedCreature();
            if (genomes == null || genomes.Count == 0)
            {
                return;
            }

            float laneSpacing = 1.5f;
            float laneCenter = (genomes.Count - 1) * 0.5f;
            for (int i = 0; i < genomes.Count; i++)
            {
                float laneZ = (i - laneCenter) * laneSpacing;
                Vector3 origin = new Vector3(0f, 3.2f, laneZ);
                Color color = Color.HSVToRGB(
                    Mathf.Repeat((i / (float)genomes.Count) * 0.78f + Generation * 0.013f, 1f),
                    0.56f,
                    0.94f);
                SpawnCreature(genomes[i], origin, color, initialEnergy);
            }

            IgnoreCrossCreatureCollisions();
        }

        private Creature SpawnCreature(
            CreatureGenome genome,
            Vector3 origin,
            Color color,
            float startingEnergy)
        {
            Creature creature = CreatureBuilder.Build(
                genome,
                origin,
                color,
                jointDriveForce,
                jointTargetSpeedDegrees,
                jointDamping,
                settlingDuration);
            creature.SetResourceSensor(environment == null ? null : environment.GetNearestResourcePosition);
            creature.ConfigureLife(
                startingEnergy,
                maxEnergy,
                metabolismPerSecond,
                movementEnergyCost,
                maxAgeSeconds,
                maturityAgeSeconds,
                reproductionEnergyThreshold,
                reproductionCost,
                reproductionCooldownSeconds);
            creature.Clicked += SelectCreature;
            creatures.Add(creature);
            return creature;
        }

        private void ProcessDeaths()
        {
            for (int i = 0; i < pendingDeaths.Count; i++)
            {
                Creature creature = pendingDeaths[i];
                if (creature == null || creatures.IndexOf(creature) < 0)
                {
                    continue;
                }

                engine.History.RecordIndividual(creature.CaptureEvaluation());
                engine.RemovePopulationGenome(creature.Genome);
                if (selectedCreature == creature)
                {
                    ClearSelectedCreature();
                }

                creature.Clicked -= SelectCreature;
                creature.StopEvaluation();
                creature.gameObject.SetActive(false);
                Destroy(creature.gameObject);
                creatures.Remove(creature);
                deathsThisCycle++;
            }
        }

        private void ProcessReproduction()
        {
            if (creatures.Count >= carryingCapacity)
            {
                return;
            }

            for (int i = 0; i < reproductionCandidates.Count; i++)
            {
                if (creatures.Count >= carryingCapacity)
                {
                    break;
                }

                Creature parent = reproductionCandidates[i];
                if (parent == null || !parent.CanReproduce || creatures.IndexOf(parent) < 0)
                {
                    continue;
                }

                Creature partner = FindReproductionPartner(parent);
                if (!parent.TrySpendReproductionCost())
                {
                    continue;
                }

                if (partner != null && !partner.TrySpendReproductionCost())
                {
                    partner = null;
                }

                CreatureGenome childGenome = engine.CreateOffspring(
                    parent.Genome,
                    partner == null ? null : partner.Genome);
                Vector3 birthOrigin = parent.RootBody == null
                    ? new Vector3(0f, 3.2f, 0f)
                    : parent.RootBody.position + new Vector3(
                        -0.4f,
                        1.6f,
                        ((parent.OffspringCount % 3) - 1) * 0.65f);
                Color childColor = Color.HSVToRGB(
                    Mathf.Repeat(childGenome.generation * 0.043f + birthsThisCycle * 0.017f, 1f),
                    0.62f,
                    0.95f);
                SpawnCreature(childGenome, birthOrigin, childColor, offspringInitialEnergy);
                birthsThisCycle++;
            }

            IgnoreCrossCreatureCollisions();
        }

        private Creature FindReproductionPartner(Creature parent)
        {
            if (parent == null || parent.RootBody == null)
            {
                return null;
            }

            Creature nearest = null;
            float nearestDistance = reproductionRadius * reproductionRadius;
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature candidate = creatures[i];
                if (candidate == null
                    || candidate == parent
                    || !candidate.CanReproduce
                    || candidate.RootBody == null)
                {
                    continue;
                }

                Vector3 delta = candidate.RootBody.position - parent.RootBody.position;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void IgnoreCrossCreatureCollisions()
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                for (int j = i + 1; j < creatures.Count; j++)
                {
                    IReadOnlyList<Collider> first = creatures[i].Colliders;
                    IReadOnlyList<Collider> second = creatures[j].Colliders;
                    for (int a = 0; a < first.Count; a++)
                    {
                        for (int b = 0; b < second.Count; b++)
                        {
                            Physics.IgnoreCollision(first[a], second[b], true);
                        }
                    }
                }
            }
        }

        private void SelectCreature(Creature creature)
        {
            if (creature == null)
            {
                return;
            }

            bool wasFollowing = freeCamera != null && freeCamera.IsFollowing;

            if (selectedCreature != null && selectedCreature != creature)
            {
                selectedCreature.SetSelected(false);
            }

            selectedCreature = creature;
            selectedCreature.SetSelected(true);
            RefreshSelectedAncestry();
            if (wasFollowing && freeCamera != null && selectedCreature.RootBody != null)
            {
                freeCamera.Follow(selectedCreature.RootBody.transform);
            }

            if (ui != null)
            {
                ui.SetSelectedCreature(selectedCreature);
            }
        }

        private void ApplyPhysicsTuning()
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i] == null)
                {
                    continue;
                }

                creatures[i].SetPhysicsTuning(
                    jointDriveForce,
                    jointTargetSpeedDegrees,
                    jointDamping,
                    settlingDuration);
            }
        }

        private void ApplyLifeTuning()
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                if (creatures[i] == null)
                {
                    continue;
                }

                creatures[i].SetLifeTuning(
                    metabolismPerSecond,
                    maxAgeSeconds,
                    maturityAgeSeconds,
                    reproductionEnergyThreshold,
                    reproductionCost,
                    reproductionCooldownSeconds);
            }
        }

        private void ClearSelectedCreature()
        {
            if (selectedCreature != null)
            {
                selectedCreature.SetSelected(false);
            }

            selectedCreature = null;
            selectedAncestry.Clear();
            ancestryCursor = 0;
            DestroyHistoryPreview();
            if (freeCamera != null)
            {
                freeCamera.StopFollowing();
            }

            if (ui != null)
            {
                ui.SetSelectedCreature(null);
            }
        }

        private void RefreshSelectedAncestry()
        {
            selectedAncestry.Clear();
            ancestryCursor = 0;
            DestroyHistoryPreview();
            if (engine == null || engine.History == null || selectedCreature == null || selectedCreature.Genome == null)
            {
                return;
            }

            List<IndividualHistoryRecord> ancestry = engine.History.GetAncestry(selectedCreature.Genome, 8);
            selectedAncestry.AddRange(ancestry);
        }

        private void DestroyHistoryPreview()
        {
            if (previewCreature == null)
            {
                return;
            }

            previewCreature.gameObject.SetActive(false);
            Destroy(previewCreature.gameObject);
            previewCreature = null;
        }

        private void HandleWorldSelection()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || mainCamera == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            if (ui != null && ui.IsPointerOverUI(screenPosition))
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                CreatureBodyPart bodyPart = hit.collider.GetComponent<CreatureBodyPart>();
                if (bodyPart == null)
                {
                    bodyPart = hit.collider.GetComponentInParent<CreatureBodyPart>();
                }

                if (bodyPart != null)
                {
                    SelectCreature(bodyPart.Owner);
                    return;
                }
            }

            ClearSelectedCreature();
        }

        private void ConfigureCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 58f;
            mainCamera.nearClipPlane = 0.05f;
            mainCamera.farClipPlane = 500f;
            mainCamera.backgroundColor = new Color(0.025f, 0.045f, 0.065f, 1f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;

            freeCamera = mainCamera.GetComponent<FreeCameraController>();
            if (freeCamera == null)
            {
                freeCamera = mainCamera.gameObject.AddComponent<FreeCameraController>();
            }

            freeCamera.Configure(new Vector3(11f, 23f, -34f), new Vector3(5f, 0f, 0f));
        }

        private void DestroyCreatures()
        {
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature creature = creatures[i];
                if (creature == null)
                {
                    continue;
                }

                creature.Clicked -= SelectCreature;
                creature.StopEvaluation();
                creature.gameObject.SetActive(false);
                Destroy(creature.gameObject);
            }

            creatures.Clear();
        }

        private void OnDestroy()
        {
            DestroyHistoryPreview();
            DestroyCreatures();
            Time.timeScale = initialTimeScale;
        }
    }

    public static class EvolutionLabBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeSimulation()
        {
            if (Object.FindAnyObjectByType<EvolutionSimulation>() != null)
            {
                return;
            }

            var root = new GameObject("Evolution Lab Runtime");
            root.AddComponent<EvolutionSimulation>();
        }
    }
}
