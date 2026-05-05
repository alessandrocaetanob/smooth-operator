namespace SmoothOperator.Application.Interfaces
{
    public interface IAppMetrics
    {
        void RecordLoginAttempt(string outcome);
        void RecordConnectionStarted();
        void RecordConnectionEnded();
        void RecordAuditEvent(string action);
        double CurrentActiveSessions { get; }
    }
}
