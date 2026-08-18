using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Prototype environment: a neutral flat physical surface only.
    /// </summary>
    public sealed class EnvironmentController : MonoBehaviour
    {
        private GameObject ground;
        private Material groundMaterial;
        private readonly List<EnergyResource> resources = new List<EnergyResource>();
        private System.Random random;
        private int resourceCount;
        private float resourceEnergy;
        private float resourceRespawnSeconds;

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
            random = new System.Random(seed);

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Prototype Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            groundMaterial = new Material(shader)
            {
                color = new Color(0.12f, 0.16f, 0.2f, 1f),
                enableInstancing = true
            };
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = groundMaterial;
            }

            GroundCollider = ground.GetComponent<Collider>();
            ResetResources(seed, initialPopulation, laneSpacing);
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
            random = new System.Random(seed);
            int guaranteedCount = Mathf.Min(resourceCount, Mathf.Max(0, initialPopulation));
            float populationCenter = (initialPopulation - 1) * 0.5f;
            for (int i = 0; i < resourceCount; i++)
            {
                bool guaranteedLaneResource = i < guaranteedCount;
                float x = guaranteedLaneResource
                    ? 0.65f + (float)random.NextDouble() * 1.6f
                    : 1f + (float)random.NextDouble() * 28f;
                float z = guaranteedLaneResource
                    ? (i - populationCenter) * laneSpacing
                    : -19f + (float)random.NextDouble() * 38f;
                SpawnResource(new Vector3(x, 0.2f, z), i);
            }
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
        }
    }
}
