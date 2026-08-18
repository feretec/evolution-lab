using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// A neutral physical feature of the world. It intentionally carries no
    /// semantic purpose such as food, shelter, or obstacle-to-use; organisms
    /// only experience its collider and shape through generic observations.
    /// </summary>
    public sealed class EnvironmentFeature : MonoBehaviour
    {
        public float PhysicalRadius { get; private set; }
        public bool IsMovable { get; private set; }

        public void Initialize(float physicalRadius, bool isMovable = false)
        {
            PhysicalRadius = Mathf.Max(0.1f, physicalRadius);
            IsMovable = isMovable;
        }
    }
}
