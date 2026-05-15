using EnvForge.Mvp0;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnvForge.Mvp1
{
    [RequireComponent(typeof(AgentMotor))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Unity.MLAgents.Policies.BehaviorParameters))]
    public sealed class NavigationAgent : Agent, INavigationEpisodeEvents
    {
        [SerializeField] private float goalReward = 1f;
        [SerializeField] private float collisionPenalty = -1f;
        [SerializeField] private float stepPenalty = -0.001f;

        private readonly float[] observationBuffer = new float[NavigationObservation.ValueCount];

        private Transform goal;
        private Rigidbody agentBody;
        private AgentMotor motor;
        private NavigationObservationProvider observationProvider;
        private Vector3 agentStartPosition;
        private Quaternion agentStartRotation;
        private Vector3 goalStartPosition;

        public void Configure(
            Transform goalTransform,
            Rigidbody body,
            AgentMotor agentMotor,
            NavigationObservationProvider observations,
            Vector3 initialAgentPosition,
            Quaternion initialAgentRotation,
            Vector3 initialGoalPosition)
        {
            goal = goalTransform;
            agentBody = body;
            motor = agentMotor;
            observationProvider = observations;
            agentStartPosition = initialAgentPosition;
            agentStartRotation = initialAgentRotation;
            goalStartPosition = initialGoalPosition;
        }

        public override void OnEpisodeBegin()
        {
            if (goal == null || agentBody == null)
            {
                return;
            }

            motor?.Stop();
            agentBody.position = agentStartPosition;
            agentBody.rotation = agentStartRotation;
#if UNITY_6000_0_OR_NEWER
            agentBody.linearVelocity = Vector3.zero;
#else
            agentBody.velocity = Vector3.zero;
#endif
            agentBody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(agentStartPosition, agentStartRotation);
            goal.position = goalStartPosition;
            Physics.SyncTransforms();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (observationProvider == null ||
                !observationProvider.TryGetObservation(out NavigationObservation observation) ||
                !observation.TryWriteTo(observationBuffer))
            {
                for (int i = 0; i < NavigationObservation.ValueCount; i++)
                {
                    sensor.AddObservation(0f);
                }

                return;
            }

            for (int i = 0; i < NavigationObservation.ValueCount; i++)
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
            AddReward(stepPenalty);
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

            continuousActions[0] = ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed);
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
