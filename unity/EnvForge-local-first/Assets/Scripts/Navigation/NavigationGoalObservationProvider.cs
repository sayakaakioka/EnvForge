using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class NavigationGoalObservationProvider : MonoBehaviour
    {
        [SerializeField] private float maxExpectedDistance = 20f;

        private NavigationMetrics navigationMetrics;

        public void Configure(NavigationMetrics metrics, float maxDistance)
        {
            navigationMetrics = metrics;
            maxExpectedDistance = Mathf.Max(0.01f, maxDistance);
        }

        public bool TryGetObservation(out NavigationGoalObservation observation)
        {
            if (navigationMetrics == null || !navigationMetrics.IsConfigured)
            {
                observation = default;
                return false;
            }

            float normalizedSignedAngleToGoal = Mathf.Clamp(navigationMetrics.SignedAngleToGoalDegrees / 180f, -1f, 1f);
            float normalizedDistanceToGoal = Mathf.Clamp01(navigationMetrics.DistanceToGoal / maxExpectedDistance);
            observation = new NavigationGoalObservation(normalizedSignedAngleToGoal, normalizedDistanceToGoal);
            return true;
        }
    }
}
