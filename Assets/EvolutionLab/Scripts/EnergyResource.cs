using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// A physical-looking, replenishing energy source. It has no semantic
    /// instruction attached to it; organisms only receive its position and
    /// can gain energy when they are physically close enough.
    /// </summary>
    public sealed class EnergyResource : MonoBehaviour
    {
        private Renderer resourceRenderer;
        private Collider resourceCollider;
        private Material resourceMaterial;
        private float maxEnergy;
        private float respawnDelaySeconds;
        private float respawnRemaining;
        private bool presentationEnabled = true;

        public float CurrentEnergy { get; private set; }

        public bool IsAvailable
        {
            get { return CurrentEnergy > 0.001f; }
        }

        public WorldResourceSnapshot CaptureState(int index)
        {
            return new WorldResourceSnapshot
            {
                index = index,
                hasTransform = true,
                position = transform.position,
                rotation = transform.rotation,
                currentEnergy = CurrentEnergy,
                respawnRemaining = respawnRemaining
            };
        }

        public void RestoreState(WorldResourceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            CurrentEnergy = Mathf.Clamp(snapshot.currentEnergy, 0f, maxEnergy);
            respawnRemaining = Mathf.Clamp(snapshot.respawnRemaining, 0f, respawnDelaySeconds);
            if (snapshot.hasTransform && IsFinite(snapshot.position) && IsFinite(snapshot.rotation))
            {
                transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
            }
            if (CurrentEnergy <= 0.001f && respawnRemaining <= 0f)
            {
                respawnRemaining = respawnDelaySeconds;
            }
            SetVisible(IsAvailable);
        }

        public void SetPresentationEnabled(bool enabled)
        {
            presentationEnabled = enabled;
            SetVisible(IsAvailable);
        }

        public void Initialize(float energy, float respawnDelay, Color color)
        {
            maxEnergy = Mathf.Max(0.1f, energy);
            CurrentEnergy = maxEnergy;
            respawnDelaySeconds = Mathf.Max(0f, respawnDelay);
            respawnRemaining = 0f;
            resourceRenderer = GetComponent<Renderer>();
            resourceCollider = GetComponent<Collider>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                resourceMaterial = new Material(shader)
                {
                    color = color,
                    enableInstancing = true
                };
            }
            if (resourceRenderer != null)
            {
                if (resourceMaterial != null)
                {
                    resourceRenderer.sharedMaterial = resourceMaterial;
                }
            }

            if (resourceCollider != null)
            {
                resourceCollider.isTrigger = true;
            }
            SetVisible(true);
        }

        public float Consume(float amount)
        {
            if (!IsAvailable || amount <= 0f)
            {
                return 0f;
            }

            float consumed = Mathf.Min(CurrentEnergy, amount);
            CurrentEnergy -= consumed;
            if (CurrentEnergy <= 0.001f)
            {
                CurrentEnergy = 0f;
                respawnRemaining = respawnDelaySeconds;
                SetVisible(false);
            }

            return consumed;
        }

        public void Tick(float deltaTime)
        {
            if (IsAvailable || respawnRemaining <= 0f || deltaTime <= 0f)
            {
                return;
            }

            respawnRemaining = Mathf.Max(0f, respawnRemaining - deltaTime);
            if (respawnRemaining <= 0f)
            {
                CurrentEnergy = maxEnergy;
                SetVisible(true);
            }
        }

        private void SetVisible(bool visible)
        {
            if (resourceRenderer != null)
            {
                resourceRenderer.enabled = visible && presentationEnabled;
            }

            if (resourceCollider != null)
            {
                resourceCollider.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            if (resourceMaterial != null)
            {
                Destroy(resourceMaterial);
                resourceMaterial = null;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
