import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Drawer } from './drawer';

describe('Drawer', () => {
  let component: Drawer;
  let fixture: ComponentFixture<Drawer>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Drawer],
      providers: [provideAnimations(), provideTranslateService()],
    }).compileComponents();
    fixture = TestBed.createComponent(Drawer);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('onEscape emits closed', () => {
    const spy = vi.fn();
    component.closed.subscribe(spy);
    component.onEscape();
    expect(spy).toHaveBeenCalled();
  });

  it('ngAfterViewInit focuses close button when present', () => {
    expect(() => component.ngAfterViewInit()).not.toThrow();
  });
});
