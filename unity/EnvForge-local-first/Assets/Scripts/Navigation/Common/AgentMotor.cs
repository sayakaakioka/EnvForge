using UnityEngine;

namespace EnvForge.Navigation
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgentMotor : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float forwardInputScale = 1f;
        [SerializeField] private float turnSpeedDegrees = 150f;

        private Rigidbody body;
        private float forwardInput;
        private float turnInput;
        private float runtimeMoveSpeed;
        private float runtimeTurnSpeedDegrees;
        private float runtimeForwardInputScale;
        private bool automationStepping;

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
            if (automationStepping)
            {
                return;
            }

            float fixedDeltaTime = Time.fixedDeltaTime;
            Quaternion turn = Quaternion.Euler(0f, turnInput * runtimeTurnSpeedDegrees * fixedDeltaTime, 0f);
            body.MoveRotation(body.rotation * turn);

            Vector3 movement = transform.forward * (forwardInput * runtimeMoveSpeed * fixedDeltaTime);
            body.MovePosition(body.position + movement);
        }

        public void StepMotionForAutomation(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            Quaternion turn = Quaternion.Euler(0f, turnInput * runtimeTurnSpeedDegrees * safeDeltaTime, 0f);
            Quaternion nextRotation = body.rotation * turn;
            Vector3 movement = nextRotation * Vector3.forward * (forwardInput * runtimeMoveSpeed * safeDeltaTime);
            body.rotation = nextRotation;
            body.position += movement;
            transform.SetPositionAndRotation(body.position, body.rotation);
            Physics.SyncTransforms();
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

        public void SetAutomationStepping(bool enabled)
        {
            automationStepping = enabled;
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
