using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class NavigationMetrics : MonoBehaviour
    {
        private Transform agent;
        private Transform goal;

        public void Configure(Transform agentTransform, Transform goalTransform)
        {
            agent = agentTransform;
            goal = goalTransform;
        }

        public bool IsConfigured => agent != null && goal != null;

        public Vector3 AgentPosition => IsConfigured ? agent.position : Vector3.zero;

        public Vector3 GoalPosition => IsConfigured ? goal.position : Vector3.zero;

        public Vector3 AgentForward => IsConfigured ? agent.forward : Vector3.forward;

        public float AgentRotationYDegrees => IsConfigured ? NormalizeAngle(agent.eulerAngles.y) : 0f;

        public float DistanceToGoal
        {
            get
            {
                if (!IsConfigured)
                {
                    return 0f;
                }

                Vector3 agentPosition = Flatten(agent.position);
                Vector3 goalPosition = Flatten(goal.position);
                return Vector3.Distance(agentPosition, goalPosition);
            }
        }

        public float SignedAngleToGoalDegrees
        {
            get
            {
                if (!IsConfigured)
                {
                    return 0f;
                }

                Vector3 toGoal = Flatten(goal.position - agent.position);
                if (toGoal.sqrMagnitude <= Mathf.Epsilon)
                {
                    return 0f;
                }

                Vector3 forward = Flatten(agent.forward);
                return Vector3.SignedAngle(forward, toGoal.normalized, Vector3.up);
            }
        }

        public string FormatSummary()
        {
            if (!IsConfigured)
            {
                return "metrics unavailable";
            }

            return $"distance={DistanceToGoal:F2}, angle={SignedAngleToGoalDegrees:F1} deg";
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static float NormalizeAngle(float angleDegrees)
        {
            return Mathf.Repeat(angleDegrees + 180f, 360f) - 180f;
        }
    }
}
