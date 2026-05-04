import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PrometheusService } from '../../../services/prometheus.service';
import Chart from 'chart.js/auto';

@Component({
  selector: 'app-dashboards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboards.html',
  styleUrl: './dashboards.css',
})
export class Dashboards implements OnInit, OnDestroy {
  private readonly prometheus = inject(PrometheusService);

  readonly activeSessions = signal(0);
  readonly totalLogins = signal(0);

  readonly dashboardFiles = [
    { name: 'ASP.NET Core', filename: 'aspnetcore-overview.json' },
    { name: 'Auth & Security', filename: 'auth-security.json' },
    { name: 'Active Sessions', filename: 'active-sessions.json' },
    { name: 'Audit Events', filename: 'audit-events.json' },
  ];

  private chart: Chart | null = null;
  private intervalId: any;

  ngOnInit(): void {
    this.fetchData();
    // Refresh every 15s to match prometheus scrape interval
    this.intervalId = setInterval(() => this.fetchData(), 15000);
  }

  ngOnDestroy(): void {
    if (this.intervalId) clearInterval(this.intervalId);
    if (this.chart) this.chart.destroy();
  }

  private fetchData(): void {
    // Current Active Sessions Gauge
    this.prometheus.query('smooth_operator_active_sessions').subscribe((val) => {
      this.activeSessions.set(val || 0);
    });

    // Total Logins in 24h
    this.prometheus
      .query('sum(increase(smooth_operator_login_attempts_total[24h]))')
      .subscribe((val) => {
        this.totalLogins.set(Math.round(val || 0));
      });

    // Chart: Login attempts rate over last hour (success vs failure)
    const end = Math.floor(Date.now() / 1000);
    const start = end - 3600; // 1 hour ago

    this.prometheus
      .queryRange('rate(smooth_operator_login_attempts_total[5m])', start, end, '1m')
      .subscribe((res) => {
        if (!res || !res.length) return;

        const labels = res[0].values.map((v) => {
          const d = new Date(v[0] * 1000);
          return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        });

        const successData =
          res.find((r) => r.metric['outcome'] === 'success')?.values.map((v) => v[1]) || [];
        const failureData =
          res.find((r) => r.metric['outcome'] === 'failure')?.values.map((v) => v[1]) || [];

        this.updateChart(labels, successData, failureData);
      });
  }

  private updateChart(labels: string[], successData: number[], failureData: number[]): void {
    if (this.chart) {
      this.chart.data.labels = labels;
      this.chart.data.datasets[0].data = successData;
      this.chart.data.datasets[1].data = failureData;
      this.chart.update();
      return;
    }

    const ctx = document.getElementById('loginsChart') as HTMLCanvasElement;
    if (!ctx) return;

    // Use CSS variables for colors if possible
    const primaryColor = 'rgba(34, 197, 94, 0.8)'; // Green for success
    const errorColor = 'rgba(239, 68, 68, 0.8)'; // Red for failure

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Success Rate (req/s)',
            data: successData,
            borderColor: primaryColor,
            backgroundColor: 'rgba(34, 197, 94, 0.1)',
            fill: true,
            tension: 0.4,
          },
          {
            label: 'Failure Rate (req/s)',
            data: failureData,
            borderColor: errorColor,
            backgroundColor: 'rgba(239, 68, 68, 0.1)',
            fill: true,
            tension: 0.4,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false,
        },
        plugins: {
          legend: {
            display: true,
            position: 'bottom',
            labels: {
              color: '#9ca3af', // on-surface-variant approx
            },
          },
        },
        scales: {
          x: {
            grid: { color: 'rgba(156, 163, 175, 0.1)' },
            ticks: { color: '#9ca3af' },
          },
          y: {
            beginAtZero: true,
            grid: { color: 'rgba(156, 163, 175, 0.1)' },
            ticks: { color: '#9ca3af' },
          },
        },
      },
    });
  }
}
