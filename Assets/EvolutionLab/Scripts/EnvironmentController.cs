using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Deterministic neutral physical world. Geometry and media expose only
    /// physical consequences; no feature carries an intended use for agents.
    /// </summary>
    public sealed class EnvironmentController : MonoBehaviour
    {
        private GameObject ground;
        private Material groundMaterial;
        private Material featureMaterial;
        private Material waterMaterial;
        private readonly List<EnergyResource> resources = new List<EnergyResource>();
        private readonly List<EnvironmentFeature> features = new List<EnvironmentFeature>();
        private readonly List<Rigidbody> movableBodies = new List<Rigidbody>();
        private DeterministicRandom resourceRandom;
        private DeterministicRandom featureRandom;
        private int resourceCount;
        private float resourceEnergy;
        private float resourceRespawnSeconds;
        private bool presentationEnabled = true;

        public Collider GroundCollider { get; private set; }

        public int ResourceCount
        {
            get { return resources.Count; }
        }

        public int AvailableResourceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < resources.Count; i++)
                {
                    if (resources[i] != null && resources[i].IsAvailable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FeatureCount
        {
            get { return features.Count; }
        }

        public void Initialize()
        {
            Initialize(172903, 36, 30f, 12f, 24, 1.5f);
        }

        public void Initialize(
            int seed,
            int configuredResourceCount,
            float configuredResourceEnergy,
            float configuredResourceRespawnSeconds,
            int initialPopulation,
            float laneSpacing)
        {
            if (ground != null)
            {
                return;
            }

            resourceCount = Mathf.Clamp(configuredResourceCount, 4, 128);
            resourceEnergy = Mathf.Max(0.1f, configuredResourceEnergy);
            resourceRespawnSeconds = Mathf.Max(0f, configuredResourceRespawnSeconds);
            resourceRandom = new DeterministicRandom(seed);
            featureRandom = new DeterministicRandom(seed ^ 0x4A17C39);

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Prototype Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                groundMaterial = new Material(shader)
                {
                    color = new Color(0.12f, 0.16f, 0.2f, 1f),
                    enableInstancing = true
                };
            }
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (groundMaterial != null)
                {
                    renderer.sharedMaterial = groundMaterial;
                }
            }

            GroundCollider = ground.GetComponent<Collider>();
            ResetResources(seed, initialPopulation, laneSpacing);
            SpawnPhysicalFeatures(seed);
            SpawnWaterVolume();
        }

        public void ResetResources(int seed, int initialPopulation, float laneSpacing)
        {
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                if (resources[i] != null)
                {
                    resources[i].gameObject.SetActive(false);
                    Destroy(resources[i].gameObject);
                }
            }

            resources.Clear();
            resourceRandom = new DeterministicRandom(seed);
            int guaranteedCount = Mathf.Min(resourceCount, Mathf.Max(0, initialPopulation));
            float populationCenter = (initialPopulation - 1) * 0.5f;
            for (int i = 0; i < resourceCount; i++)
            {
                bool guaranteedLaneResource = i < guaranteedCount;
                float x = guaranteedLaneResource
                    ? 0.65f + (float)resourceRandom.NextDouble() * 1.6f
                    : 1f + (float)resourceRandom.NextDouble() * 28f;
                float z = guaranteedLaneResource
                    ? (i - populationCenter) * laneSpacing
                    : -19f + (float)resourceRandom.NextDouble() * 38f;
                SpawnResource(new Vector3(x, 0.2f, z), i);
            }
        }

        public float GetObstacleProximity(Vector3 origin, float radius)
        {
            float searchRadius = Mathf.Max(0.1f, radius);
            float nearest = searchRadius;
            for (int i = 0; i < features.Count; i++)
            {
                EnvironmentFeature feature = features[i];
                if (feature == null)
                {
                    continue;
                }

                Vector3 delta = feature.transform.position - origin;
                delta.y = 0f;
                float distance = Mathf.Max(0f, delta.magnitude - feature.PhysicalRadius);
                nearest = Mathf.Min(nearest, distance);
            }

            return Mathf.Clamp01(1f - nearest / searchRadius);
        }

        public Vector3 GetNearestFeatureDirection(Vector3 origin, float radius)
        {
            float searchRadius = Mathf.Max(0.1f, radius);
            float nearest = searchRadius * searchRadius;
            Vector3 direction = Vector3.zero;
            for (int i = 0; i < features.Count; i++)
            {
                EnvironmentFeature feature = features[i];
                if (feature == null)
                {
                    continue;
                }

                Vector3 delta = feature.transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude <= nearest)
                {
                    nearest = delta.sqrMagnitude;
                    direction = delta;
                }
            }

            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] != null)
                {
                    resources[i].Tick(deltaTime);
                }
            }
        }

        public void SetPresentationEnabled(bool enabled)
        {
            presentationEnabled = enabled;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }

            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] != null)
                {
                    resources[i].SetPresentationEnabled(enabled);
                }
            }
        }

        public void CaptureRuntimeState(WorldSnapshotArchive archive)
        {
            if (archive == null)
            {
                return;
            }

            archive.resources.Clear();
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] != null)
                {
                    archive.resources.Add(resources[i].CaptureState(i));
                }
            }

            archive.movableFeatures.Clear();
            for (int i = 0; i < movableBodies.Count; i++)
            {
                Rigidbody body = movableBodies[i];
                if (body == null)
                {
                    continue;
                }

                archive.movableFeatures.Add(new WorldRigidbodySnapshot
                {
                    index = i,
                    position = body.position,
                    rotation = body.rotation,
                    linearVelocity = body.linearVelocity,
                    angularVelocity = body.angularVelocity
                });
            }

            archive.environmentResourceRandomState = resourceRandom == null ? 0u : resourceRandom.State;
            archive.environmentFeatureRandomState = featureRandom == null ? 0u : featureRandom.State;
        }

        public void RestoreRuntimeState(WorldSnapshotArchive archive)
        {
            if (archive == null)
            {
                return;
            }

            int fallbackSeed = archive.randomSeed == 0 ? 172903 : archive.randomSeed;
            if (resourceRandom == null)
            {
                resourceRandom = new DeterministicRandom(fallbackSeed);
            }
            if (featureRandom == null)
            {
                featureRandom = new DeterministicRandom(fallbackSeed ^ 0x4A17C39);
            }

            if (archive.schemaVersion >= 4)
            {
                if (archive.environmentResourceRandomState != 0u)
                {
                    resourceRandom.State = archive.environmentResourceRandomState;
                }
                if (archive.environmentFeatureRandomState != 0u)
                {
                    featureRandom.State = archive.environmentFeatureRandomState;
                }
            }

            if (archive.resources != null)
            {
                for (int i = 0; i < archive.resources.Count; i++)
                {
                    WorldResourceSnapshot snapshot = archive.resources[i];
                    if (snapshot != null && snapshot.index >= 0 && snapshot.index < resources.Count
                        && resources[snapshot.index] != null)
                    {
                        resources[snapshot.index].RestoreState(snapshot);
                    }
                }
            }

            if (archive.movableFeatures != null)
            {
                for (int i = 0; i < archive.movableFeatures.Count; i++)
                {
                    WorldRigidbodySnapshot snapshot = archive.movableFeatures[i];
                    if (snapshot == null || snapshot.index < 0 || snapshot.index >= movableBodies.Count)
                    {
                        continue;
                    }

                    Rigidbody body = movableBodies[snapshot.index];
                    if (body == null)
                    {
                        continue;
                    }

                    body.position = snapshot.position;
                    body.rotation = snapshot.rotation;
                    body.linearVelocity = snapshot.linearVelocity;
                    body.angularVelocity = snapshot.angularVelocity;
                }
            }

            Physics.SyncTransforms();
        }

        public Vector3 GetNearestResourcePosition(Vector3 origin)
        {
            EnergyResource nearest = FindNearestResource(origin, float.PositiveInfinity);
            if (nearest == null)
            {
                return origin;
            }

            return new Vector3(nearest.transform.position.x, origin.y, nearest.transform.position.z);
        }

        public float TryConsumeEnergy(Vector3 origin, float radius, float amount)
        {
            EnergyResource nearest = FindNearestResource(origin, radius);
            return nearest == null ? 0f : nearest.Consume(amount);
        }

        private EnergyResource FindNearestResource(Vector3 origin, float radius)
        {
            float radiusSquared = float.IsPositiveInfinity(radius)
                ? float.PositiveInfinity
                : Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            EnergyResource nearest = null;
            float nearestDistanceSquared = radiusSquared;
            for (int i = 0; i < resources.Count; i++)
            {
                EnergyResource resource = resources[i];
                if (resource == null || !resource.IsAvailable)
                {
                    continue;
                }

                Vector3 delta = resource.transform.position - origin;
                delta.y = 0f;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearest = resource;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private void SpawnResource(Vector3 position, int index)
        {
            GameObject resourceObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            resourceObject.name = "Energy Resource " + index.ToString("00");
            resourceObject.transform.SetParent(transform, true);
            resourceObject.transform.position = position;
            resourceObject.transform.localScale = Vector3.one * 0.34f;
            EnergyResource resource = resourceObject.AddComponent<EnergyResource>();
            resource.Initialize(
                resourceEnergy,
                resourceRespawnSeconds,
                new Color(0.35f, 0.95f, 0.55f, 1f));
            resources.Add(resource);
            resource.SetPresentationEnabled(presentationEnabled);
        }

        private void SpawnPhysicalFeatures(int seed)
        {
            for (int i = features.Count - 1; i >= 0; i--)
            {
                if (features[i] != null)
                {
                    features[i].gameObject.SetActive(false);
                    Destroy(features[i].gameObject);
                }
            }

            features.Clear();
            movableBodies.Clear();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                featureMaterial = new Material(shader)
                {
                    color = new Color(0.22f, 0.24f, 0.27f, 1f),
                    enableInstancing = true
                };
            }

            featureRandom = new DeterministicRandom(seed ^ 0x4A17C39);
            const int featureCount = 14;
            for (int i = 0; i < featureCount; i++)
            {
                PrimitiveType type = i % 3 == 0
                    ? PrimitiveType.Cylinder
                    : (i % 3 == 1 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                GameObject featureObject = GameObject.CreatePrimitive(type);
                featureObject.name = "Physical Feature " + i.ToString("00");
                featureObject.transform.SetParent(transform, true);
                float radius = 0.55f + (float)featureRandom.NextDouble() * 1.15f;
                float x = 4f + (float)featureRandom.NextDouble() * 29f;
                float z = -25f + (float)featureRandom.NextDouble() * 50f;
                featureObject.transform.position = new Vector3(x, radius * 0.55f, z);
                featureObject.transform.localScale = type == PrimitiveType.Cylinder
                    ? new Vector3(radius, radius * 0.85f, radius)
                    : new Vector3(radius * 1.4f, radius, radius * 1.15f);
                featureObject.transform.rotation = Quaternion.Euler(
                    0f,
                    (float)featureRandom.NextDouble() * 360f,
                    type == PrimitiveType.Cube ? (float)featureRandom.NextDouble() * 18f : 0f);
                Renderer renderer = featureObject.GetComponent<Renderer>();
                if (renderer != null && featureMaterial != null)
                {
                    renderer.sharedMaterial = featureMaterial;
                }

                RegisterFeature(featureObject, radius, false);
            }

            // Long, low boxes produce slopes and height changes without giving
            // their possible use to the controller.
            CreateFeature(
                PrimitiveType.Cube,
                new Vector3(10f, 0.55f, -7f),
                new Vector3(4.8f, 0.45f, 3.2f),
                Quaternion.Euler(0f, 12f, -11f),
                3.2f,
                false);
            CreateFeature(
                PrimitiveType.Cube,
                new Vector3(18f, 0.75f, 8f),
                new Vector3(5.2f, 0.55f, 3.5f),
                Quaternion.Euler(0f, -18f, 14f),
                3.5f,
                false);

            // Paired walls form neutral narrow passages. They are geometry,
            // not labelled shelters or routes.
            CreateFeature(
                PrimitiveType.Cube,
                new Vector3(23f, 1.1f, -2.8f),
                new Vector3(6f, 2.2f, 0.45f),
                Quaternion.identity,
                3.1f,
                false);
            CreateFeature(
                PrimitiveType.Cube,
                new Vector3(23f, 1.1f, 2.8f),
                new Vector3(6f, 2.2f, 0.45f),
                Quaternion.identity,
                3.1f,
                false);

            // Movable bodies make pushing, blocking, and accidental cover
            // physically possible without defining any of those behaviours.
            for (int i = 0; i < 6; i++)
            {
                float radius = 0.45f + (float)featureRandom.NextDouble() * 0.45f;
                GameObject movable = CreateFeature(
                    i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube,
                    new Vector3(
                        7f + (float)featureRandom.NextDouble() * 22f,
                        radius + 0.15f,
                        -14f + (float)featureRandom.NextDouble() * 28f),
                    Vector3.one * (radius * 1.45f),
                    Quaternion.Euler(0f, (float)featureRandom.NextDouble() * 360f, 0f),
                    radius,
                    true);
                Rigidbody body = movable.AddComponent<Rigidbody>();
                body.mass = 2f + radius * 4f;
                body.linearDamping = 0.18f;
                body.angularDamping = 0.25f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                movableBodies.Add(body);
            }
        }

        private GameObject CreateFeature(
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            float physicalRadius,
            bool movable)
        {
            GameObject featureObject = GameObject.CreatePrimitive(type);
            featureObject.name = "Physical Feature " + features.Count.ToString("00");
            featureObject.transform.SetParent(transform, true);
            featureObject.transform.position = position;
            featureObject.transform.localScale = scale;
            featureObject.transform.rotation = rotation;
            Renderer renderer = featureObject.GetComponent<Renderer>();
            if (renderer != null && featureMaterial != null)
            {
                renderer.sharedMaterial = featureMaterial;
            }

            RegisterFeature(featureObject, physicalRadius, movable);
            return featureObject;
        }

        private void RegisterFeature(GameObject featureObject, float physicalRadius, bool movable)
        {
            EnvironmentFeature feature = featureObject.AddComponent<EnvironmentFeature>();
            feature.Initialize(physicalRadius, movable);
            features.Add(feature);
        }

        private void SpawnWaterVolume()
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Physical Medium 00";
            water.transform.SetParent(transform, true);
            water.transform.position = new Vector3(31f, 0.35f, -10f);
            water.transform.localScale = new Vector3(10f, 0.7f, 10f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                waterMaterial = new Material(shader)
                {
                    color = new Color(0.08f, 0.36f, 0.48f, 0.42f),
                    enableInstancing = true,
                    renderQueue = 3000
                };
                waterMaterial.SetFloat("_Surface", 1f);
                waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            Renderer renderer = water.GetComponent<Renderer>();
            if (renderer != null && waterMaterial != null)
            {
                renderer.sharedMaterial = waterMaterial;
            }

            WaterVolume volume = water.AddComponent<WaterVolume>();
            volume.Initialize(0.7f, 13.5f, 2.4f);
            if (!presentationEnabled && renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] != null)
                {
                    resources[i].gameObject.SetActive(false);
                }
            }

            if (groundMaterial != null)
            {
                Destroy(groundMaterial);
                groundMaterial = null;
            }

            if (featureMaterial != null)
            {
                Destroy(featureMaterial);
                featureMaterial = null;
            }

            if (waterMaterial != null)
            {
                Destroy(waterMaterial);
                waterMaterial = null;
            }
        }
    }
}
