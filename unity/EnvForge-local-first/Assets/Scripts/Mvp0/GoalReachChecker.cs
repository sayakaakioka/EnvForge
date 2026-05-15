using UnityEngine;

namespace EnvForge.Mvp0
{
    public sealed class GoalReachChecker : MonoBehaviour
    {
        [SerializeField] private float reachRadius = 1.2f;

        private INavigationEpisodeEvents episodeEvents;
        private NavigationMetrics navigationMetrics;
        private bool waitingForExit;

        public void Configure(INavigationEpisodeEvents eventSink, NavigationMetrics metrics, float radius)
        {
            episodeEvents = eventSink;
            navigationMetrics = metrics;
            reachRadius = Mathf.Max(0.01f, radius);
        }

        private void Update()
        {
            if (episodeEvents == null || navigationMetrics == null || !navigationMetrics.IsConfigured)
            {
                return;
            }

            float distanceToGoal = navigationMetrics.DistanceToGoal;
            if (waitingForExit)
            {
                if (distanceToGoal > reachRadius)
                {
                    waitingForExit = false;
                }

                return;
            }

            if (distanceToGoal <= reachRadius)
            {
                waitingForExit = true;
                episodeEvents.ReportGoalReached();
            }
        }
    }
}
