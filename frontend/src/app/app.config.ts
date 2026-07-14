import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { catchError, firstValueFrom, of } from 'rxjs';

import { routes } from './app.routes';
import { AuthService } from './services/auth.service';
import { authInterceptor } from './services/auth.interceptor';
import { ThemeService } from './services/theme.service';
import { MotionService } from './services/motion.service';
import { LanguageService } from './services/language.service';
import { RuntimeConfigService } from './core/config/runtime-config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    provideTranslateService({
      fallbackLang: 'en',
      loader: provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }),
    }),
    // Runtime config — must run first so subsequent initializers can read it.
    provideAppInitializer(async () => {
      await inject(RuntimeConfigService).load();
    }),
    // Theme + motion + language — synchronous, run before route activation.
    provideAppInitializer(() => {
      inject(ThemeService).init();
      inject(MotionService).init();
      inject(LanguageService).init();
    }),
    // Load setup-status + user profile (when authenticated) before route activation.
    provideAppInitializer(async () => {
      const auth = inject(AuthService);
      await firstValueFrom(auth.loadSetupStatus());

      await firstValueFrom(auth.me().pipe(catchError(() => of(null))));
    }),
  ],
};
