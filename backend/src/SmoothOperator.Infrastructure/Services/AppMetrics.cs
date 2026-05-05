using Prometheus;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Infrastructure.Services
{
    /// <summary>
    /// Centralised Prometheus counter/gauge definitions for Smooth Operator.
    /// Metrics are singleton-safe: Counter/Gauge instances are thread-safe by design.
    /// </summary>
    public class AppMetrics : IAppMetrics
    {
        private static readonly Counter LoginAttempts = Metrics.CreateCounter(
            "smooth_operator_login_attempts_total",
            "Total login attempts, labelled by outcome (success/failure).",
            "outcome");

        private static readonly Gauge ActiveSessions = Metrics.CreateGauge(
            "smooth_operator_active_sessions",
            "Number of currently active Guacamole proxy sessions.");

        private static readonly Counter ConnectionsStarted = Metrics.CreateCounter(
            "smooth_operator_connections_started_total",
            "Total remote desktop/SSH connections initiated.");

        private static readonly Counter ConnectionsEnded = Metrics.CreateCounter(
            "smooth_operator_connections_ended_total",
            "Total remote desktop/SSH connections closed.");

        private static readonly Counter AuditEvents = Metrics.CreateCounter(
            "smooth_operator_audit_events_total",
            "Total audit events recorded, labelled by action.",
            "action");

        public void RecordLoginAttempt(string outcome) =>
            LoginAttempts.WithLabels(outcome).Inc();

        public void RecordConnectionStarted()
        {
            ConnectionsStarted.Inc();
            ActiveSessions.Inc();
        }

        public void RecordConnectionEnded()
        {
            ConnectionsEnded.Inc();
            ActiveSessions.Dec();
        }

        public void RecordAuditEvent(string action) =>
            AuditEvents.WithLabels(action).Inc();

        public double CurrentActiveSessions => ActiveSessions.Value;
    }
}
