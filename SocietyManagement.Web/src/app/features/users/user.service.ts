import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';
import { UserListItem } from '../../core/models/user.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/users`;

  getUsers(params: {
    search?: string; roleId?: number; isActive?: boolean; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<UserListItem>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    });
    return this.http.get<ApiResponse<PaginatedResult<UserListItem>>>(this.baseUrl, { params: httpParams })
      .pipe(map((r) => r.data!));
  }

  createUser(payload: { firstName: string; lastName: string; email: string; mobileNumber: string; roleId: number }): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }

  updateUser(id: number, payload: { firstName: string; lastName: string; mobileNumber: string; roleId: number; isActive: boolean }): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => void 0));
  }

  toggleLock(id: number, locked: boolean): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/${id}/lock`, {}, { params: { locked } })
      .pipe(map(() => void 0));
  }

  resetPassword(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/${id}/reset-password`, {}).pipe(map(() => void 0));
  }
}
