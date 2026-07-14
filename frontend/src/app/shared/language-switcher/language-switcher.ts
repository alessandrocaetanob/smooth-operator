import { ChangeDetectionStrategy, Component, Input, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AppLanguage, LanguageService } from '../../services/language.service';

export type LanguageSwitcherVariant = 'nav' | 'floating';

const LANGUAGE_LABELS: Record<AppLanguage, { short: string; nextName: string }> = {
  'pt-BR': { short: 'PT', nextName: 'English' },
  en: { short: 'EN', nextName: 'Português' },
};

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './language-switcher.html',
  styleUrl: './language-switcher.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitcher {
  private readonly languageService = inject(LanguageService);

  /**
   * `nav` — flat text button, intended for inline use in the top navigation.
   * `floating` — fixed top-right glass disc, intended for layout-less pages
   *  (login, first-access), paired with the floating theme toggle.
   */
  @Input() variant: LanguageSwitcherVariant = 'nav';

  /** Compact size — matches `tool-btn-sm` style for use in the session header. */
  @Input() compact = false;

  readonly language = this.languageService.language;
  readonly currentLabel = computed(() => LANGUAGE_LABELS[this.language()].short);
  readonly nextLanguageName = computed(() => LANGUAGE_LABELS[this.language()].nextName);

  toggle(): void {
    const next: AppLanguage = this.language() === 'pt-BR' ? 'en' : 'pt-BR';
    this.languageService.use(next);
  }
}
