import { Routes } from '@angular/router';
import { Authentication } from './pages/authentication/authentication';
import { FirstAccess } from './pages/first-access/first-access';
import { Administration } from './pages/administration/administration';
import { Vault } from './pages/vault/vault';
import { EmptyState } from './pages/empty-state/empty-state';
import { ConnectingState } from './pages/connecting-state/connecting-state';
import { ActiveSession } from './pages/active-session/active-session';
import { AuditLogs } from './pages/audit-logs/audit-logs';
import { SearchResults } from './pages/search-results/search-results';
import { Layout } from './shared/layout/layout';
import { rootRedirectGuard, loginGuard, setupGuard, authGuard } from './services/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [rootRedirectGuard], children: [] },
  { path: 'login', component: Authentication, canActivate: [loginGuard] },
  { path: 'first-access', component: FirstAccess, canActivate: [setupGuard] },
  { path: 'session', component: ActiveSession, canActivate: [authGuard] },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'administration', component: Administration },
      { path: 'vault', component: Vault },
      { path: 'empty', component: EmptyState },
      { path: 'audit-logs', component: AuditLogs },
      { path: 'search', component: SearchResults },
      { path: 'connecting', component: ConnectingState },
    ],
  },
];
