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

        public Collider GroundCollider { get; private set; }

        public void Initialize()
        {
            if (ground != null)
            {
                return;
            }

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
        }

        private void OnDestroy()
        {
            if (groundMaterial != null)
            {
                Destroy(groundMaterial);
                groundMaterial = null;
            }
        }
    }
}
