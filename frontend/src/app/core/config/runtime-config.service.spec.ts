import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { RuntimeConfigService } from './runtime-config.service';

describe('RuntimeConfigService', () => {
  let svc: RuntimeConfigService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(RuntimeConfigService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns defaults before load()', () => {
    expect(svc.config.helpUrl).toBe('http://localhost:3000');
    expect(svc.config.featureFlags).toEqual({});
  });

  it('merges fetched config over defaults', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      ok: true,
      json: () =>
        Promise.resolve({
          helpUrl: 'https://help.example.com',
          featureFlags: { sso: true },
        }),
    } as Response);
    await svc.load();
    expect(svc.config.helpUrl).toBe('https://help.example.com');
    expect(svc.config.docsUrl).toBe('http://localhost:3000');
    expect(svc.config.featureFlags).toEqual({ sso: true });
  });

  it('falls back to defaults on non-OK response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({}),
    } as Response);
    await svc.load();
    expect(svc.config.helpUrl).toBe('http://localhost:3000');
  });

  it('falls back to defaults on network error', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new Error('offline'));
    await svc.load();
    expect(svc.config.helpUrl).toBe('http://localhost:3000');
  });
});
