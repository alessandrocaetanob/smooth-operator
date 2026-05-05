import { Routes } from '@angular/router';
import { Authentication } from './pages/authentication/authentication';
import { FirstAccess } from './pages/first-access/first-access';
import { ForgotPassword } from './pages/forgot-password/forgot-password';
import { Invite } from './pages/invite/invite';
import { Layout } from './shared/layout/layout';
import {
  rootRedirectGuard,
  loginGuard,
  setupGuard,
  authGuard,
  ownerAdminGuard,
  connectionManagerGuard,
} from './services/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [rootRedirectGuard], children: [] },
  { path: 'login', component: Authentication, canActivate: [loginGuard] },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'first-access', component: FirstAccess, canActivate: [setupGuard] },
  { path: 'invite/:token', component: Invite },
  {
    path: 'auth/sso/finalize',
    loadComponent: () => import('./pages/sso-finalize/sso-finalize').then((m) => m.SsoFinalize),
  },
  {
    path: 'session/:id',
    loadComponent: () =>
      import('./pages/active-session/active-session').then((m) => m.ActiveSession),
    canActivate: [authGuard],
  },
  {
    path: 'connecting/:id',
    loadComponent: () =>
      import('./pages/connecting-state/connecting-state').then((m) => m.ConnectingState),
    canActivate: [authGuard],
  },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'vault', loadComponent: () => import('./pages/vault/vault').then((m) => m.Vault) },
      {
        path: 'my-access',
        loadComponent: () => import('./pages/my-access/my-access').then((m) => m.MyAccess),
      },
      {
        path: 'connections',
        loadComponent: () => import('./pages/connections/connections').then((m) => m.Connections),
        canActivate: [connectionManagerGuard],
      },
      {
        path: 'hosts',
        loadComponent: () => import('./pages/hosts/hosts').then((m) => m.Hosts),
        canActivate: [connectionManagerGuard],
      },
      {
        path: 'credentials',
        loadComponent: () => import('./pages/credentials/credentials').then((m) => m.Credentials),
        canActivate: [connectionManagerGuard],
      },
      {
        path: 'empty',
        loadComponent: () => import('./pages/empty-state/empty-state').then((m) => m.EmptyState),
      },
      {
        path: 'search',
        loadComponent: () =>
          import('./pages/search-results/search-results').then((m) => m.SearchResults),
      },
      {
        path: 'profile',
        loadComponent: () => import('./pages/profile/profile').then((m) => m.Profile),
      },

      {
        path: 'auditing',
        loadComponent: () => import('./pages/auditing/logs/audit-logs').then((m) => m.AuditLogs),
        canActivate: [ownerAdminGuard],
      },
      { path: 'auditing/logs', redirectTo: 'auditing', pathMatch: 'full' },
      { path: 'auditing/dashboards', redirectTo: 'monitoring', pathMatch: 'full' },
      {
        path: 'monitoring',
        loadComponent: () => import('./pages/monitoring/monitoring').then((m) => m.Monitoring),
        canActivate: [ownerAdminGuard],
      },
      {
        path: 'settings',
        loadComponent: () => import('./pages/settings/settings').then((m) => m.Settings),
        canActivate: [ownerAdminGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'users' },
          {
            path: 'users',
            loadComponent: () =>
              import('./pages/administration/administration').then((m) => m.Administration),
          },
          {
            path: 'groups',
            loadComponent: () =>
              import('./pages/settings/groups/groups').then((m) => m.SettingsGroups),
          },
          {
            path: 'vaults',
            loadComponent: () =>
              import('./pages/settings/vaults/vaults').then((m) => m.SettingsVaults),
          },
          {
            path: 'roles',
            loadComponent: () =>
              import('./pages/settings/roles/roles').then((m) => m.SettingsRoles),
          },
          {
            path: 'email',
            loadComponent: () => import('./pages/settings/email/email').then((m) => m.Email),
          },
          {
            path: 'sso',
            loadComponent: () => import('./pages/settings/sso/sso').then((m) => m.SsoSettings),
          },
          {
            path: 'retention',
            loadComponent: () =>
              import('./pages/settings/retention/retention').then((m) => m.Retention),
          },

          {
            path: 'secret-providers',
            loadComponent: () =>
              import('./pages/settings/secret-providers/secret-providers').then(
                (m) => m.SecretProviders,
              ),
          },
        ],
      },
      { path: 'administration', redirectTo: 'settings/users', pathMatch: 'full' },
      { path: 'audit-logs', redirectTo: 'auditing/logs', pathMatch: 'full' },
    ],
  },
];
