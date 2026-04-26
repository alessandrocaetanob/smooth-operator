import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface UserGroupMember {
  id: string;
  name: string;
  email: string;
  isActive?: boolean;
}

export interface UserGroup {
  id: string;
  name: string;
  description?: string | null;
  ownerUserId?: string | null;
  ownerName?: string | null;
  createdAt: string;
  memberCount: number;
  members: UserGroupMember[];
}

export interface UpdateGroupPayload {
  name: string;
  description?: string | null;
  ownerUserId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly http = inject(HttpClient);
  readonly list = signal<UserGroup[]>([]);

  reload(): Observable<UserGroup[]> {
    return this.http.get<UserGroup[]>('/api/groups').pipe(
      tap((groups) => this.list.set(groups)),
    );
  }

  create(payload: { name: string; description?: string | null; ownerUserId?: string | null }): Observable<UserGroup> {
    return this.http.post<UserGroup>('/api/groups', payload).pipe(
      tap(() => this.reload().subscribe()),
    );
  }

  rename(id: string, name: string): Observable<UserGroup> {
    return this.http.put<UserGroup>(`/api/groups/${id}`, { name }).pipe(
      tap(() => this.reload().subscribe()),
    );
  }

  update(id: string, payload: UpdateGroupPayload): Observable<UserGroup> {
    return this.http.put<UserGroup>(`/api/groups/${id}`, payload).pipe(
      tap(() => this.reload().subscribe()),
    );
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/groups/${id}`).pipe(
      tap(() => this.reload().subscribe()),
    );
  }

  setMembers(groupId: string, userIds: string[]): Observable<void> {
    return this.http.put<void>(`/api/groups/${groupId}/members`, { userIds });
  }
}
