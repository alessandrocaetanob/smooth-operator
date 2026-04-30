import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { ThemeService } from '../../services/theme.service';

export type ThemeToggleVariant = 'nav' | 'floating';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  templateUrl: './theme-toggle.html',
  styleUrl: './theme-toggle.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeToggle {
  readonly themeService = inject(ThemeService);

  /**
   * `nav` — flat icon button, intended for inline use in the top navigation.
   * `floating` — fixed top-right glass disc, intended for layout-less pages
   *  (login, first-access) so the user can switch theme without a chrome.
   */
  @Input() variant: ThemeToggleVariant = 'nav';

  /** Compact size — matches `tool-btn-sm` style for use in the session header. */
  @Input() compact = false;

  toggle(): void {
    this.themeService.toggle();
  }
}
