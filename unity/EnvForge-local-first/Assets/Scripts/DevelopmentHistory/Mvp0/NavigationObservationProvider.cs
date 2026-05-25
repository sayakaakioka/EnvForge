using UnityEngine;

namespace EnvForge.Mvp0
{
    public sealed class NavigationObservationProvider : MonoBehaviour
    {
        [SerializeField] private float maxExpectedDistance = 20f;

        private NavigationMetrics navigationMetrics;

        public void Configure(NavigationMetrics metrics, float maxDistance)
        {
            navigationMetrics = metrics;
            maxExpectedDistance = Mathf.Max(0.01f, maxDistance);
        }

        public bool TryGetObservation(out NavigationObservation observation)
        {
            if (navigationMetrics == null || !navigationMetrics.IsConfigured)
            {
                observation = default;
                return false;
            }

            float distanceToGoal = navigationMetrics.DistanceToGoal;
            float signedAngleToGoalDegrees = navigationMetrics.SignedAngleToGoalDegrees;
            float normalizedDistanceToGoal = Mathf.Clamp01(distanceToGoal / maxExpectedDistance);
            float normalizedSignedAngleToGoal = Mathf.Clamp(signedAngleToGoalDegrees / 180f, -1f, 1f);

            observation = new NavigationObservation(
                distanceToGoal,
                signedAngleToGoalDegrees,
                normalizedDistanceToGoal,
                normalizedSignedAngleToGoal);

            return true;
        }

        public string FormatSummary()
        {
            return TryGetObservation(out NavigationObservation observation)
                ? observation.FormatSummary()
                : "observation unavailable";
        }
    }
}
