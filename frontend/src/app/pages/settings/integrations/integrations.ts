import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

interface Endpoint {
  label: string;
  value: string;
}

interface CodeBlock {
  label: string;
  code: string;
}

@Component({
  selector: 'app-integrations',
  imports: [CommonModule],
  templateUrl: './integrations.html',
  styleUrl: './integrations.css',
})
export class Integrations {
  readonly activeTab = signal<'prometheus' | 'otlp' | 'loki' | 'grafana'>('prometheus');

  readonly copiedKey = signal<string | null>(null);

  readonly prometheusEndpoints: Endpoint[] = [
    { label: 'Metrics scrape endpoint', value: 'http://<your-host>:5000/metrics' },
  ];

  readonly prometheusConfig: CodeBlock = {
    label: 'prometheus.yml scrape config',
    code: `global:
  scrape_interval: 15s

scrape_configs:
  - job_name: smooth-operator-backend
    static_configs:
      - targets: ['<your-host>:5000']
    metrics_path: /metrics`,
  };

  readonly prometheusMetrics: string[] = [
    'smooth_operator_login_attempts_total{outcome="success|failure"}',
    'smooth_operator_active_sessions',
    'smooth_operator_connections_started_total',
    'smooth_operator_audit_events_total{action="<action>"}',
    'http_requests_received_total{code="<status>"}',
    'http_request_duration_seconds_bucket',
  ];

  readonly otlpEndpoints: Endpoint[] = [
    { label: 'OTLP gRPC endpoint', value: 'http://<your-host>:4317' },
    { label: 'Tempo HTTP query endpoint', value: 'http://<your-host>:3200' },
  ];

  readonly otlpConfig: CodeBlock = {
    label: 'OpenTelemetry Collector config (receivers)',
    code: `receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  otlp/smooth-operator:
    endpoint: http://<your-host>:4317
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [otlp/smooth-operator]`,
  };

  readonly lokiEndpoints: Endpoint[] = [
    { label: 'Loki push endpoint', value: 'http://<your-host>:3100' },
    { label: 'Loki query API', value: 'http://<your-host>:3100/loki/api/v1/query_range' },
  ];

  readonly lokiQueries: CodeBlock[] = [
    {
      label: 'All audit events',
      code: `{app="smooth-operator"} | json | action != ""`,
    },
    {
      label: 'Failed login attempts',
      code: `{app="smooth-operator"} | json | action = \`user.login_failed\``,
    },
    {
      label: 'Specific user activity',
      code: `{app="smooth-operator"} | json | action != "" | userId = "<user-id>"`,
    },
    {
      label: 'Failure events only',
      code: `{app="smooth-operator"} | json | outcome = \`failure\``,
    },
  ];

  readonly lokiLabels: string[] = [
    'app="smooth-operator"',
    'level="Information|Warning|Error"',
    'EnvironmentName="Development|Production"',
  ];

  readonly grafanaEndpoints: Endpoint[] = [
    { label: 'Grafana UI', value: 'http://localhost:3001' },
    { label: 'Grafana API', value: 'http://localhost:3001/api' },
  ];

  readonly grafanaDashboards = [
    {
      name: 'ASP.NET Core — Overview',
      uid: 'aspnetcore-overview',
      filename: 'aspnetcore-overview.json',
      description: 'HTTP request rate, latency percentiles, error rates, active sessions.',
    },
    {
      name: 'Auth & Security',
      uid: 'auth-security',
      filename: 'auth-security.json',
      description: 'Login attempts, brute-force detection, success/failure rates.',
    },
    {
      name: 'Active Sessions',
      uid: 'active-sessions',
      filename: 'active-sessions.json',
      description: 'Live RDP/SSH session gauge, connection history.',
    },
    {
      name: 'Audit Events',
      uid: 'audit-events',
      filename: 'audit-events.json',
      description: 'Compliance audit event stream, event type distribution.',
    },
  ];

  setTab(tab: 'prometheus' | 'otlp' | 'loki' | 'grafana'): void {
    this.activeTab.set(tab);
  }

  copy(text: string, key: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedKey.set(key);
      setTimeout(() => this.copiedKey.set(null), 2000);
    });
  }

  downloadDashboard(filename: string): void {
    const a = document.createElement('a');
    a.href = `/dashboards/${filename}`;
    a.download = filename;
    a.click();
  }

  copyDashboardJson(uid: string, filename: string): void {
    fetch(`/dashboards/${filename}`)
      .then((r) => r.text())
      .then((text) => {
        navigator.clipboard.writeText(text).then(() => {
          this.copiedKey.set(uid);
          setTimeout(() => this.copiedKey.set(null), 2000);
        });
      });
  }
}
