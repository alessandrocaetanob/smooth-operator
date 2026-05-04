import {
  Component,
  AfterViewInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ElementRef,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { finalize } from 'rxjs/operators';
import {
  Chart,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  ArcElement,
  Title,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';

Chart.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  ArcElement,
  Title,
  Tooltip,
  Legend,
  Filler,
);
import {
  MetricsService,
  MetricsSummary,
  TimeseriesBucket,
  TopEvent,
  OutcomeBreakdown,
} from '../../services/metrics.service';
import { forkJoin } from 'rxjs';

type TimeRange = '1h' | '6h' | '24h' | '7d';
type ChartId =
  | 'connectionsChart'
  | 'loginChart'
  | 'breakdownChart'
  | 'topEventsChart'
  | 'eventTimeseriesChart';

const RANGE_HOURS: Record<TimeRange, number> = { '1h': 1, '6h': 6, '24h': 24, '7d': 168 };
const BUCKET_MINUTES: Record<TimeRange, number> = { '1h': 5, '6h': 15, '24h': 60, '7d': 360 };

type AutoRefreshInterval = 'off' | '30s' | '1m' | '5m' | '15m';
const INTERVAL_MS: Record<string, number> = {
  '30s': 30_000,
  '1m': 60_000,
  '5m': 300_000,
  '15m': 900_000,
};

const CHART_COLORS = [
  { line: 'rgba(99, 102, 241, 0.9)', fill: 'rgba(99, 102, 241, 0.15)' },
  { line: 'rgba(34, 197, 94, 0.9)', fill: 'rgba(34, 197, 94, 0.15)' },
  { line: 'rgba(239, 68, 68, 0.9)', fill: 'rgba(239, 68, 68, 0.15)' },
  { line: 'rgba(234, 179, 8, 0.9)', fill: 'rgba(234, 179, 8, 0.15)' },
  { line: 'rgba(236, 72, 153, 0.9)', fill: 'rgba(236, 72, 153, 0.15)' },
];
const TICK_COLOR = '#9ca3af';
const GRID_COLOR = 'rgba(156, 163, 175, 0.1)';

@Component({
  selector: 'app-monitoring',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './monitoring.html',
  styleUrl: './monitoring.css',
})
export class Monitoring implements AfterViewInit, OnDestroy {
  private readonly metricsService = inject(MetricsService);

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

  readonly activeSessionsColor = computed(() => {
    const n = this.summary()?.activeSessions ?? 0;
    if (n >= 50) return 'text-red-400';
    if (n >= 20) return 'text-yellow-400';
    return 'text-green-400';
  });

  private charts: Record<string, Chart> = {};
  private intervalId: ReturnType<typeof setInterval> | null = null;
  @ViewChild('connectionsChart') private connectionsChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('loginChart') private loginChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('breakdownChart') private breakdownChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('topEventsChart') private topEventsChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('eventTimeseriesChart')
  private eventTimeseriesChartRef?: ElementRef<HTMLCanvasElement>;

  ngAfterViewInit(): void {
    this.loadAll();
  }

  ngOnDestroy(): void {
    if (this.intervalId) clearInterval(this.intervalId);
    Object.values(this.charts).forEach((c) => c.destroy());
    this.charts = {};
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
    if (ms) this.intervalId = setInterval(() => this.loadAll(), ms);
  }

  private get hours(): number {
    return RANGE_HOURS[this.selectedRange()];
  }

  private get bucketMinutes(): number {
    return BUCKET_MINUTES[this.selectedRange()];
  }

  loadAll(): void {
    this.loading.set(true);
    forkJoin({
      summary: this.metricsService.getSummary(),
      loginTs: this.metricsService.getLoginTimeseries(this.hours, this.bucketMinutes),
      connTs: this.metricsService.getConnectionsTimeseries(this.hours, this.bucketMinutes),
      topEvents: this.metricsService.getTopAuditEvents(this.hours, 10),
      breakdown: this.metricsService.getAuditEventBreakdown(this.hours),
      eventTs: this.metricsService.getAuditEventTimeseries(this.hours, this.bucketMinutes),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ summary, loginTs, connTs, topEvents, breakdown, eventTs }) => {
          this.summary.set(summary);
          try {
            this.renderLoginChart(loginTs);
          } catch (e) {
            console.error('[Monitoring] renderLoginChart', e);
          }
          try {
            this.renderConnectionsChart(connTs);
          } catch (e) {
            console.error('[Monitoring] renderConnectionsChart', e);
          }
          try {
            this.renderTopEventsChart(topEvents);
          } catch (e) {
            console.error('[Monitoring] renderTopEventsChart', e);
          }
          try {
            this.renderBreakdownChart(breakdown);
          } catch (e) {
            console.error('[Monitoring] renderBreakdownChart', e);
          }
          try {
            this.renderEventTimeseriesChart(eventTs);
          } catch (e) {
            console.error('[Monitoring] renderEventTimeseriesChart', e);
          }
          this.lastRefreshed.set(new Date());
        },
        error: (err) => console.error('[Monitoring] HTTP error', err),
      });
  }

  // ── Chart helpers ─────────────────────────────────────────────────────────

  private formatLabel(bucket: TimeseriesBucket): string {
    const d = new Date(bucket.timestamp);
    if (this.hours <= 6) return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (this.hours <= 24) return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    return d.toLocaleDateString([], { month: 'short', day: 'numeric', hour: '2-digit' });
  }

  private baseLineOptions(hideLegend = false): Chart['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: {
          display: !hideLegend,
          position: 'bottom',
          labels: { color: TICK_COLOR, boxWidth: 12, padding: 12 },
        },
      },
      scales: {
        x: {
          grid: { color: GRID_COLOR },
          ticks: { color: TICK_COLOR, maxTicksLimit: 8 },
        },
        y: {
          beginAtZero: true,
          grid: { color: GRID_COLOR },
          ticks: { color: TICK_COLOR, precision: 0 },
        },
      },
    };
  }

  private canvasFor(id: ChartId): HTMLCanvasElement | null {
    switch (id) {
      case 'connectionsChart':
        return this.connectionsChartRef?.nativeElement ?? null;
      case 'loginChart':
        return this.loginChartRef?.nativeElement ?? null;
      case 'breakdownChart':
        return this.breakdownChartRef?.nativeElement ?? null;
      case 'topEventsChart':
        return this.topEventsChartRef?.nativeElement ?? null;
      case 'eventTimeseriesChart':
        return this.eventTimeseriesChartRef?.nativeElement ?? null;
      default:
        return null;
    }
  }

  private upsertChart(
    id: ChartId,
    factory: (ctx: HTMLCanvasElement) => Chart,
    updater: (c: Chart) => void,
  ): void {
    const el = this.canvasFor(id);
    if (!el) return;

    const existing = this.charts[id];
    if (existing) {
      if (existing.canvas !== el) {
        existing.destroy();
        delete this.charts[id];
      } else {
        updater(existing);
        existing.update();
        return;
      }
    }

    // If another chart instance was attached to this canvas (e.g. from stale route state),
    // clear it before creating the new instance.
    const stale = Chart.getChart(el);
    if (stale) {
      stale.destroy();
    }

    this.charts[id] = factory(el);
  }

  private renderLoginChart(buckets: TimeseriesBucket[]): void {
    const labels = buckets.map((b) => this.formatLabel(b));
    const successData = buckets.map((b) => b.values['success'] ?? 0);
    const failureData = buckets.map((b) => b.values['failure'] ?? 0);

    this.upsertChart(
      'loginChart',
      (ctx) =>
        new Chart(ctx, {
          type: 'line',
          data: {
            labels,
            datasets: [
              {
                label: 'Success',
                data: successData,
                borderColor: 'rgba(34,197,94,0.9)',
                backgroundColor: 'rgba(34,197,94,0.15)',
                fill: true,
                tension: 0.4,
              },
              {
                label: 'Failure',
                data: failureData,
                borderColor: 'rgba(239,68,68,0.9)',
                backgroundColor: 'rgba(239,68,68,0.15)',
                fill: true,
                tension: 0.4,
              },
            ],
          },
          options: this.baseLineOptions(),
        }),
      (c) => {
        c.data.labels = labels;
        c.data.datasets[0].data = successData;
        c.data.datasets[1].data = failureData;
      },
    );
  }

  private renderConnectionsChart(buckets: TimeseriesBucket[]): void {
    const labels = buckets.map((b) => this.formatLabel(b));
    const startedData = buckets.map((b) => b.values['connection.started'] ?? 0);
    const endedData = buckets.map((b) => b.values['connection.ended'] ?? 0);

    this.upsertChart(
      'connectionsChart',
      (ctx) =>
        new Chart(ctx, {
          type: 'line',
          data: {
            labels,
            datasets: [
              {
                label: 'Started',
                data: startedData,
                borderColor: 'rgba(99,102,241,0.9)',
                backgroundColor: 'rgba(99,102,241,0.15)',
                fill: true,
                tension: 0.4,
              },
              {
                label: 'Ended',
                data: endedData,
                borderColor: 'rgba(156,163,175,0.7)',
                backgroundColor: 'rgba(156,163,175,0.1)',
                fill: true,
                tension: 0.4,
              },
            ],
          },
          options: this.baseLineOptions(),
        }),
      (c) => {
        c.data.labels = labels;
        c.data.datasets[0].data = startedData;
        c.data.datasets[1].data = endedData;
      },
    );
  }

  private renderTopEventsChart(events: TopEvent[]): void {
    const labels = events.map((e) => e.action);
    const data = events.map((e) => e.count);

    this.upsertChart(
      'topEventsChart',
      (ctx) =>
        new Chart(ctx, {
          type: 'bar',
          data: {
            labels,
            datasets: [
              {
                label: 'Count',
                data,
                backgroundColor: 'rgba(99,102,241,0.7)',
                borderRadius: 4,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            indexAxis: 'y',
            plugins: { legend: { display: false } },
            scales: {
              x: {
                beginAtZero: true,
                grid: { color: GRID_COLOR },
                ticks: { color: TICK_COLOR, precision: 0 },
              },
              y: {
                grid: { display: false },
                ticks: { color: TICK_COLOR },
              },
            },
          },
        }),
      (c) => {
        c.data.labels = labels;
        c.data.datasets[0].data = data;
      },
    );
  }

  private renderBreakdownChart(breakdown: OutcomeBreakdown): void {
    const data = [breakdown.success, breakdown.failure];

    this.upsertChart(
      'breakdownChart',
      (ctx) =>
        new Chart(ctx, {
          type: 'doughnut',
          data: {
            labels: ['Success', 'Failure'],
            datasets: [
              {
                data,
                backgroundColor: ['rgba(34,197,94,0.8)', 'rgba(239,68,68,0.8)'],
                borderColor: ['rgba(34,197,94,1)', 'rgba(239,68,68,1)'],
                borderWidth: 1,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '65%',
            plugins: {
              legend: {
                display: true,
                position: 'bottom',
                labels: { color: TICK_COLOR, boxWidth: 12 },
              },
            },
          },
        }),
      (c) => {
        c.data.datasets[0].data = data;
      },
    );
  }

  private renderEventTimeseriesChart(buckets: TimeseriesBucket[]): void {
    const allActions = [...new Set(buckets.flatMap((b) => Object.keys(b.values)))];
    const labels = buckets.map((b) => this.formatLabel(b));

    const datasets = allActions.map((action, i) => {
      const color = CHART_COLORS[i % CHART_COLORS.length];
      return {
        label: action,
        data: buckets.map((b) => b.values[action] ?? 0),
        borderColor: color.line,
        backgroundColor: color.fill,
        fill: false,
        tension: 0.4,
      };
    });

    this.upsertChart(
      'eventTimeseriesChart',
      (ctx) =>
        new Chart(ctx, {
          type: 'line',
          data: { labels, datasets },
          options: this.baseLineOptions(),
        }),
      (c) => {
        c.data.labels = labels;
        c.data.datasets = datasets;
      },
    );
  }
}
