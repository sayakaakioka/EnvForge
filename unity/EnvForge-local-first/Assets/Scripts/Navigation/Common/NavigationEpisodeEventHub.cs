using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class NavigationEpisodeEventHub : MonoBehaviour, INavigationEpisodeEvents
    {
        private INavigationEpisodeEvents defaultSink;
        private INavigationEpisodeEvents overrideSink;

        public void Configure(INavigationEpisodeEvents sink)
        {
            defaultSink = sink;
        }

        public void SetOverrideSink(INavigationEpisodeEvents sink)
        {
            overrideSink = sink;
        }

        public void ClearOverrideSink(INavigationEpisodeEvents sink)
        {
            if (overrideSink == sink)
            {
                overrideSink = null;
            }
        }

        public void ReportGoalReached()
        {
            (overrideSink ?? defaultSink)?.ReportGoalReached();
        }

        public void ReportWallCollision()
        {
            (overrideSink ?? defaultSink)?.ReportWallCollision();
        }

        public void ReportWallCollision(string wallId)
        {
            (overrideSink ?? defaultSink)?.ReportWallCollision(wallId);
        }
    }
}
