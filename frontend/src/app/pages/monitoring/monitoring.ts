import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin, Subscription } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { MetricsService, MetricsSummary } from '../../services/metrics.service';
import { MonitoringChartsComponent, MonitoringChartData } from './monitoring-charts.component';

type TimeRange = '1h' | '6h' | '24h' | '7d';

const RANGE_HOURS: Record<TimeRange, number> = { '1h': 1, '6h': 6, '24h': 24, '7d': 168 };
const BUCKET_MINUTES: Record<TimeRange, number> = { '1h': 5, '6h': 15, '24h': 60, '7d': 360 };

type AutoRefreshInterval = 'off' | '30s' | '1m' | '5m' | '15m';
const INTERVAL_MS: Record<string, number> = {
  '30s': 30_000,
  '1m': 60_000,
  '5m': 300_000,
  '15m': 900_000,
};

@Component({
  selector: 'app-monitoring',
  standalone: true,
  imports: [CommonModule, MonitoringChartsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring.html',
  styleUrl: './monitoring.css',
})
export class Monitoring implements OnInit, OnDestroy {
  private readonly metricsService = inject(MetricsService);
  private readonly handleVisibilityChange = (): void => {
    if (document.visibilityState === 'visible' && this.autoRefresh() !== 'off') {
      this.loadAll();
    }
  };

  readonly selectedRange = signal<TimeRange>('24h');
  readonly ranges: TimeRange[] = ['1h', '6h', '24h', '7d'];
  readonly loading = signal(false);
  readonly lastRefreshed = signal<Date | null>(null);

  readonly autoRefresh = signal<AutoRefreshInterval>('off');
  readonly autoRefreshOptions: { label: string; value: AutoRefreshInterval }[] = [
    { label: 'Off', value: 'off' },
    { label: '30s', value: '30s' },
    { label: '1m', value: '1m' },
    { label: '5m', value: '5m' },
    { label: '15m', value: '15m' },
  ];

  readonly summary = signal<MetricsSummary | null>(null);
  readonly chartData = signal<MonitoringChartData | null>(null);

  readonly activeSessionsColor = computed(() => {
    const n = this.summary()?.activeSessions ?? 0;
    if (n >= 50) return 'text-red-400';
    if (n >= 20) return 'text-yellow-400';
    return 'text-green-400';
  });

  private intervalId: ReturnType<typeof setInterval> | null = null;
  private inflight: Subscription | null = null;

  ngOnInit(): void {
    this.loadAll();
    document.addEventListener('visibilitychange', this.handleVisibilityChange, { passive: true });
  }

  ngOnDestroy(): void {
    if (this.intervalId) clearInterval(this.intervalId);
    this.inflight?.unsubscribe();
    this.inflight = null;
    document.removeEventListener('visibilitychange', this.handleVisibilityChange);
  }

  setRange(range: TimeRange): void {
    this.selectedRange.set(range);
    this.loadAll();
  }

  setAutoRefresh(val: AutoRefreshInterval): void {
    this.autoRefresh.set(val);
    this.restartTimer();
  }

  private restartTimer(): void {
    if (this.intervalId) clearInterval(this.intervalId);
    this.intervalId = null;
    const ms = INTERVAL_MS[this.autoRefresh()];
    if (ms) {
      this.intervalId = setInterval(() => {
        if (document.visibilityState === 'visible') {
          this.loadAll();
        }
      }, ms);
    }
  }

  private get hours(): number {
    return RANGE_HOURS[this.selectedRange()];
  }

  private get bucketMinutes(): number {
    return BUCKET_MINUTES[this.selectedRange()];
  }

  loadAll(): void {
    // Cancel any in-flight refresh so concurrent triggers (manual + range change + auto)
    // don't race and leave `loading` flickering or apply stale responses.
    this.inflight?.unsubscribe();
    this.loading.set(true);
    this.inflight = forkJoin({
      summary: this.metricsService.getSummary(),
      loginTs: this.metricsService.getLoginTimeseries(this.hours, this.bucketMinutes),
      connTs: this.metricsService.getConnectionsTimeseries(this.hours, this.bucketMinutes),
      topEvents: this.metricsService.getTopAuditEvents(this.hours, 10),
      breakdown: this.metricsService.getAuditEventBreakdown(this.hours),
      eventTs: this.metricsService.getAuditEventTimeseries(this.hours, this.bucketMinutes),
    })
      .pipe(
        finalize(() => {
          this.loading.set(false);
          this.inflight = null;
        }),
      )
      .subscribe({
        next: ({ summary, loginTs, connTs, topEvents, breakdown, eventTs }) => {
          this.summary.set(summary);
          this.chartData.set({ loginTs, connTs, topEvents, breakdown, eventTs, hours: this.hours });
          this.lastRefreshed.set(new Date());
        },
        error: (err) => console.error('[Monitoring] HTTP error', err),
      });
  }
}
