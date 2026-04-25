import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';

// Decides where to send the visitor when they hit the application root.
// The setup status was preloaded by APP_INITIALIZER so this is synchronous.
export const rootRedirectGuard: CanActivateFn = (): UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.requiresSetup()) return router.parseUrl('/first-access');
  if (auth.isAuthenticated()) return router.parseUrl('/administration');
  return router.parseUrl('/login');
};

// Bounces to /first-access if the app still needs bootstrap, otherwise allows
// the login screen to render.
export const loginGuard: CanActivateFn = (): boolean | UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.requiresSetup()) return router.parseUrl('/first-access');
  return true;
};

// Allows the first-access screen only while the application has no users.
export const setupGuard: CanActivateFn = (): boolean | UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.requiresSetup()) return router.parseUrl('/login');
  return true;
};

// Protects the authenticated areas of the app.
export const authGuard: CanActivateFn = (): boolean | UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.requiresSetup()) return router.parseUrl('/first-access');
  if (!auth.isAuthenticated()) return router.parseUrl('/login');
  return true;
};
