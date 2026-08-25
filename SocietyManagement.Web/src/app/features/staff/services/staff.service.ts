import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { StaffDto } from '../models/staff.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

@Injectable({ providedIn: 'root' })
export class StaffService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getStaff(params: {
    societyId: number; search?: string; category?: number; isActive?: boolean;
    sortBy?: string; sortDescending?: boolean; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<StaffDto>> {
    return this.http.get<ApiResponse<PaginatedResult<StaffDto>>>(`${this.baseUrl}/staff`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getStaffById(id: number): Observable<StaffDto> {
    return this.http.get<ApiResponse<StaffDto>>(`${this.baseUrl}/staff/${id}`).pipe(map((r) => r.data!));
  }
  createStaff(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/staff`, payload).pipe(map((r) => r.data!));
  }
  updateStaff(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/staff/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteStaff(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/staff/${id}`).pipe(map(() => void 0));
  }
}
