import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';

import { LanguageSwitcher } from './language-switcher';
import { LanguageService } from '../../services/language.service';

describe('LanguageSwitcher', () => {
  let component: LanguageSwitcher;
  let languageService: { use: ReturnType<typeof vi.fn>; language: ReturnType<typeof signal> };

  beforeEach(() => {
    languageService = { use: vi.fn(), language: signal('pt-BR') };
    TestBed.configureTestingModule({
      imports: [LanguageSwitcher],
      providers: [
        provideTranslateService(),
        { provide: LanguageService, useValue: languageService },
      ],
    });
    const fixture = TestBed.createComponent(LanguageSwitcher);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('toggle() switches from pt-BR to en', () => {
    component.toggle();
    expect(languageService.use).toHaveBeenCalledWith('en');
  });

  it('toggle() switches from en to pt-BR', () => {
    languageService.language.set('en');
    component.toggle();
    expect(languageService.use).toHaveBeenCalledWith('pt-BR');
  });

  it('default variant is nav and compact is false', () => {
    expect(component.variant).toBe('nav');
    expect(component.compact).toBe(false);
  });
});
