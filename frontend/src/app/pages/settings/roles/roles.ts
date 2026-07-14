import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface RoleDescriptor {
  key: string;
  nameKey: string;
  tone: string;
  summaryKey: string;
  permissionKeys: string[];
}

@Component({
  selector: 'app-settings-roles',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './roles.html',
})
export class SettingsRoles {
  readonly roles: RoleDescriptor[] = [
    {
      key: 'Owner',
      nameKey: 'pages.settingsRoles.owner.name',
      tone: 'text-amber-700 border-amber-500/50 bg-amber-500/15 dark:text-amber-300 dark:border-amber-500/40 dark:bg-amber-500/10',
      summaryKey: 'pages.settingsRoles.owner.summary',
      permissionKeys: [
        'pages.settingsRoles.owner.permissions.fullAccess',
        'pages.settingsRoles.owner.permissions.manageAll',
        'pages.settingsRoles.owner.permissions.viewAudit',
        'pages.settingsRoles.owner.permissions.promoteDemote',
      ],
    },
    {
      key: 'Admin',
      nameKey: 'pages.settingsRoles.admin.name',
      tone: 'text-blue-700 border-blue-500/50 bg-blue-500/15 dark:text-blue-300 dark:border-blue-500/40 dark:bg-blue-500/10',
      summaryKey: 'pages.settingsRoles.admin.summary',
      permissionKeys: [
        'pages.settingsRoles.admin.permissions.invite',
        'pages.settingsRoles.admin.permissions.manageGroups',
        'pages.settingsRoles.admin.permissions.manageCredentials',
        'pages.settingsRoles.admin.permissions.viewAudit',
      ],
    },
    {
      key: 'TeamAdmin',
      nameKey: 'pages.settingsRoles.teamAdmin.name',
      tone: 'text-violet-700 border-violet-500/50 bg-violet-500/15 dark:text-violet-300 dark:border-violet-500/40 dark:bg-violet-500/10',
      summaryKey: 'pages.settingsRoles.teamAdmin.summary',
      permissionKeys: [
        'pages.settingsRoles.teamAdmin.permissions.manageConnections',
        'pages.settingsRoles.teamAdmin.permissions.useCredentials',
        'pages.settingsRoles.teamAdmin.permissions.viewMembers',
        'pages.settingsRoles.teamAdmin.permissions.noAdminAccess',
      ],
    },
    {
      key: 'User',
      nameKey: 'pages.settingsRoles.user.name',
      tone: 'text-slate-700 border-slate-400/50 bg-slate-500/10 dark:text-slate-300 dark:border-white/10 dark:bg-white/5',
      summaryKey: 'pages.settingsRoles.user.summary',
      permissionKeys: [
        'pages.settingsRoles.user.permissions.launchSessions',
        'pages.settingsRoles.user.permissions.viewProfile',
        'pages.settingsRoles.user.permissions.noManagement',
      ],
    },
  ];

  trackRole(_: number, r: RoleDescriptor): string {
    return r.key;
  }
}
