import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { PermissionItem, RoleDetail, RoleListItem } from '../../core/models/user.model';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly http = inject(HttpClient);
  private readonly rolesUrl = `${environment.apiUrl}/roles`;
  private readonly permissionsUrl = `${environment.apiUrl}/permissions`;

  getRoles(): Observable<RoleListItem[]> {
    return this.http.get<ApiResponse<RoleListItem[]>>(this.rolesUrl).pipe(map((r) => r.data!));
  }

  getRole(id: number): Observable<RoleDetail> {
    return this.http.get<ApiResponse<RoleDetail>>(`${this.rolesUrl}/${id}`).pipe(map((r) => r.data!));
  }

  getAllPermissions(): Observable<Record<string, PermissionItem[]>> {
    return this.http.get<ApiResponse<Record<string, PermissionItem[]>>>(this.permissionsUrl)
      .pipe(map((r) => r.data!));
  }

  createRole(payload: { name: string; description?: string; permissionIds: number[] }): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.rolesUrl, payload).pipe(map((r) => r.data!));
  }

  updateRole(id: number, payload: { name: string; description?: string; permissionIds: number[] }): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.rolesUrl}/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  deleteRole(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.rolesUrl}/${id}`).pipe(map(() => void 0));
  }
}
