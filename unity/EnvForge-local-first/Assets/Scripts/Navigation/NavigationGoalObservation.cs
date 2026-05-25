namespace EnvForge.Navigation
{
    public readonly struct NavigationGoalObservation
    {
        public const int ValueCount = 2;

        public NavigationGoalObservation(float normalizedSignedAngleToGoal, float normalizedDistanceToGoal)
        {
            NormalizedSignedAngleToGoal = normalizedSignedAngleToGoal;
            NormalizedDistanceToGoal = normalizedDistanceToGoal;
        }

        public float NormalizedSignedAngleToGoal { get; }

        public float NormalizedDistanceToGoal { get; }

        public bool TryWriteTo(float[] values)
        {
            if (values == null || values.Length < ValueCount)
            {
                return false;
            }

            values[0] = NormalizedSignedAngleToGoal;
            values[1] = NormalizedDistanceToGoal;
            return true;
        }
    }
}
