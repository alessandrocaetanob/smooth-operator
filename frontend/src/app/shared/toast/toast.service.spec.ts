import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { ToastService, Toast, ToastKind } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [ToastService] });
    service = TestBed.inject(ToastService);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create with empty toasts', () => {
    expect(service).toBeTruthy();
    expect(service.toasts()).toEqual([]);
  });

  describe('success()', () => {
    it('should push a success toast with 3500ms TTL', () => {
      service.success('Saved!');
      const toasts = service.toasts();
      expect(toasts.length).toBe(1);
      expect(toasts[0].kind).toBe('success');
      expect(toasts[0].message).toBe('Saved!');
      expect(toasts[0].ttl).toBe(3500);
    });

    it('should auto-dismiss after 3500ms', () => {
      service.success('Done');
      expect(service.toasts().length).toBe(1);
      vi.advanceTimersByTime(3500);
      expect(service.toasts().length).toBe(0);
    });
  });

  describe('error()', () => {
    it('should push an error toast with 6000ms TTL', () => {
      service.error('Something went wrong');
      const toasts = service.toasts();
      expect(toasts.length).toBe(1);
      expect(toasts[0].kind).toBe('error');
      expect(toasts[0].ttl).toBe(6000);
    });

    it('should auto-dismiss after 6000ms', () => {
      service.error('Failure');
      vi.advanceTimersByTime(5999);
      expect(service.toasts().length).toBe(1);
      vi.advanceTimersByTime(1);
      expect(service.toasts().length).toBe(0);
    });
  });

  describe('info()', () => {
    it('should push an info toast with 3500ms TTL', () => {
      service.info('FYI');
      const t = service.toasts()[0];
      expect(t.kind).toBe('info');
      expect(t.ttl).toBe(3500);
    });

    it('should auto-dismiss after 3500ms', () => {
      service.info('Note');
      vi.advanceTimersByTime(3500);
      expect(service.toasts().length).toBe(0);
    });
  });

  describe('warning()', () => {
    it('should push a warning toast with 5000ms TTL', () => {
      service.warning('Watch out!');
      const t = service.toasts()[0];
      expect(t.kind).toBe('warning');
      expect(t.message).toBe('Watch out!');
      expect(t.ttl).toBe(5000);
    });

    it('should auto-dismiss after 5000ms', () => {
      service.warning('Be careful');
      vi.advanceTimersByTime(5000);
      expect(service.toasts().length).toBe(0);
    });
  });

  describe('dismiss()', () => {
    it('should remove toast by id', () => {
      service.success('Toast 1');
      service.error('Toast 2');
      expect(service.toasts().length).toBe(2);

      const id = service.toasts()[0].id;
      service.dismiss(id);
      expect(service.toasts().length).toBe(1);
      expect(service.toasts()[0].kind).toBe('error');
    });

    it('should be a no-op for unknown id', () => {
      service.success('Only one');
      service.dismiss(999);
      expect(service.toasts().length).toBe(1);
    });

    it('should prevent auto-dismiss from re-removing already-dismissed toast', () => {
      service.success('Temp');
      const id = service.toasts()[0].id;
      service.dismiss(id);
      expect(service.toasts().length).toBe(0);
      // Timer fires but toast is already gone — no error
      vi.advanceTimersByTime(3500);
      expect(service.toasts().length).toBe(0);
    });
  });

  describe('multiple toasts', () => {
    it('should accumulate multiple toasts', () => {
      service.success('A');
      service.info('B');
      service.warning('C');
      expect(service.toasts().length).toBe(3);
    });

    it('should assign unique incrementing ids', () => {
      service.success('X');
      service.error('Y');
      const ids = service.toasts().map((t: Toast) => t.id);
      expect(ids[0]).not.toBe(ids[1]);
      expect(ids[1]).toBeGreaterThan(ids[0]);
    });

    it('should each auto-dismiss at their own TTL', () => {
      service.success('Fast'); // 3500
      service.error('Slow'); // 6000
      vi.advanceTimersByTime(3500);
      expect(service.toasts().length).toBe(1);
      expect(service.toasts()[0].kind).toBe('error');
      vi.advanceTimersByTime(2500);
      expect(service.toasts().length).toBe(0);
    });
  });
});
