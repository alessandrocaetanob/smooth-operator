import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MetricsSummary {
  activeSessions: number;
  loginAttemptsTotal24h: number;
  loginFailures24h: number;
  loginSuccessRate24h: number;
  connectionsStarted24h: number;
  connectionsEnded24h: number;
  auditEventsTotal24h: number;
  auditFailures24h: number;
  authEvents24h: number;
}

export interface TimeseriesBucket {
  timestamp: string;
  values: Record<string, number>;
}

export interface TopEvent {
  action: string;
  count: number;
}

export interface OutcomeBreakdown {
  success: number;
  failure: number;
}

@Injectable({ providedIn: 'root' })
export class MetricsService {
  private readonly http = inject(HttpClient);

  getSummary(): Observable<MetricsSummary> {
    return this.http.get<MetricsSummary>('/api/metrics/summary');
  }

  getLoginTimeseries(hours: number, bucketMinutes: number): Observable<TimeseriesBucket[]> {
    const params = new HttpParams().set('hours', hours).set('bucketMinutes', bucketMinutes);
    return this.http.get<TimeseriesBucket[]>('/api/metrics/logins/timeseries', { params });
  }

  getConnectionsTimeseries(hours: number, bucketMinutes: number): Observable<TimeseriesBucket[]> {
    const params = new HttpParams().set('hours', hours).set('bucketMinutes', bucketMinutes);
    return this.http.get<TimeseriesBucket[]>('/api/metrics/connections/timeseries', { params });
  }

  getTopAuditEvents(hours: number, limit = 10): Observable<TopEvent[]> {
    const params = new HttpParams().set('hours', hours).set('limit', limit);
    return this.http.get<TopEvent[]>('/api/metrics/audit-events/top', { params });
  }

  getAuditEventBreakdown(hours: number): Observable<OutcomeBreakdown> {
    const params = new HttpParams().set('hours', hours);
    return this.http.get<OutcomeBreakdown>('/api/metrics/audit-events/breakdown', { params });
  }

  getAuditEventTimeseries(hours: number, bucketMinutes: number): Observable<TimeseriesBucket[]> {
    const params = new HttpParams().set('hours', hours).set('bucketMinutes', bucketMinutes);
    return this.http.get<TimeseriesBucket[]>('/api/metrics/audit-events/timeseries', { params });
  }
}
