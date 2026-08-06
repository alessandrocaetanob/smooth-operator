import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

interface Endpoint {
  label: string;
  value: string;
}

interface CodeBlock {
  label: string;
  code: string;
}

interface GrafanaDashboard {
  name: string;
  description: string;
  filename: string;
  url: string;
}

@Component({
  selector: 'app-integrations',
  imports: [CommonModule, TranslatePipe],
  templateUrl: './integrations.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './integrations.css',
})
export class Integrations {
  readonly activeTab = signal<'prometheus' | 'otlp' | 'loki' | 'grafana'>('prometheus');

  readonly copiedKey = signal<string | null>(null);

  readonly prometheusEndpoints: Endpoint[] = [
    {
      label: 'pages.settingsIntegrations.prometheus.endpoints.metricsScrape',
      value: 'https://<your-host>:5000/metrics',
    },
  ];

  readonly prometheusConfig: CodeBlock = {
    label: 'pages.settingsIntegrations.prometheus.scrapeConfigLabel',
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
    {
      label: 'pages.settingsIntegrations.otlp.endpoints.grpc',
      value: 'https://<your-host>:4317',
    },
    {
      label: 'pages.settingsIntegrations.otlp.endpoints.tempoQuery',
      value: 'https://<your-host>:3200',
    },
  ];

  readonly otlpConfig: CodeBlock = {
    label: 'pages.settingsIntegrations.otlp.collectorConfigLabel',
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
    {
      label: 'pages.settingsIntegrations.loki.endpoints.push',
      value: 'https://<your-host>:3100',
    },
    {
      label: 'pages.settingsIntegrations.loki.endpoints.queryApi',
      value: 'https://<your-host>:3100/loki/api/v1/query_range',
    },
  ];

  readonly lokiQueries: CodeBlock[] = [
    {
      label: 'pages.settingsIntegrations.loki.queries.allAuditEvents',
      code: `{app="smooth-operator"} | json | action != ""`,
    },
    {
      label: 'pages.settingsIntegrations.loki.queries.failedLoginAttempts',
      code: `{app="smooth-operator"} | json | action = \`user.login_failed\``,
    },
    {
      label: 'pages.settingsIntegrations.loki.queries.specificUserActivity',
      code: `{app="smooth-operator"} | json | action != "" | userId = "<user-id>"`,
    },
    {
      label: 'pages.settingsIntegrations.loki.queries.failureEventsOnly',
      code: `{app="smooth-operator"} | json | outcome = \`failure\``,
    },
  ];

  readonly lokiLabels: string[] = [
    'app="smooth-operator"',
    'level="Information|Warning|Error"',
    'EnvironmentName="Development|Production"',
  ];

  readonly grafanaDashboards: GrafanaDashboard[] = [
    {
      name: 'pages.settingsIntegrations.grafana.dashboards.activeSessions.name',
      description: 'pages.settingsIntegrations.grafana.dashboards.activeSessions.description',
      filename: 'active-sessions.json',
      url: '/grafana-dashboards/active-sessions.json',
    },
    {
      name: 'pages.settingsIntegrations.grafana.dashboards.aspnetOverview.name',
      description: 'pages.settingsIntegrations.grafana.dashboards.aspnetOverview.description',
      filename: 'aspnetcore-overview.json',
      url: '/grafana-dashboards/aspnetcore-overview.json',
    },
    {
      name: 'pages.settingsIntegrations.grafana.dashboards.auditEvents.name',
      description: 'pages.settingsIntegrations.grafana.dashboards.auditEvents.description',
      filename: 'audit-events.json',
      url: '/grafana-dashboards/audit-events.json',
    },
    {
      name: 'pages.settingsIntegrations.grafana.dashboards.authSecurity.name',
      description: 'pages.settingsIntegrations.grafana.dashboards.authSecurity.description',
      filename: 'auth-security.json',
      url: '/grafana-dashboards/auth-security.json',
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
}
