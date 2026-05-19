import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { ThemeToggle } from './theme-toggle';
import { ThemeService } from '../../services/theme.service';

describe('ThemeToggle', () => {
  let component: ThemeToggle;
  let theme: { toggle: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    theme = { toggle: vi.fn() };
    TestBed.configureTestingModule({
      imports: [ThemeToggle],
      providers: [{ provide: ThemeService, useValue: theme }],
    });
    const fixture = TestBed.createComponent(ThemeToggle);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('toggle() delegates to ThemeService', () => {
    component.toggle();
    expect(theme.toggle).toHaveBeenCalled();
  });

  it('default variant is nav and compact is false', () => {
    expect(component.variant).toBe('nav');
    expect(component.compact).toBe(false);
  });
});
