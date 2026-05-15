using UnityEngine;

namespace EnvForge.Mvp0
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgentMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float turnSpeedDegrees = 180f;

        private Rigidbody body;
        private float forwardInput;
        private float turnInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void FixedUpdate()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            Quaternion turn = Quaternion.Euler(0f, turnInput * turnSpeedDegrees * fixedDeltaTime, 0f);
            body.MoveRotation(body.rotation * turn);

            Vector3 movement = transform.forward * (forwardInput * moveSpeed * fixedDeltaTime);
            body.MovePosition(body.position + movement);
        }

        public void SetInput(float forward, float turn)
        {
            forwardInput = Mathf.Clamp(forward, -1f, 1f);
            turnInput = Mathf.Clamp(turn, -1f, 1f);
        }

        public void Stop()
        {
            forwardInput = 0f;
            turnInput = 0f;
        }
    }
}
