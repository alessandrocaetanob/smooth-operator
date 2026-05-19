import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';

import { ConfirmDialog } from './confirm-dialog';
import { ConfirmDialogService } from './confirm-dialog.service';

describe('ConfirmDialog', () => {
  let component: ConfirmDialog;
  let fixture: ComponentFixture<ConfirmDialog>;
  let svc: ConfirmDialogService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmDialog],
      providers: [provideAnimations()],
    }).compileComponents();

    svc = TestBed.inject(ConfirmDialogService);
    fixture = TestBed.createComponent(ConfirmDialog);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('default tone, labels when no active request', () => {
    expect(component.tone()).toBe('default');
    expect(component.confirmLabel()).toBe('Confirm');
    expect(component.cancelLabel()).toBe('Cancel');
  });

  it('reads tone and labels from active request', () => {
    void svc.ask({
      title: 't',
      message: 'm',
      tone: 'danger',
      confirmLabel: 'Yes',
      cancelLabel: 'No',
    });
    expect(component.tone()).toBe('danger');
    expect(component.confirmLabel()).toBe('Yes');
    expect(component.cancelLabel()).toBe('No');
  });

  it('confirm resolves true', async () => {
    const promise = svc.ask({ title: 't', message: 'm' });
    component.confirm();
    expect(await promise).toBe(true);
  });

  it('cancel resolves false', async () => {
    const promise = svc.ask({ title: 't', message: 'm' });
    component.cancel();
    expect(await promise).toBe(false);
  });

  it('onEscape resolves false when active', async () => {
    const promise = svc.ask({ title: 't', message: 'm' });
    component.onEscape();
    expect(await promise).toBe(false);
  });

  it('onEscape is no-op when no active request', () => {
    expect(() => component.onEscape()).not.toThrow();
  });

  it('handleBackdropClick cancels when target == currentTarget', async () => {
    const promise = svc.ask({ title: 't', message: 'm' });
    const el = document.createElement('div');
    const event = { target: el, currentTarget: el } as unknown as MouseEvent;
    component.handleBackdropClick(event);
    expect(await promise).toBe(false);
  });

  it('handleBackdropClick does not cancel when click bubbles from a child', () => {
    void svc.ask({ title: 't', message: 'm' });
    const parent = document.createElement('div');
    const child = document.createElement('span');
    const event = { target: child, currentTarget: parent } as unknown as MouseEvent;
    component.handleBackdropClick(event);
    expect(component.active()).not.toBeNull();
  });

  it('danger tone triggers countdown lock for 3s', () => {
    vi.useFakeTimers();
    fixture.detectChanges();
    void svc.ask({ title: 't', message: 'm', tone: 'danger' });
    fixture.detectChanges();
    expect(component.isCountingDown()).toBe(true);
    vi.advanceTimersByTime(3100);
    expect(component.isCountingDown()).toBe(false);
  });

  it('default tone does not engage countdown', () => {
    fixture.detectChanges();
    void svc.ask({ title: 't', message: 'm' });
    fixture.detectChanges();
    expect(component.isCountingDown()).toBe(false);
  });

  it('cancel clears countdown', () => {
    vi.useFakeTimers();
    fixture.detectChanges();
    void svc.ask({ title: 't', message: 'm', tone: 'danger' });
    fixture.detectChanges();
    component.cancel();
    expect(component.isCountingDown()).toBe(false);
  });

  it('ngOnDestroy clears any pending countdown timer', () => {
    expect(() => component.ngOnDestroy()).not.toThrow();
  });
});
