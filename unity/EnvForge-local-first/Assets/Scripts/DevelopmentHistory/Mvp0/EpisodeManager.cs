using UnityEngine;

namespace EnvForge.Mvp0
{
    public sealed class EpisodeManager : MonoBehaviour, INavigationEpisodeEvents
    {
        private Transform agent;
        private Transform goal;
        private Rigidbody agentBody;
        private AgentMotor agentMotor;
        private NavigationMetrics navigationMetrics;
        private NavigationObservationProvider observationProvider;
        private Vector3 agentStartPosition;
        private Quaternion agentStartRotation;
        private Vector3 goalStartPosition;
        private int episodeIndex;
        private int successCount;
        private int failureCount;
        private int manualResetCount;

        public void Initialize(
            Transform agentTransform,
            Transform goalTransform,
            Rigidbody agentRigidbody,
            AgentMotor motor,
            NavigationMetrics metrics,
            NavigationObservationProvider observations,
            Vector3 initialAgentPosition,
            Quaternion initialAgentRotation,
            Vector3 initialGoalPosition)
        {
            agent = agentTransform;
            goal = goalTransform;
            agentBody = agentRigidbody;
            agentMotor = motor;
            navigationMetrics = metrics;
            observationProvider = observations;
            agentStartPosition = initialAgentPosition;
            agentStartRotation = initialAgentRotation;
            goalStartPosition = initialGoalPosition;

            ResetEpisode("initial setup");
        }

        public void ReportGoalReached()
        {
            successCount++;
            Debug.Log($"MVP 0 episode {episodeIndex}: success ({FormatMetrics()}; {FormatObservation()})");
            ResetEpisode("goal reached");
        }

        public void ReportWallCollision()
        {
            failureCount++;
            Debug.Log($"MVP 0 episode {episodeIndex}: failure ({FormatMetrics()}; {FormatObservation()})");
            ResetEpisode("wall collision");
        }

        public void RequestManualReset()
        {
            manualResetCount++;
            Debug.Log($"MVP 0 episode {episodeIndex}: manual reset ({FormatMetrics()}; {FormatObservation()})");
            ResetEpisode("manual reset");
        }

        private void ResetEpisode(string reason)
        {
            if (agent == null || goal == null || agentBody == null)
            {
                Debug.LogWarning("MVP 0 reset requested before the episode was initialized.");
                return;
            }

            episodeIndex++;
            agentMotor?.Stop();
            agentBody.position = agentStartPosition;
            agentBody.rotation = agentStartRotation;
#if UNITY_6000_0_OR_NEWER
            agentBody.linearVelocity = Vector3.zero;
#else
            agentBody.velocity = Vector3.zero;
#endif
            agentBody.angularVelocity = Vector3.zero;
            agent.SetPositionAndRotation(agentStartPosition, agentStartRotation);
            goal.position = goalStartPosition;
            Physics.SyncTransforms();

            Debug.Log($"MVP 0 episode {episodeIndex}: reset ({reason}); success={successCount}, failure={failureCount}, manualReset={manualResetCount}, {FormatMetrics()}; {FormatObservation()}");
        }

        private string FormatMetrics()
        {
            return navigationMetrics != null ? navigationMetrics.FormatSummary() : "metrics unavailable";
        }

        private string FormatObservation()
        {
            return observationProvider != null ? observationProvider.FormatSummary() : "observation unavailable";
        }
    }
}
