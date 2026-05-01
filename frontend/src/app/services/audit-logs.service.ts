import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { pickOr } from './json-utils';

export interface AuditLogEntry {
  id: string;
  timestamp: string;
  userId: string | null;
  userEmail: string | null;
  userName: string | null;
  action: string;
  resourceType: string;
  resourceId: string;
  details: string;
  ipAddress: string;
  outcome: string;
  userAgent: string | null;
  correlationId: string | null;
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  user?: string;
  action?: string;
  resourceType?: string;
  from?: string;
  to?: string;
  outcome?: string;
}

export interface PagedAuditLogs {
  items: AuditLogEntry[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class AuditLogsService {
  private readonly http = inject(HttpClient);
  private readonly _result = signal<PagedAuditLogs>({
    items: [],
    page: 1,
    pageSize: 25,
    totalItems: 0,
    totalPages: 0,
  });
  readonly result = this._result.asReadonly();

  reload(q: AuditLogQuery = {}): Observable<PagedAuditLogs> {
    const params = this.buildParams(q);
    return this.http.get<any>('/api/audit-logs', { params }).pipe(
      tap((raw) => {
        const items = (pickOr<any[]>(raw, [] as any[], 'items', 'Items') ?? []).map((r) =>
          this.normalize(r),
        );
        this._result.set({
          items,
          page: pickOr(raw, 1, 'page', 'Page'),
          pageSize: pickOr(raw, 25, 'pageSize', 'PageSize'),
          totalItems: pickOr(raw, items.length, 'totalItems', 'TotalItems'),
          totalPages: pickOr(raw, 0, 'totalPages', 'TotalPages'),
        });
      }),
    ) as unknown as Observable<PagedAuditLogs>;
  }

  exportCsvUrl(q: AuditLogQuery = {}): string {
    const params = this.buildParams(q);
    const qs = params.toString();
    return qs ? `/api/audit-logs/export?${qs}` : '/api/audit-logs/export';
  }

  private buildParams(q: AuditLogQuery): HttpParams {
    let p = new HttpParams();
    if (q.page) p = p.set('page', String(q.page));
    if (q.pageSize) p = p.set('pageSize', String(q.pageSize));
    if (q.user) p = p.set('user', q.user);
    if (q.action) p = p.set('action', q.action);
    if (q.resourceType) p = p.set('resourceType', q.resourceType);
    if (q.from) p = p.set('from', q.from);
    if (q.to) p = p.set('to', q.to);
    if (q.outcome) p = p.set('outcome', q.outcome);
    return p;
  }

  private normalize(raw: any): AuditLogEntry {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      timestamp: pickOr(raw, '', 'timestamp', 'Timestamp'),
      userId: pickOr<string | null>(raw, null, 'userId', 'UserId'),
      userEmail: pickOr<string | null>(raw, null, 'userEmail', 'UserEmail'),
      userName: pickOr<string | null>(raw, null, 'userName', 'UserName'),
      action: pickOr(raw, '', 'action', 'Action'),
      resourceType: pickOr(raw, '', 'resourceType', 'ResourceType'),
      resourceId: pickOr(raw, '', 'resourceId', 'ResourceId'),
      details: pickOr(raw, '', 'details', 'Details'),
      ipAddress: pickOr(raw, '', 'ipAddress', 'IpAddress'),
      outcome: pickOr(raw, 'success', 'outcome', 'Outcome'),
      userAgent: pickOr<string | null>(raw, null, 'userAgent', 'UserAgent'),
      correlationId: pickOr<string | null>(raw, null, 'correlationId', 'CorrelationId'),
    };
  }
}
