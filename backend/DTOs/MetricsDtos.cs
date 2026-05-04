using System;
using System.Collections.Generic;

namespace Backend.DTOs
{
    public class MetricsSummaryDto
    {
        public double ActiveSessions { get; set; }
        public long LoginAttemptsTotal24h { get; set; }
        public long LoginFailures1h { get; set; }
        public double LoginSuccessRate24h { get; set; }
        public long ConnectionsStarted24h { get; set; }
        public long ConnectionsEnded24h { get; set; }
        public long AuditEventsTotal24h { get; set; }
        public long AuditFailures24h { get; set; }
        public long AuthEvents24h { get; set; }
    }

    public class TimeseriesBucketDto
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, long> Values { get; set; } = new();
    }

    public class TopEventDto
    {
        public string Action { get; set; } = string.Empty;
        public long Count { get; set; }
    }

    public class OutcomeBreakdownDto
    {
        public long Success { get; set; }
        public long Failure { get; set; }
    }
}
