using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation
{
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NavigationLiveController : MonoBehaviour, INavigationEpisodeEvents
    {
        private AgentMotor motor;
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            body = GetComponent<Rigidbody>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void Update()
        {
            if (motor == null || !motor.enabled)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                motor.SetInput(0f, 0f);
                return;
            }

            float forward = keyboard.wKey.isPressed ? 1f : 0f;
            float turn = ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
            motor.SetInput(forward, turn);
        }

        public void ReportGoalReached()
        {
            ResetPose();
        }

        public void ReportWallCollision()
        {
            ResetPose();
        }

        public void ReportWallCollision(string wallId)
        {
            ResetPose();
        }

        public void ResetPose()
        {
            motor?.Stop();
            if (body != null)
            {
                body.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
                body.position = startPosition;
                body.rotation = startRotation;
            }

            transform.SetPositionAndRotation(startPosition, startRotation);
            Physics.SyncTransforms();
        }

        public void SetResetPose(Vector3 position, Quaternion rotation, bool applyImmediately = true)
        {
            startPosition = position;
            startRotation = rotation;
            if (applyImmediately)
            {
                ResetPose();
            }
        }

        private static float ReadAxis(bool positive, bool negative)
        {
            if (positive == negative)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
