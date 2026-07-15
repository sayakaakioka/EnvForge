using System;
using System.Collections.Generic;
using EmbodiedLab.Contracts;

namespace EnvForge.Navigation.Cloud
{
    internal static class EnvForgeReplayDisplayBuilder
    {
        public static List<ReplayLogStep> BuildDisplaySteps(
            IReadOnlyList<ReplayLogStep> rawSteps,
            int displayEnvIndex)
        {
            List<ReplayLogStep> displaySteps = new();
            if (rawSteps == null)
            {
                return displaySteps;
            }

            for (int i = 0; i < rawSteps.Count; i++)
            {
                ReplayLogStep step = rawSteps[i];
                if (step != null && step.EnvIndex == displayEnvIndex)
                {
                    displaySteps.Add(step);
                }
            }

            return displaySteps;
        }

        public static string FormatSummary(
            IReadOnlyList<ReplayLogStep> steps,
            string source,
            string scenarioSource,
            Func<string, int, string> shorten)
        {
            if (steps == null || steps.Count == 0)
            {
                return $"{source}: no steps";
            }

            ReplayLogStep first = steps[0];
            ReplayLogStep last = steps[steps.Count - 1];
            string episode = string.IsNullOrWhiteSpace(first.EpisodeId) ? "episode unknown" : first.EpisodeId;
            int episodeCount = CountEpisodeSegments(steps);
            string scenario = string.IsNullOrWhiteSpace(scenarioSource) ? string.Empty : $" · {scenarioSource}";
            return $"{source}: job {shorten(first.JobId, 18)} · scenario {first.ScenarioId ?? "unknown"} · " +
                   $"{episodeCount} ep · first {episode} · {steps.Count} steps · {last.TimeSeconds:0.0}s{scenario}";
        }

        private static int CountEpisodeSegments(IReadOnlyList<ReplayLogStep> steps)
        {
            int count = 0;
            string previousEpisodeId = null;
            for (int i = 0; i < steps.Count; i++)
            {
                string episodeId = string.IsNullOrWhiteSpace(steps[i]?.EpisodeId)
                    ? "episode_unknown"
                    : steps[i].EpisodeId;
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
