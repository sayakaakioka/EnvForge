namespace EnvForge.Navigation
{
    public interface INavigationEpisodeEvents
    {
        void ReportGoalReached();

        void ReportWallCollision();

        void ReportWallCollision(string wallId);
    }
}
