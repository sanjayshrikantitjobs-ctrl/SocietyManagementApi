import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { ComplaintDto, ComplaintKpisDto } from '../models/complaint.model';

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
export class ComplaintService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getComplaints(params: { societyId: number; category?: number; priority?: number; search?: string }): Observable<ComplaintDto[]> {
    return this.http.get<ApiResponse<ComplaintDto[]>>(`${this.baseUrl}/complaints`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getComplaintsPaged(params: {
    societyId: number; category?: number; priority?: number; search?: string;
    sortBy?: string; sortDescending?: boolean; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<ComplaintDto>> {
    return this.http.get<ApiResponse<PaginatedResult<ComplaintDto>>>(`${this.baseUrl}/complaints/paged`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getKpis(societyId: number): Observable<ComplaintKpisDto> {
    return this.http.get<ApiResponse<ComplaintKpisDto>>(`${this.baseUrl}/complaints/kpis`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  getMine(): Observable<ComplaintDto[]> {
    return this.http.get<ApiResponse<ComplaintDto[]>>(`${this.baseUrl}/complaints/mine`).pipe(map((r) => r.data!));
  }
  getById(id: number): Observable<ComplaintDto> {
    return this.http.get<ApiResponse<ComplaintDto>>(`${this.baseUrl}/complaints/${id}`).pipe(map((r) => r.data!));
  }
  create(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/complaints`, payload).pipe(map((r) => r.data!));
  }
  update(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}`).pipe(map(() => void 0));
  }
  assign(id: number, staffId: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}/assign`, { staffId }).pipe(map(() => void 0));
  }
  start(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}/start`, {}).pipe(map(() => void 0));
  }
  resolve(id: number, resolutionNotes: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}/resolve`, { resolutionNotes }).pipe(map(() => void 0));
  }
  close(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}/close`, {}).pipe(map(() => void 0));
  }
  reopen(id: number, reason: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/complaints/${id}/reopen`, { reason }).pipe(map(() => void 0));
  }
}
