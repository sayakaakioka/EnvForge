using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Navigation
{
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Unity.MLAgents.Policies.BehaviorParameters))]
    public sealed class NavigationAgent : Agent, INavigationEpisodeEvents
    {
        [SerializeField] private float goalReward = 100f;
        [SerializeField] private float collisionPenalty = -50f;
        [SerializeField] private float movementReward = 0.01f;
        [SerializeField] private float distanceProgressReward = 0.1f;
        [SerializeField] private float wideAnglePenalty = -0.1f;
        [SerializeField] private float rearAnglePenalty = -5f;
        [SerializeField] private float inactivePenalty = -0.1f;
        [SerializeField] private float movementThreshold = 0.001f;
        [SerializeField] private float turnActivityThreshold = 0.3f;
        [SerializeField] private Vector2 randomStartHalfExtents = new(6f, 4f);
        [SerializeField] private float randomStartClearanceRadius = 0.65f;
        [SerializeField] private int randomStartMaxAttempts = 20;

        private readonly float[] observationBuffer = new float[NavigationGoalObservation.ValueCount];

        private Transform goal;
        private Rigidbody agentBody;
        private AgentMotor motor;
        private NavigationMetrics navigationMetrics;
        private NavigationGoalObservationProvider observationProvider;
        private Vector3 agentStartPosition;
        private Quaternion agentStartRotation;
        private Vector3 goalStartPosition;
        private float previousDistanceToGoal;

        public void Configure(
            Transform goalTransform,
            Rigidbody body,
            AgentMotor agentMotor,
            NavigationMetrics metrics,
            NavigationGoalObservationProvider observations,
            Vector3 initialAgentPosition,
            Quaternion initialAgentRotation,
            Vector3 initialGoalPosition,
            Vector2 initialRandomStartHalfExtents)
        {
            goal = goalTransform;
            agentBody = body;
            motor = agentMotor;
            navigationMetrics = metrics;
            observationProvider = observations;
            agentStartPosition = initialAgentPosition;
            agentStartRotation = initialAgentRotation;
            goalStartPosition = initialGoalPosition;
            randomStartHalfExtents = initialRandomStartHalfExtents;
        }

        public override void OnEpisodeBegin()
        {
            if (goal == null || agentBody == null)
            {
                return;
            }

            motor?.Stop();
            Vector3 episodeStartPosition = GetRandomStartPosition();
            Quaternion episodeStartRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            agentBody.position = episodeStartPosition;
            agentBody.rotation = episodeStartRotation;
#if UNITY_6000_0_OR_NEWER
            agentBody.linearVelocity = Vector3.zero;
#else
            agentBody.velocity = Vector3.zero;
#endif
            agentBody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(episodeStartPosition, episodeStartRotation);
            goal.position = goalStartPosition;
            Physics.SyncTransforms();
            previousDistanceToGoal = navigationMetrics != null ? navigationMetrics.DistanceToGoal : 0f;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (observationProvider == null ||
                !observationProvider.TryGetObservation(out NavigationGoalObservation observation) ||
                !observation.TryWriteTo(observationBuffer))
            {
                for (int i = 0; i < NavigationGoalObservation.ValueCount; i++)
                {
                    sensor.AddObservation(0f);
                }

                return;
            }

            for (int i = 0; i < NavigationGoalObservation.ValueCount; i++)
            {
                sensor.AddObservation(observationBuffer[i]);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            ActionSegment<float> continuousActions = actions.ContinuousActions;
            float forward = continuousActions.Length > 0 ? continuousActions[0] : 0f;
            float turn = continuousActions.Length > 1 ? continuousActions[1] : 0f;

            motor?.SetInput(forward, turn);
            ApplyActionRewards(Mathf.Clamp01(forward), turn);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
            if (continuousActions.Length < 2)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                continuousActions[0] = 0f;
                continuousActions[1] = 0f;
                return;
            }

            continuousActions[0] = keyboard.wKey.isPressed ? 1f : 0f;
            continuousActions[1] = ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
        }

        public void ReportGoalReached()
        {
            AddReward(goalReward);
            EndEpisode();
        }

        public void ReportWallCollision()
        {
            AddReward(collisionPenalty);
            EndEpisode();
        }

        private void ApplyActionRewards(float forward, float turn)
        {
            if (Mathf.Abs(forward) > movementThreshold || Mathf.Abs(turn) > movementThreshold)
            {
                AddReward(movementReward);
            }

            if (navigationMetrics == null || !navigationMetrics.IsConfigured)
            {
                return;
            }

            float currentDistanceToGoal = navigationMetrics.DistanceToGoal;
            if (currentDistanceToGoal < previousDistanceToGoal)
            {
                AddReward(distanceProgressReward);
            }

            previousDistanceToGoal = currentDistanceToGoal;

            float signedAngleToGoal = navigationMetrics.SignedAngleToGoalDegrees;
            if (signedAngleToGoal < -150f || signedAngleToGoal > 150f)
            {
                AddReward(rearAnglePenalty);
            }
            else if (signedAngleToGoal < -90f || signedAngleToGoal > 90f)
            {
                AddReward(wideAnglePenalty);
            }

            if (Mathf.Abs(forward) <= movementThreshold || Mathf.Abs(turn) <= turnActivityThreshold)
            {
                AddReward(inactivePenalty);
            }
        }

        private Vector3 GetRandomStartPosition()
        {
            for (int attempt = 0; attempt < randomStartMaxAttempts; attempt++)
            {
                Vector3 candidate = new(
                    Random.Range(-randomStartHalfExtents.x, randomStartHalfExtents.x),
                    agentStartPosition.y,
                    Random.Range(-randomStartHalfExtents.y, randomStartHalfExtents.y));

                if (IsClearStartPosition(candidate))
                {
                    return candidate;
                }
            }

            return agentStartPosition;
        }

        private bool IsClearStartPosition(Vector3 position)
        {
            Vector3 bottom = position + Vector3.up * 0.35f;
            Vector3 top = position + Vector3.up * 1.45f;
            return !Physics.CheckCapsule(
                bottom,
                top,
                randomStartClearanceRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
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
