using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class NavigationGoalObservationProvider : MonoBehaviour
    {
        [SerializeField] private float goalRadius = 1.2f;
        [SerializeField] private float frontDistanceRangeMeters = 5f;
        [SerializeField] private LayerMask frontDistanceLayers = ~0;

        private NavigationMetrics navigationMetrics;

        public void Configure(NavigationMetrics metrics, float maxDistance, float radius = 1.2f)
        {
            navigationMetrics = metrics;
            goalRadius = Mathf.Max(0.01f, radius);
            frontDistanceRangeMeters = Mathf.Max(0.01f, Mathf.Min(5f, maxDistance));
        }

        public bool TryGetObservation(out NavigationGoalObservation observation)
        {
            if (navigationMetrics == null || !navigationMetrics.IsConfigured)
            {
                observation = default;
                return false;
            }

            Vector3 agentPosition = navigationMetrics.AgentPosition;
            Vector3 goalPosition = navigationMetrics.GoalPosition;
            observation = new NavigationGoalObservation(
                agentPosition.x,
                agentPosition.z,
                NormalizeAngleDegrees(navigationMetrics.AgentRotationYDegrees),
                goalPosition.x,
                goalPosition.z,
                goalRadius,
                MeasureFrontDistance(agentPosition, navigationMetrics.AgentForward));
            return true;
        }

        private float MeasureFrontDistance(Vector3 agentPosition, Vector3 agentForward)
        {
            Vector3 origin = agentPosition + Vector3.up * 0.15f;
            Vector3 direction = Vector3.ProjectOnPlane(agentForward, Vector3.up).normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return frontDistanceRangeMeters;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                frontDistanceRangeMeters,
                frontDistanceLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = frontDistanceRangeMeters;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            return nearestDistance;
        }

        private static float NormalizeAngleDegrees(float angleDegrees)
        {
            return Mathf.Repeat(angleDegrees + 180f, 360f) - 180f;
        }
    }
}
