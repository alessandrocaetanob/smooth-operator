import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (req.url.startsWith('/api/')) {
    req = req.clone({ withCredentials: true });
  }
  return next(req).pipe(
    catchError((err: unknown) => {
      // Server rejected the credential. Clear the stale session and bounce to
      // login so the user isn't stuck on a half-broken page issuing more 401s.
      // Login/setup/logout endpoints are excluded so a wrong-password attempt
      // doesn't wipe an existing valid session and self-redirect, and logout
      // cannot trigger another logout.
      if (
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        req.url.startsWith('/api/') &&
        !req.url.startsWith('/api/auth/login') &&
        !req.url.startsWith('/api/auth/setup') &&
        !req.url.startsWith('/api/auth/logout') &&
        auth.isAuthenticated()
      ) {
        auth.logout();
        router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};
