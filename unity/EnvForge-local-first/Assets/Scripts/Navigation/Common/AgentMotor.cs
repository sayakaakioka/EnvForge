using UnityEngine;

namespace EnvForge.Navigation
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgentMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float forwardInputScale = 2f;
        [SerializeField] private float turnSpeedDegrees = 180f;

        private Rigidbody body;
        private float forwardInput;
        private float turnInput;
        private float runtimeMoveSpeed;
        private float runtimeTurnSpeedDegrees;
        private float runtimeForwardInputScale;

        public float ForwardCommand => runtimeForwardInputScale > 0f ? forwardInput / runtimeForwardInputScale : 0f;
        public float TurnCommand => turnInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            ResetMotionProfile();
        }

        private void FixedUpdate()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            Quaternion turn = Quaternion.Euler(0f, turnInput * runtimeTurnSpeedDegrees * fixedDeltaTime, 0f);
            body.MoveRotation(body.rotation * turn);

            Vector3 movement = transform.forward * (forwardInput * runtimeMoveSpeed * fixedDeltaTime);
            body.MovePosition(body.position + movement);
        }

        public void SetInput(float forward, float turn)
        {
            forwardInput = Mathf.Clamp(forward, -1f, 1f) * runtimeForwardInputScale;
            turnInput = Mathf.Clamp(turn, -1f, 1f);
        }

        public void SetMotionProfile(float movementSpeed, float rotationSpeedDegrees, float inputScale = 1f)
        {
            runtimeMoveSpeed = Mathf.Max(0f, movementSpeed);
            runtimeTurnSpeedDegrees = Mathf.Max(0f, rotationSpeedDegrees);
            runtimeForwardInputScale = Mathf.Max(0f, inputScale);
        }

        public void ResetMotionProfile()
        {
            runtimeMoveSpeed = moveSpeed;
            runtimeTurnSpeedDegrees = turnSpeedDegrees;
            runtimeForwardInputScale = forwardInputScale;
        }

        public void Stop()
        {
            forwardInput = 0f;
            turnInput = 0f;
        }
    }
}
