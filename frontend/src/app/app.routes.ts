import { Routes } from '@angular/router';
import { Authentication } from './pages/authentication/authentication';
import { FirstAccess } from './pages/first-access/first-access';
import { Administration } from './pages/administration/administration';
import { SettingsVaults } from './pages/settings/vaults/vaults';
import { SettingsGroups } from './pages/settings/groups/groups';
import { SettingsRoles } from './pages/settings/roles/roles';
import { MyAccess } from './pages/my-access/my-access';
import { Vault } from './pages/vault/vault';
import { EmptyState } from './pages/empty-state/empty-state';
import { ConnectingState } from './pages/connecting-state/connecting-state';
import { ActiveSession } from './pages/active-session/active-session';
import { AuditLogs } from './pages/audit-logs/audit-logs';
import { SearchResults } from './pages/search-results/search-results';
import { Connections } from './pages/connections/connections';
import { Hosts } from './pages/hosts/hosts';
import { Credentials } from './pages/credentials/credentials';
import { Settings } from './pages/settings/settings';
import { Email } from './pages/settings/email/email';
import { Retention } from './pages/settings/retention/retention';
import { Invite } from './pages/invite/invite';
import { Profile } from './pages/profile/profile';
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
  { path: 'first-access', component: FirstAccess, canActivate: [setupGuard] },
  { path: 'invite/:token', component: Invite },
  { path: 'session', component: ActiveSession, canActivate: [authGuard] },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'vault', component: Vault },
      { path: 'my-access', component: MyAccess },
      { path: 'connections', component: Connections, canActivate: [connectionManagerGuard] },
      { path: 'hosts', component: Hosts, canActivate: [connectionManagerGuard] },
      { path: 'credentials', component: Credentials, canActivate: [connectionManagerGuard] },
      { path: 'empty', component: EmptyState },
      { path: 'search', component: SearchResults },
      { path: 'profile', component: Profile },
      { path: 'connecting', component: ConnectingState },
      {
        path: 'settings',
        component: Settings,
        canActivate: [ownerAdminGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'users' },
          { path: 'users', component: Administration },
          { path: 'groups', component: SettingsGroups },
          { path: 'vaults', component: SettingsVaults },
          { path: 'roles', component: SettingsRoles },
          { path: 'email', component: Email },
          { path: 'retention', component: Retention },
          { path: 'audit-logs', component: AuditLogs },
        ],
      },
      { path: 'administration', redirectTo: 'settings/users', pathMatch: 'full' },
      { path: 'audit-logs', redirectTo: 'settings/audit-logs', pathMatch: 'full' },
    ],
  },
];
