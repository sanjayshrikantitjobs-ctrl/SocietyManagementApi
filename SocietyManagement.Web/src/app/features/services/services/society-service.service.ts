import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { ExpiringServiceDto, SocietyServiceDto } from '../models/society-service.model';

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
export class SocietyServiceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getServices(params: {
    societyId: number; search?: string; isActive?: boolean;
    sortBy?: string; sortDescending?: boolean; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<SocietyServiceDto>> {
    return this.http.get<ApiResponse<PaginatedResult<SocietyServiceDto>>>(`${this.baseUrl}/services`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getServiceById(id: number): Observable<SocietyServiceDto> {
    return this.http.get<ApiResponse<SocietyServiceDto>>(`${this.baseUrl}/services/${id}`).pipe(map((r) => r.data!));
  }
  getExpiring(societyId: number, withinDays = 10): Observable<ExpiringServiceDto[]> {
    return this.http.get<ApiResponse<ExpiringServiceDto[]>>(`${this.baseUrl}/services/expiring`, { params: { societyId, withinDays } })
      .pipe(map((r) => r.data!));
  }
  createService(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/services`, payload).pipe(map((r) => r.data!));
  }
  updateService(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/services/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteService(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/services/${id}`).pipe(map(() => void 0));
  }
}
