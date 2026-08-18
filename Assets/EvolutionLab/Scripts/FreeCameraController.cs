using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionLab
{
    /// <summary>
    /// Small unscaled free-camera controller for observing the simulation.
    /// WASD moves on the camera plane, Q/E move vertically, RMB looks, and the
    /// mouse wheel dollies. It is intentionally independent from the simulation
    /// clock so the world can be inspected while paused.
    /// </summary>
    public sealed class FreeCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float fastMultiplier = 3f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float wheelStep = 0.03f;
        [SerializeField] private float minimumPitch = -85f;
        [SerializeField] private float maximumPitch = 85f;

        private Vector3 defaultPosition;
        private Vector3 defaultLookAt;
        private float yaw;
        private float pitch;
        private EvolutionLabUI ui;
        private Transform followTarget;
        private Vector3 followOffset;

        public bool IsFollowing
        {
            get { return followTarget != null; }
        }

        public void Configure(Vector3 position, Vector3 lookAt)
        {
            defaultPosition = position;
            defaultLookAt = lookAt;
            SetView(position, lookAt);
        }

        public void ResetView()
        {
            StopFollowing();
            SetView(defaultPosition, defaultLookAt);
        }

        public void BindUI(EvolutionLabUI owner)
        {
            ui = owner;
        }

        public void Follow(Transform target)
        {
            if (target == null)
            {
                return;
            }

            followTarget = target;
            followOffset = transform.position - target.position;
        }

        public void StopFollowing()
        {
            followTarget = null;
        }

        private void Awake()
        {
            SyncAnglesFromTransform();
        }

        private void Update()
        {
            if (followTarget == null)
            {
                followTarget = null;
            }
            else
            {
                transform.position = followTarget.position + followOffset;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null && mouse == null)
            {
                return;
            }

            Vector3 input = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed)
                {
                    input += Vector3.forward;
                }

                if (keyboard.sKey.isPressed)
                {
                    input += Vector3.back;
                }

                if (keyboard.dKey.isPressed)
                {
                    input += Vector3.right;
                }

                if (keyboard.aKey.isPressed)
                {
                    input += Vector3.left;
                }

                if (keyboard.eKey.isPressed)
                {
                    input += Vector3.up;
                }

                if (keyboard.qKey.isPressed)
                {
                    input += Vector3.down;
                }
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            float speed = moveSpeed;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                speed *= fastMultiplier;
            }

            float deltaTime = Time.unscaledDeltaTime;
            Vector3 movement = transform.right * input.x
                + transform.forward * input.z
                + Vector3.up * input.y;
            transform.position += movement * speed * deltaTime;
            if (followTarget != null && movement.sqrMagnitude > 0.0001f)
            {
                followOffset = transform.position - followTarget.position;
            }

            if (mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                bool pointerOverUI = ui != null && ui.IsPointerOverUI(mousePosition);
                if (!pointerOverUI && mouse.rightButton.isPressed)
                {
                    Vector2 lookDelta = mouse.delta.ReadValue();
                    yaw += lookDelta.x * lookSensitivity;
                    pitch = Mathf.Clamp(pitch - lookDelta.y * lookSensitivity, minimumPitch, maximumPitch);
                }

                float wheel = mouse.scroll.ReadValue().y;
                if (!pointerOverUI && Mathf.Abs(wheel) > 0.01f)
                {
                    transform.position += transform.forward * (wheel * wheelStep);
                }
            }

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void SetView(Vector3 position, Vector3 lookAt)
        {
            transform.position = position;
            Vector3 direction = lookAt - position;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.forward;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            SyncAnglesFromTransform();
        }

        private void SyncAnglesFromTransform()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizeAngle(euler.x);
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }
    }
}
