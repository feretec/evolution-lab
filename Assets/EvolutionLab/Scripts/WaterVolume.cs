using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// A neutral physical medium. It applies buoyancy and drag to any Rigidbody
    /// entering the volume; controllers receive no semantic "water" flag.
    /// </summary>
    public sealed class WaterVolume : MonoBehaviour
    {
        private float surfaceHeight;
        private float buoyancy;
        private float linearDrag;

        public void Initialize(float configuredSurfaceHeight, float configuredBuoyancy, float configuredDrag)
        {
            surfaceHeight = configuredSurfaceHeight;
            buoyancy = Mathf.Max(0f, configuredBuoyancy);
            linearDrag = Mathf.Max(0f, configuredDrag);

            BoxCollider volume = GetComponent<BoxCollider>();
            if (volume != null)
            {
                volume.isTrigger = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            Rigidbody body = other == null ? null : other.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }

            float submergedDepth = surfaceHeight - body.worldCenterOfMass.y;
            if (submergedDepth <= 0f)
            {
                return;
            }

            float depthFactor = Mathf.Clamp01(submergedDepth / Mathf.Max(0.1f, other.bounds.extents.y * 2f));
            body.AddForce(Vector3.up * (buoyancy * body.mass * depthFactor), ForceMode.Force);
            body.AddForce(-body.linearVelocity * (linearDrag * body.mass * depthFactor), ForceMode.Force);
            body.AddTorque(-body.angularVelocity * (linearDrag * 0.35f * body.mass * depthFactor), ForceMode.Force);
        }
    }
}
