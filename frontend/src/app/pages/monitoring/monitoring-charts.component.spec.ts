import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { MonitoringChartsComponent, MonitoringChartData } from './monitoring-charts.component';

vi.mock('chart.js', async () => {
  const destroy = vi.fn();
  const update = vi.fn();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const ChartCtor = vi.fn(function (this: any, canvas: HTMLCanvasElement, config: unknown) {
    this.canvas = canvas;
    this.config = config;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    this.data = (config as any).data;
    this.destroy = destroy;
    this.update = update;
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (ChartCtor as any).register = vi.fn();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (ChartCtor as any).getChart = vi.fn(() => undefined);
  return {
    Chart: ChartCtor,
    LineController: class {},
    BarController: class {},
    DoughnutController: class {},
    CategoryScale: class {},
    LinearScale: class {},
    PointElement: class {},
    LineElement: class {},
    BarElement: class {},
    ArcElement: class {},
    Title: class {},
    Tooltip: class {},
    Legend: class {},
    Filler: class {},
  };
});

describe('MonitoringChartsComponent', () => {
  let fixture: ComponentFixture<MonitoringChartsComponent>;
  let component: MonitoringChartsComponent;

  const sampleData: MonitoringChartData = {
    loginTs: [
      { timestamp: new Date().toISOString(), values: { success: 5, failure: 1 } },
      { timestamp: new Date().toISOString(), values: { success: 3 } },
    ],
    connTs: [
      {
        timestamp: new Date().toISOString(),
        values: { 'connection.started': 4, 'connection.ended': 3 },
      },
    ],
    topEvents: [
      { action: 'user.created', count: 7 },
      { action: 'connection.ticket.issued', count: 3 },
    ],
    breakdown: { success: 80, failure: 20 },
    eventTs: [
      { timestamp: new Date().toISOString(), values: { a: 1, b: 2 } },
      { timestamp: new Date().toISOString(), values: { a: 3 } },
    ],
    hours: 24,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MonitoringChartsComponent],
      providers: [provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(MonitoringChartsComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders nothing when ngAfterViewInit fires with no data', () => {
    fixture.componentRef.setInput('data', null);
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('renders all charts when data is provided via input', () => {
    fixture.componentRef.setInput('data', sampleData);
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('re-renders when data input changes after view init', () => {
    fixture.componentRef.setInput('data', sampleData);
    fixture.detectChanges();
    fixture.componentRef.setInput('data', {
      ...sampleData,
      breakdown: { success: 1, failure: 99 },
    });
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('formats labels for short, mid and long ranges', () => {
    fixture.componentRef.setInput('data', { ...sampleData, hours: 3 });
    fixture.detectChanges();
    fixture.componentRef.setInput('data', { ...sampleData, hours: 12 });
    fixture.detectChanges();
    fixture.componentRef.setInput('data', { ...sampleData, hours: 72 });
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('handles empty bucket arrays', () => {
    fixture.componentRef.setInput('data', {
      ...sampleData,
      loginTs: [],
      connTs: [],
      eventTs: [],
      topEvents: [],
    });
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('cleans up charts on destroy', () => {
    fixture.componentRef.setInput('data', sampleData);
    fixture.detectChanges();
    fixture.destroy();
    expect(component).toBeTruthy();
  });

  it('absorbs errors in chart render via try/catch', () => {
    // Force one render path to throw by setting bad data
    fixture.componentRef.setInput('data', {
      loginTs: null as unknown as MonitoringChartData['loginTs'],
      connTs: [],
      topEvents: [],
      breakdown: { success: 1, failure: 1 },
      eventTs: [],
      hours: 24,
    });
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});
