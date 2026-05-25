using UnityEngine;

namespace EnvForge.Mvp0
{
    public sealed class WallCollisionReporter : MonoBehaviour
    {
        private INavigationEpisodeEvents episodeEvents;

        public void Configure(INavigationEpisodeEvents eventSink)
        {
            episodeEvents = eventSink;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (episodeEvents == null)
            {
                return;
            }

            if (collision.rigidbody != null && collision.rigidbody.GetComponent<AgentMotor>() != null)
            {
                episodeEvents.ReportWallCollision();
            }
        }
    }
}
