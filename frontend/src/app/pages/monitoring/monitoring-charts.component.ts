import {
  Component,
  AfterViewInit,
  OnDestroy,
  ElementRef,
  ViewChild,
  effect,
  input,
  inject,
  Injector,
  runInInjectionContext,
} from '@angular/core';
import {
  Chart,
  LineController,
  BarController,
  DoughnutController,
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
import { TimeseriesBucket, TopEvent, OutcomeBreakdown } from '../../services/metrics.service';

Chart.register(
  LineController,
  BarController,
  DoughnutController,
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

export interface MonitoringChartData {
  loginTs: TimeseriesBucket[];
  connTs: TimeseriesBucket[];
  topEvents: TopEvent[];
  breakdown: OutcomeBreakdown;
  eventTs: TimeseriesBucket[];
  hours: number;
}

type ChartId =
  | 'connectionsChart'
  | 'loginChart'
  | 'breakdownChart'
  | 'topEventsChart'
  | 'eventTimeseriesChart';

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
  selector: 'app-monitoring-charts',
  standalone: true,
  imports: [],
  templateUrl: './monitoring-charts.component.html',
  styleUrl: './monitoring-charts.component.css',
})
export class MonitoringChartsComponent implements AfterViewInit, OnDestroy {
  readonly data = input<MonitoringChartData | null>(null);

  @ViewChild('connectionsChart') private connectionsChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('loginChart') private loginChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('breakdownChart') private breakdownChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('topEventsChart') private topEventsChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('eventTimeseriesChart')
  private eventTimeseriesChartRef?: ElementRef<HTMLCanvasElement>;

  private readonly injector = inject(Injector);
  private charts: Record<string, Chart> = {};
  private viewInitialized = false;

  ngAfterViewInit(): void {
    this.viewInitialized = true;
    const d = this.data();
    if (d) this.renderAll(d);

    // React to future data changes after view is ready.
    runInInjectionContext(this.injector, () => {
      effect(() => {
        const incoming = this.data();
        if (incoming && this.viewInitialized) {
          this.renderAll(incoming);
        }
      });
    });
  }

  ngOnDestroy(): void {
    Object.values(this.charts).forEach((c) => c.destroy());
    this.charts = {};
  }

  private renderAll(d: MonitoringChartData): void {
    const hours = d.hours;
    try {
      this.renderLoginChart(d.loginTs, hours);
    } catch (e) {
      console.error('[MonitoringCharts] renderLoginChart', e);
    }
    try {
      this.renderConnectionsChart(d.connTs, hours);
    } catch (e) {
      console.error('[MonitoringCharts] renderConnectionsChart', e);
    }
    try {
      this.renderTopEventsChart(d.topEvents);
    } catch (e) {
      console.error('[MonitoringCharts] renderTopEventsChart', e);
    }
    try {
      this.renderBreakdownChart(d.breakdown);
    } catch (e) {
      console.error('[MonitoringCharts] renderBreakdownChart', e);
    }
    try {
      this.renderEventTimeseriesChart(d.eventTs, hours);
    } catch (e) {
      console.error('[MonitoringCharts] renderEventTimeseriesChart', e);
    }
  }

  private formatLabel(bucket: TimeseriesBucket, hours: number): string {
    const d = new Date(bucket.timestamp);
    if (hours <= 6) return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (hours <= 24) return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
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

    // Clear any stale chart instance attached to this canvas element.
    const stale = Chart.getChart(el);
    if (stale) stale.destroy();

    this.charts[id] = factory(el);
  }

  private renderLoginChart(buckets: TimeseriesBucket[], hours: number): void {
    const labels = buckets.map((b) => this.formatLabel(b, hours));
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

  private renderConnectionsChart(buckets: TimeseriesBucket[], hours: number): void {
    const labels = buckets.map((b) => this.formatLabel(b, hours));
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

  private renderEventTimeseriesChart(buckets: TimeseriesBucket[], hours: number): void {
    const allActions = [...new Set(buckets.flatMap((b) => Object.keys(b.values)))];
    const labels = buckets.map((b) => this.formatLabel(b, hours));

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
