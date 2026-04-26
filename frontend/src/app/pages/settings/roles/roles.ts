import { Component } from '@angular/core';

interface RoleDescriptor {
  key: string;
  name: string;
  tone: string;
  summary: string;
  permissions: string[];
}

@Component({
  selector: 'app-settings-roles',
  standalone: true,
  imports: [],
  templateUrl: './roles.html',
})
export class SettingsRoles {
  readonly roles: RoleDescriptor[] = [
    {
      key: 'Owner',
      name: 'Owner',
      tone: 'text-amber-300 border-amber-500/40 bg-amber-500/10',
      summary:
        'Root account, created on first setup. Cannot be demoted by anyone other than themselves.',
      permissions: [
        'Full access to every resource and setting',
        'Manage all users, groups, vaults, credentials, and connections',
        'View audit logs and email/system configuration',
        'Promote and demote Admins',
      ],
    },
    {
      key: 'Admin',
      name: 'Admin',
      tone: 'text-blue-300 border-blue-500/40 bg-blue-500/10',
      summary:
        'Workspace administrators. Manage users, groups, vaults, and credentials, and audit activity.',
      permissions: [
        'Invite users and assign roles (User, TeamAdmin, Admin)',
        'Create, rename and delete groups and vaults',
        'Manage all credentials and connections',
        'View audit logs and email settings',
      ],
    },
    {
      key: 'TeamAdmin',
      name: 'Team Admin',
      tone: 'text-violet-300 border-violet-500/40 bg-violet-500/10',
      summary:
        'Manage connections inside vaults they have access to. Cannot create vaults or invite users.',
      permissions: [
        'Create, edit and delete connections in assigned vaults',
        'Use credentials available to those vaults',
        'View members and groups assigned to those vaults',
        'No access to user, group or vault administration',
      ],
    },
    {
      key: 'User',
      name: 'User',
      tone: 'text-slate-300 border-white/10 bg-white/5',
      summary: 'Standard user. Can launch and use connections in vaults assigned to them.',
      permissions: [
        'Launch sessions on connections in assigned vaults',
        'View their own profile and effective access',
        'Cannot manage users, groups, vaults or credentials',
      ],
    },
  ];

  trackRole(_: number, r: RoleDescriptor): string {
    return r.key;
  }
}
