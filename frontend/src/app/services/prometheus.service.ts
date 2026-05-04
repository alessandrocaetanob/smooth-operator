import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface PromResult {
  metric: Record<string, string>;
  value: [number, string]; // [timestamp, value]
}

export interface PromQueryResponse {
  status: string;
  data: {
    resultType: string;
    result: PromResult[];
  };
}

export interface PromQueryRangeResponse {
  status: string;
  data: {
    resultType: string;
    result: {
      metric: Record<string, string>;
      values: [number, string][];
    }[];
  };
}

@Injectable({ providedIn: 'root' })
export class PrometheusService {
  private readonly http = inject(HttpClient);

  query(query: string): Observable<number> {
    const params = new HttpParams().set('query', query);
    return this.http.get<PromQueryResponse>('/api/v1/query', { params }).pipe(
      map((res) => {
        if (res.status !== 'success' || !res.data.result.length) return 0;
        return parseFloat(res.data.result[0].value[1]);
      }),
    );
  }

  queryRange(
    query: string,
    start: number,
    end: number,
    step: string,
  ): Observable<{ metric: Record<string, string>; values: [number, number][] }[]> {
    const params = new HttpParams()
      .set('query', query)
      .set('start', start.toString())
      .set('end', end.toString())
      .set('step', step);

    return this.http.get<PromQueryRangeResponse>('/api/v1/query_range', { params }).pipe(
      map((res) => {
        if (res.status !== 'success') return [];
        return res.data.result.map((r) => ({
          metric: r.metric,
          values: r.values.map((v) => [v[0], parseFloat(v[1])] as [number, number]),
        }));
      }),
    );
  }
}
