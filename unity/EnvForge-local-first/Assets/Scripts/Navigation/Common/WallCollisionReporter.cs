using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class WallCollisionReporter : MonoBehaviour
    {
        private INavigationEpisodeEvents episodeEvents;
        private string wallId;

        public void Configure(INavigationEpisodeEvents eventSink, string id = "")
        {
            episodeEvents = eventSink;
            wallId = id;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (episodeEvents == null)
            {
                return;
            }

            if (collision.rigidbody != null && collision.rigidbody.GetComponent<AgentMotor>() != null)
            {
                episodeEvents.ReportWallCollision(wallId);
            }
        }
    }
}
