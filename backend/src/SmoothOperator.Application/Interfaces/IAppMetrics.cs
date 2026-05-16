namespace SmoothOperator.Application.Interfaces
{
    public interface IAppMetrics
    {
        void RecordLoginAttempt(string outcome);
        void RecordConnectionStarted();
        void RecordConnectionEnded();
        void RecordAuditEvent(string action);

        /// <summary>
        /// Records the handling of a MediatR request: observes the duration and,
        /// via the histogram's implicit <c>_count</c>, the per-request-type volume.
        /// </summary>
        /// <param name="requestName">The CLR type name of the command/query.</param>
        /// <param name="outcome">"success" or "failure".</param>
        /// <param name="durationSeconds">Wall-clock handling time in seconds.</param>
        void RecordMediatrRequest(string requestName, string outcome, double durationSeconds);

        double CurrentActiveSessions { get; }
    }
}
