import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type AppLanguage = 'pt-BR' | 'en';

export const SUPPORTED_LANGUAGES: AppLanguage[] = ['pt-BR', 'en'];
const STORAGE_KEY = 'language';
const DEFAULT_LANGUAGE: AppLanguage = 'pt-BR';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly language = signal<AppLanguage>(DEFAULT_LANGUAGE);

  /** Call once during app bootstrap. Reads localStorage; falls back to browser language. */
  init(): void {
    this.translate.addLangs(SUPPORTED_LANGUAGES);
    const saved = localStorage.getItem(STORAGE_KEY) as AppLanguage | null;
    const resolved = this.isSupported(saved) ? saved : this.detectBrowserLanguage();
    this.apply(resolved);
  }

  use(lang: AppLanguage): void {
    this.apply(lang);
  }

  private detectBrowserLanguage(): AppLanguage {
    const browserLang = navigator.language;
    if (browserLang?.toLowerCase().startsWith('pt')) {
      return 'pt-BR';
    }
    return this.isSupported(browserLang) ? browserLang : DEFAULT_LANGUAGE;
  }

  private isSupported(lang: string | null): lang is AppLanguage {
    return !!lang && SUPPORTED_LANGUAGES.includes(lang as AppLanguage);
  }

  private apply(lang: AppLanguage): void {
    this.language.set(lang);
    this.translate.use(lang);
    document.documentElement.lang = lang;
    localStorage.setItem(STORAGE_KEY, lang);
  }
}
