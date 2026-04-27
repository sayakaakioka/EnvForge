using UnityEngine;

public static class SubmitRequestBuilder
{
    public static SubmitRequestData Build(EnvironmentManager environmentManager, string robotType, TrainingRequestData training)
    {
        if (environmentManager == null)
        {
            Debug.LogError("SubmitRequestBuilder: EnvironmentManager is not assigned.");
            return null;
        }

        if (environmentManager.Goal == null || environmentManager.RobotStart == null)
        {
            Debug.LogError("SubmitRequestBuilder: Goal or robot start is not assigned.");
            return null;
        }

        var obstacles = new ObstacleData[environmentManager.Obstacles.Count];
        for (int i = 0; i < obstacles.Length; i++)
        {
            var obstacle = environmentManager.Obstacles[i];
            obstacles[i] = new ObstacleData
            {
                x = obstacle.X,
                y = obstacle.Y
            };
        }

        return new SubmitRequestData
        {
            environment = new EnvironmentRequestData
            {
                size = new[] { environmentManager.Width, environmentManager.Height },
                obstacles = obstacles,
                goal = new PositionData
                {
                    x = environmentManager.Goal.X,
                    y = environmentManager.Goal.Y
                },
                robot_start = new PositionData
                {
                    x = environmentManager.RobotStart.X,
                    y = environmentManager.RobotStart.Y
                }
            },
            robot = new RobotData
            {
                type = robotType
            },
            training = training
        };
    }
}
