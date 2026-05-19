import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { ConfirmDialogService } from './confirm-dialog.service';

describe('ConfirmDialogService', () => {
  let svc: ConfirmDialogService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(ConfirmDialogService);
  });

  it('ask sets active and resolves with the chosen value', async () => {
    const p = svc.ask({ title: 't', message: 'm' });
    expect(svc.active()?.title).toBe('t');
    svc.resolve(true);
    expect(await p).toBe(true);
    expect(svc.active()).toBeNull();
  });

  it('resolve(false) closes without exception', async () => {
    const p = svc.ask({ title: 't', message: 'm' });
    svc.resolve(false);
    expect(await p).toBe(false);
  });

  it('a second ask resolves the first with false', async () => {
    const first = svc.ask({ title: 'a', message: 'a' });
    const second = svc.ask({ title: 'b', message: 'b' });
    expect(await first).toBe(false);
    svc.resolve(true);
    expect(await second).toBe(true);
  });

  it('resolve with no active request is a no-op', () => {
    expect(() => svc.resolve(true)).not.toThrow();
    expect(svc.active()).toBeNull();
  });
});
