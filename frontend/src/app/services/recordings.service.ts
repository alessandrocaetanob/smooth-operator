import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { pickOr, RawRecord } from './json-utils';

export type RecordingStatus = 'Recording' | 'Uploading' | 'Available' | 'Failed' | 'Deleted';
export type RecordingStorageType = 'Local' | 'S3' | 'AzureBlob';

export interface Recording {
  id: string;
  sessionId: string;
  connectionId: string;
  connectionName: string;
  protocol: string;
  vaultId?: string | null;
  vaultName?: string | null;
  userId: string;
  userEmail?: string | null;
  startedAt: string;
  endedAt?: string | null;
  durationSeconds?: number | null;
  storageType: RecordingStorageType;
  fileSizeBytes?: number | null;
  status: RecordingStatus;
  errorMessage?: string | null;
  includeKeys: boolean;
}

export interface RecordingsList {
  total: number;
  page: number;
  pageSize: number;
  items: Recording[];
}

export interface ListRecordingsFilters {
  userId?: string | null;
  connectionId?: string | null;
  vaultId?: string | null;
  from?: string | null;
  to?: string | null;
  status?: RecordingStatus | null;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class RecordingsService {
  private readonly http = inject(HttpClient);

  list(filters: ListRecordingsFilters = {}): Observable<RecordingsList> {
    let params = new HttpParams();
    if (filters.userId) params = params.set('userId', filters.userId);
    if (filters.connectionId) params = params.set('connectionId', filters.connectionId);
    if (filters.vaultId) params = params.set('vaultId', filters.vaultId);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.page) params = params.set('page', String(filters.page));
    if (filters.pageSize) params = params.set('pageSize', String(filters.pageSize));

    return this.http
      .get<RawRecord>('/api/recordings', { params })
      .pipe(map((r) => this.normalizeList(r)));
  }

  /** Returns the recording bytes as an ArrayBuffer for the Guacamole.SessionRecording player. */
  fetchStream(id: string): Observable<ArrayBuffer> {
    return this.http.get(`/api/recordings/${id}/stream`, { responseType: 'arraybuffer' });
  }

  /** Direct URL — used for download (Save As) so the browser handles the file dialog. */
  streamUrl(id: string): string {
    return `/api/recordings/${id}/stream`;
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/recordings/${id}`);
  }

  private normalizeList(raw: RawRecord): RecordingsList {
    const items = pickOr<RawRecord[]>(raw, [], 'items', 'Items');
    return {
      total: pickOr(raw, 0, 'total', 'Total'),
      page: pickOr(raw, 1, 'page', 'Page'),
      pageSize: pickOr(raw, 25, 'pageSize', 'PageSize'),
      items: items.map((r) => this.normalizeRecording(r)),
    };
  }

  private normalizeRecording(raw: RawRecord): Recording {
    return {
      id: pickOr(raw, '', 'id', 'Id'),
      sessionId: pickOr(raw, '', 'sessionId', 'SessionId'),
      connectionId: pickOr(raw, '', 'connectionId', 'ConnectionId'),
      connectionName: pickOr(raw, '', 'connectionName', 'ConnectionName'),
      protocol: pickOr(raw, '', 'protocol', 'Protocol'),
      vaultId: pickOr(raw, null as string | null, 'vaultId', 'VaultId'),
      vaultName: pickOr(raw, null as string | null, 'vaultName', 'VaultName'),
      userId: pickOr(raw, '', 'userId', 'UserId'),
      userEmail: pickOr(raw, null as string | null, 'userEmail', 'UserEmail'),
      startedAt: pickOr(raw, '', 'startedAt', 'StartedAt'),
      endedAt: pickOr(raw, null as string | null, 'endedAt', 'EndedAt'),
      durationSeconds: pickOr(raw, null as number | null, 'durationSeconds', 'DurationSeconds'),
      storageType: pickOr<RecordingStorageType>(raw, 'Local', 'storageType', 'StorageType'),
      fileSizeBytes: pickOr(raw, null as number | null, 'fileSizeBytes', 'FileSizeBytes'),
      status: pickOr<RecordingStatus>(raw, 'Available', 'status', 'Status'),
      errorMessage: pickOr(raw, null as string | null, 'errorMessage', 'ErrorMessage'),
      includeKeys: pickOr(raw, false, 'includeKeys', 'IncludeKeys'),
    };
  }
}
