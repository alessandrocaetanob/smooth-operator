import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideAppInitializer, inject } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { catchError, firstValueFrom, of } from 'rxjs';

import { routes } from './app.routes';
import { AuthService } from './services/auth.service';
import { authInterceptor } from './services/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
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
