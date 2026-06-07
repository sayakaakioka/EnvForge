namespace EnvForge.Navigation
{
    public readonly struct NavigationGoalObservation
    {
        public const int RobotValueCount = 3;
        public const int GoalValueCount = 3;
        public const int FrontDistanceValueCount = 1;
        public const int ValueCount = RobotValueCount + GoalValueCount + FrontDistanceValueCount;

        public NavigationGoalObservation(
            float robotX,
            float robotZ,
            float robotRotationYDegrees,
            float goalX,
            float goalZ,
            float goalRadius,
            float frontDistance)
        {
            RobotX = robotX;
            RobotZ = robotZ;
            RobotRotationYDegrees = robotRotationYDegrees;
            GoalX = goalX;
            GoalZ = goalZ;
            GoalRadius = goalRadius;
            FrontDistance = frontDistance;
        }

        public float RobotX { get; }

        public float RobotZ { get; }

        public float RobotRotationYDegrees { get; }

        public float GoalX { get; }

        public float GoalZ { get; }

        public float GoalRadius { get; }

        public float FrontDistance { get; }

        public string FormatSummary()
        {
            return $"obs robot ({RobotX:0.00}, {RobotZ:0.00}, rot {RobotRotationYDegrees:0.0}) · " +
                   $"goal ({GoalX:0.00}, {GoalZ:0.00}, r {GoalRadius:0.00}) · front {FrontDistance:0.00}";
        }

        public bool TryWriteTo(float[] values)
        {
            if (values == null || values.Length < ValueCount)
            {
                return false;
            }

            values[0] = RobotX;
            values[1] = RobotZ;
            values[2] = RobotRotationYDegrees;
            values[3] = GoalX;
            values[4] = GoalZ;
            values[5] = GoalRadius;
            values[6] = FrontDistance;
            return true;
        }

        public bool TryWriteRobotTo(float[] values)
        {
            if (values == null || values.Length < RobotValueCount)
            {
                return false;
            }

            values[0] = RobotX;
            values[1] = RobotZ;
            values[2] = RobotRotationYDegrees;
            return true;
        }

        public bool TryWriteGoalTo(float[] values)
        {
            if (values == null || values.Length < GoalValueCount)
            {
                return false;
            }

            values[0] = GoalX;
            values[1] = GoalZ;
            values[2] = GoalRadius;
            return true;
        }

        public bool TryWriteFrontDistanceTo(float[] values)
        {
            if (values == null || values.Length < FrontDistanceValueCount)
            {
                return false;
            }

            values[0] = FrontDistance;
            return true;
        }
    }
}
