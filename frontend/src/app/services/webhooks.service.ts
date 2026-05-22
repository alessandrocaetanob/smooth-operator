import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export interface Webhook {
  id: string;
  name: string;
  url: string;
  /** Comma-separated event-type patterns; `*` = all events. */
  eventTypes: string;
  enabled: boolean;
  createdAt: string;
  updatedAt?: string;
  lastDeliveryAt?: string;
  /** `success` | `failed` | undefined (no attempt yet). */
  lastDeliveryStatus?: string;
  lastResponseCode?: number;
  consecutiveFailures: number;
}

export interface CreateWebhookPayload {
  name: string;
  url: string;
  eventTypes: string;
}

export interface UpdateWebhookPayload extends CreateWebhookPayload {
  enabled: boolean;
}

/** Returned once on create or secret rotation — the secret is never retrievable again. */
export interface WebhookSecret {
  id: string;
  name: string;
  secret: string;
}

@Injectable({ providedIn: 'root' })
export class WebhooksService {
  private readonly http = inject(HttpClient);
  private readonly _list = signal<Webhook[]>([]);
  readonly list = this._list.asReadonly();

  load(): Observable<Webhook[]> {
    return this.http.get<RawRecord[]>('/api/webhooks').pipe(
      map((rows) => (rows ?? []).map((r) => this.normalize(r))),
      tap((rows) => this._list.set(rows)),
    );
  }

  create(payload: CreateWebhookPayload): Observable<WebhookSecret> {
    return this.http
      .post<RawRecord>('/api/webhooks', payload)
      .pipe(map((r) => this.normalizeSecret(r)));
  }

  update(id: string, payload: UpdateWebhookPayload): Observable<void> {
    return this.http.put<void>(`/api/webhooks/${id}`, payload);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/webhooks/${id}`);
  }

  rotateSecret(id: string): Observable<WebhookSecret> {
    return this.http
      .post<RawRecord>(`/api/webhooks/${id}/rotate-secret`, {})
      .pipe(map((r) => this.normalizeSecret(r)));
  }

  sendTest(id: string): Observable<void> {
    return this.http.post<void>(`/api/webhooks/${id}/test`, {});
  }

  private normalize(raw: RawRecord): Webhook {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      url: pickOr(raw, '', 'url', 'Url'),
      eventTypes: pickOr(raw, '*', 'eventTypes', 'EventTypes'),
      enabled: pickOr(raw, true, 'enabled', 'Enabled'),
      createdAt: pickOr(raw, '', 'createdAt', 'CreatedAt'),
      updatedAt: pickOr(raw, undefined as string | undefined, 'updatedAt', 'UpdatedAt'),
      lastDeliveryAt: pickOr(
        raw,
        undefined as string | undefined,
        'lastDeliveryAt',
        'LastDeliveryAt',
      ),
      lastDeliveryStatus: pickOr(
        raw,
        undefined as string | undefined,
        'lastDeliveryStatus',
        'LastDeliveryStatus',
      ),
      lastResponseCode: pickOr(
        raw,
        undefined as number | undefined,
        'lastResponseCode',
        'LastResponseCode',
      ),
      consecutiveFailures: pickOr(raw, 0, 'consecutiveFailures', 'ConsecutiveFailures'),
    };
  }

  private normalizeSecret(raw: RawRecord): WebhookSecret {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      name: pickOr(raw, '', 'name', 'Name'),
      secret: pickOr(raw, '', 'secret', 'Secret'),
    };
  }
}
