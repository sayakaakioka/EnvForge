namespace EnvForge.Mvp0
{
    public readonly struct NavigationObservation
    {
        public const int ValueCount = 4;

        public NavigationObservation(
            float distanceToGoal,
            float signedAngleToGoalDegrees,
            float normalizedDistanceToGoal,
            float normalizedSignedAngleToGoal)
        {
            DistanceToGoal = distanceToGoal;
            SignedAngleToGoalDegrees = signedAngleToGoalDegrees;
            NormalizedDistanceToGoal = normalizedDistanceToGoal;
            NormalizedSignedAngleToGoal = normalizedSignedAngleToGoal;
        }

        public float DistanceToGoal { get; }

        public float SignedAngleToGoalDegrees { get; }

        public float NormalizedDistanceToGoal { get; }

        public float NormalizedSignedAngleToGoal { get; }

        public string FormatSummary()
        {
            return $"obsDistance={DistanceToGoal:F2}, obsAngle={SignedAngleToGoalDegrees:F1} deg, normDistance={NormalizedDistanceToGoal:F3}, normAngle={NormalizedSignedAngleToGoal:F3}";
        }

        public bool TryWriteTo(float[] values)
        {
            if (values == null || values.Length < ValueCount)
            {
                return false;
            }

            values[0] = NormalizedDistanceToGoal;
            values[1] = NormalizedSignedAngleToGoal;
            values[2] = DistanceToGoal;
            values[3] = SignedAngleToGoalDegrees;
            return true;
        }
    }
}
