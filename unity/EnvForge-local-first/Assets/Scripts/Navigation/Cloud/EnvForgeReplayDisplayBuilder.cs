using System;
using System.Collections.Generic;
using EnvForge.Navigation.Contracts;

namespace EnvForge.Navigation.Cloud
{
    internal static class EnvForgeReplayDisplayBuilder
    {
        public static List<ReplayLogStepDto> BuildDisplaySteps(
            IReadOnlyList<ReplayLogStepDto> rawSteps,
            int displayEnvIndex)
        {
            List<ReplayLogStepDto> displaySteps = new();
            if (rawSteps == null)
            {
                return displaySteps;
            }

            for (int i = 0; i < rawSteps.Count; i++)
            {
                ReplayLogStepDto step = rawSteps[i];
                if (step != null && step.env_index == displayEnvIndex)
                {
                    displaySteps.Add(step);
                }
            }

            return displaySteps;
        }

        public static string FormatSummary(
            IReadOnlyList<ReplayLogStepDto> steps,
            string source,
            string scenarioSource,
            Func<string, int, string> shorten)
        {
            if (steps == null || steps.Count == 0)
            {
                return $"{source}: no steps";
            }

            ReplayLogStepDto first = steps[0];
            ReplayLogStepDto last = steps[steps.Count - 1];
            string episode = string.IsNullOrWhiteSpace(first.episode_id) ? "episode unknown" : first.episode_id;
            int episodeCount = CountEpisodeSegments(steps);
            string scenario = string.IsNullOrWhiteSpace(scenarioSource) ? string.Empty : $" · {scenarioSource}";
            return $"{source}: job {shorten(first.job_id, 18)} · scenario {first.scenario_id ?? "unknown"} · " +
                   $"{episodeCount} ep · first {episode} · {steps.Count} steps · {last.time_seconds:0.0}s{scenario}";
        }

        private static int CountEpisodeSegments(IReadOnlyList<ReplayLogStepDto> steps)
        {
            int count = 0;
            string previousEpisodeId = null;
            for (int i = 0; i < steps.Count; i++)
            {
                string episodeId = string.IsNullOrWhiteSpace(steps[i]?.episode_id) ? "episode_unknown" : steps[i].episode_id;
                if (i == 0 || episodeId != previousEpisodeId)
                {
                    count++;
                    previousEpisodeId = episodeId;
                }
            }

            return count;
        }
    }
}
