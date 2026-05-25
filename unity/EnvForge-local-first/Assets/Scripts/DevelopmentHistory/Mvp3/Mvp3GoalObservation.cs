namespace EnvForge.Mvp3
{
    public readonly struct Mvp3GoalObservation
    {
        public const int ValueCount = 2;

        public Mvp3GoalObservation(float normalizedSignedAngleToGoal, float normalizedDistanceToGoal)
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
