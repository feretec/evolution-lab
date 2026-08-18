using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionLab
{
    /// <summary>
    /// Scene-facing coordinator for the first evolution experiment.
    /// </summary>
    public sealed class EvolutionSimulation : MonoBehaviour
    {
        [Header("Prototype 1 settings")]
        [SerializeField] private int populationSize = 24;
        [SerializeField] private float generationDuration = 20f;
        [SerializeField] private int randomSeed = 172903;

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

            populationSize = Mathf.Clamp(populationSize, 4, 64);
            generationDuration = Mathf.Clamp(generationDuration, 4f, 120f);
            jointDriveForce = Mathf.Clamp(jointDriveForce, 10f, 500f);
            jointTargetSpeedDegrees = Mathf.Clamp(jointTargetSpeedDegrees, 20f, 720f);
            jointDamping = Mathf.Clamp(jointDamping, 0f, 60f);
            settlingDuration = Mathf.Clamp(settlingDuration, 0f, 3f);
            environment = gameObject.AddComponent<EnvironmentController>();
            environment.Initialize();
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
                if (evaluationElapsed >= generationDuration)
                {
                    CompleteGeneration();
                }
            }

            HandleWorldSelection();
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
            ClearSelectedCreature();
            DestroyCreatures();

            engine = new EvolutionEngine(populationSize, randomSeed);
            engine.Initialize();
            SpawnPopulation(engine.CurrentPopulation);
        }

        private void CompleteGeneration()
        {
            if (engine == null || creatures.Count == 0)
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
                creatures[i].StopEvaluation();
            }

            List<CreatureGenome> nextPopulation = engine.BreedNextGeneration(results);
            if (pendingGenerationSkips > 0)
            {
                pendingGenerationSkips--;
            }

            SpawnPopulation(nextPopulation);
            evaluationElapsed = 0f;

            if (pendingGenerationSkips == 0 && skipMode)
            {
                skipMode = false;
                SetSimulationSpeed(speedBeforeSkip);
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
                Creature creature = CreatureBuilder.Build(
                    genomes[i],
                    origin,
                    color,
                    jointDriveForce,
                    jointTargetSpeedDegrees,
                    jointDamping,
                    settlingDuration);
                creature.Clicked += SelectCreature;
                creatures.Add(creature);
            }

            IgnoreCrossCreatureCollisions();
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

            if (selectedCreature != null && selectedCreature != creature)
            {
                selectedCreature.SetSelected(false);
            }

            selectedCreature = creature;
            selectedCreature.SetSelected(true);
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

        private void ClearSelectedCreature()
        {
            if (selectedCreature != null)
            {
                selectedCreature.SetSelected(false);
            }

            selectedCreature = null;
            if (ui != null)
            {
                ui.SetSelectedCreature(null);
            }
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
