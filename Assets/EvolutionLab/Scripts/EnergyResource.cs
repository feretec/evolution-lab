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

        public float CurrentEnergy { get; private set; }

        public bool IsAvailable
        {
            get { return CurrentEnergy > 0.001f; }
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

            resourceMaterial = new Material(shader)
            {
                color = color,
                enableInstancing = true
            };
            if (resourceRenderer != null)
            {
                resourceRenderer.sharedMaterial = resourceMaterial;
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
                resourceRenderer.enabled = visible;
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
    }
}
