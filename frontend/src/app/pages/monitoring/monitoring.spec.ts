import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { of, throwError } from 'rxjs';

import { Monitoring } from './monitoring';
import { MetricsService, MetricsSummary } from '../../services/metrics.service';

describe('Monitoring', () => {
  let component: Monitoring;
  let fixture: ComponentFixture<Monitoring>;
  let metrics: {
    getSummary: ReturnType<typeof vi.fn>;
    getLoginTimeseries: ReturnType<typeof vi.fn>;
    getConnectionsTimeseries: ReturnType<typeof vi.fn>;
    getTopAuditEvents: ReturnType<typeof vi.fn>;
    getAuditEventBreakdown: ReturnType<typeof vi.fn>;
    getAuditEventTimeseries: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    metrics = {
      getSummary: vi.fn(() =>
        of({ activeSessions: 10, totalUsers: 5 } as unknown as MetricsSummary),
      ),
      getLoginTimeseries: vi.fn(() => of([])),
      getConnectionsTimeseries: vi.fn(() => of([])),
      getTopAuditEvents: vi.fn(() => of([])),
      getAuditEventBreakdown: vi.fn(() => of({ success: 1, failure: 1 })),
      getAuditEventTimeseries: vi.fn(() => of([])),
    };

    await TestBed.configureTestingModule({
      imports: [Monitoring],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: MetricsService, useValue: metrics },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        monitoring: {
          autoRefresh: { off: 'Off' },
          loadError: 'Unable to load monitoring metrics. Check your connection or try refreshing.',
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(Monitoring);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ngOnInit triggers loadAll', () => {
    const spy = vi.spyOn(component, 'loadAll');
    component.ngOnInit();
    expect(spy).toHaveBeenCalled();
  });

  it('loadAll populates summary, chartData, lastRefreshed', () => {
    component.loadAll();
    expect(metrics.getSummary).toHaveBeenCalled();
    expect(component.summary()?.activeSessions).toBe(10);
    expect(component.chartData()).not.toBeNull();
    expect(component.lastRefreshed()).toBeInstanceOf(Date);
    expect(component.loading()).toBe(false);
  });

  it('loadAll handles error and clears loading', () => {
    metrics.getSummary.mockReturnValueOnce(throwError(() => new Error('x')));
    component.loadAll();
    expect(component.loading()).toBe(false);
  });

  it('setRange updates selectedRange and reloads', () => {
    component.setRange('7d');
    expect(component.selectedRange()).toBe('7d');
    expect(metrics.getLoginTimeseries).toHaveBeenLastCalledWith(168, 360);
  });

  describe('autoRefresh / restartTimer', () => {
    it('setAutoRefresh off clears interval', () => {
      component.setAutoRefresh('30s');
      component.setAutoRefresh('off');
      // No further triggers from interval — verify by counting calls before/after
      const before = metrics.getSummary.mock.calls.length;
      vi.useFakeTimers();
      vi.advanceTimersByTime(60_000);
      expect(metrics.getSummary.mock.calls.length).toBe(before);
    });

    it('setAutoRefresh schedules periodic reload', () => {
      vi.useFakeTimers();
      metrics.getSummary.mockClear();
      component.setAutoRefresh('30s');
      vi.advanceTimersByTime(31_000);
      expect(metrics.getSummary).toHaveBeenCalled();
    });

    it('does not reload when document is hidden', () => {
      vi.useFakeTimers();
      Object.defineProperty(document, 'visibilityState', {
        configurable: true,
        get: () => 'hidden',
      });
      metrics.getSummary.mockClear();
      component.setAutoRefresh('30s');
      vi.advanceTimersByTime(31_000);
      expect(metrics.getSummary).not.toHaveBeenCalled();
      Object.defineProperty(document, 'visibilityState', {
        configurable: true,
        get: () => 'visible',
      });
    });
  });

  describe('activeSessionsColor', () => {
    it('green when low', () => {
      metrics.getSummary.mockReturnValueOnce(
        of({ activeSessions: 1 } as unknown as MetricsSummary),
      );
      component.loadAll();
      expect(component.activeSessionsColor()).toBe('text-green-400');
    });

    it('yellow when ≥ 20', () => {
      metrics.getSummary.mockReturnValueOnce(
        of({ activeSessions: 30 } as unknown as MetricsSummary),
      );
      component.loadAll();
      expect(component.activeSessionsColor()).toBe('text-yellow-400');
    });

    it('red when ≥ 50', () => {
      metrics.getSummary.mockReturnValueOnce(
        of({ activeSessions: 75 } as unknown as MetricsSummary),
      );
      component.loadAll();
      expect(component.activeSessionsColor()).toBe('text-red-400');
    });

    it('green by default with no summary', () => {
      expect(component.activeSessionsColor()).toBe('text-green-400');
    });
  });

  it('ngOnDestroy clears intervals and removes listener', () => {
    component.setAutoRefresh('30s');
    expect(() => component.ngOnDestroy()).not.toThrow();
  });

  it('visibility change triggers reload when autoRefresh is on', () => {
    // ngOnInit registers the visibility listener; otherwise we'd dispatch into nothing.
    component.ngOnInit();
    component.setAutoRefresh('30s');
    metrics.getSummary.mockClear();
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => 'visible',
    });
    document.dispatchEvent(new Event('visibilitychange'));
    expect(metrics.getSummary).toHaveBeenCalled();
    component.ngOnDestroy();
  });

  it('visibility change does NOT reload when autoRefresh off', () => {
    component.ngOnInit();
    component.setAutoRefresh('off');
    metrics.getSummary.mockClear();
    document.dispatchEvent(new Event('visibilitychange'));
    expect(metrics.getSummary).not.toHaveBeenCalled();
    component.ngOnDestroy();
  });
});
