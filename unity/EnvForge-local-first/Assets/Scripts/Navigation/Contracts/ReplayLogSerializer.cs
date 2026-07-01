using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnvForge.Navigation.Contracts
{
    public static class ReplayLogSerializer
    {
        public static ReplayLogStepDto FromReplayLogStepJson(string json)
        {
            return JsonUtility.FromJson<ReplayLogStepDto>(json);
        }

        public static IReadOnlyList<ReplayLogStepDto> FromJsonLines(string jsonLines)
        {
            if (string.IsNullOrWhiteSpace(jsonLines))
            {
                return Array.Empty<ReplayLogStepDto>();
            }

            string[] lines = jsonLines.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<ReplayLogStepDto> steps = new(lines.Length);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                steps.Add(FromReplayLogStepJson(line));
            }

            return steps;
        }
    }
}
