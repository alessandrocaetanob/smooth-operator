import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { catchError, firstValueFrom, of } from 'rxjs';

import { routes } from './app.routes';
import { AuthService } from './services/auth.service';
import { authInterceptor } from './services/auth.interceptor';
import { ThemeService } from './services/theme.service';
import { MotionService } from './services/motion.service';
import { RuntimeConfigService } from './core/config/runtime-config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    // Runtime config — must run first so subsequent initializers can read it.
    provideAppInitializer(async () => {
      await inject(RuntimeConfigService).load();
    }),
    // Theme + motion — synchronous, run before route activation.
    provideAppInitializer(() => {
      inject(ThemeService).init();
      inject(MotionService).init();
    }),
    // Load setup-status + user profile (when authenticated) before route activation.
    provideAppInitializer(async () => {
      const auth = inject(AuthService);
      await firstValueFrom(auth.loadSetupStatus());
      if (!auth.token()) return;

      await firstValueFrom(
        auth.me().pipe(
          catchError(() => {
            auth.logout();
            return of(null);
          }),
        ),
      );
    }),
  ],
};
