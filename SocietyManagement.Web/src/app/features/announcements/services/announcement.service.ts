import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { AnnouncementDto, AnnouncementStatus, AnnouncementType } from '../models/announcement.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    httpParams = httpParams.set(key, String(value));
  });
  return httpParams;
}

@Injectable({ providedIn: 'root' })
export class AnnouncementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getAnnouncements(params: {
    societyId: number; status?: AnnouncementStatus; type?: AnnouncementType; search?: string;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<AnnouncementDto>> {
    return this.http.get<ApiResponse<PaginatedResult<AnnouncementDto>>>(`${this.baseUrl}/announcements`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  getPublished(params: { societyId: number; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<AnnouncementDto>> {
    return this.http.get<ApiResponse<PaginatedResult<AnnouncementDto>>>(`${this.baseUrl}/announcements/published`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  getUnreadCount(societyId: number): Observable<number> {
    return this.http.get<ApiResponse<number>>(`${this.baseUrl}/announcements/unread-count`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }

  getById(id: number): Observable<AnnouncementDto> {
    return this.http.get<ApiResponse<AnnouncementDto>>(`${this.baseUrl}/announcements/${id}`).pipe(map((r) => r.data!));
  }

  create(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/announcements`, payload).pipe(map((r) => r.data!));
  }

  update(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/announcements/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/announcements/${id}`).pipe(map(() => void 0));
  }

  publish(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/announcements/${id}/publish`, {}).pipe(map(() => void 0));
  }

  markRead(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/announcements/${id}/mark-read`, {}).pipe(map(() => void 0));
  }

  toggleSaved(id: number): Observable<boolean> {
    return this.http.post<ApiResponse<boolean>>(`${this.baseUrl}/announcements/${id}/toggle-saved`, {}).pipe(map((r) => r.data!));
  }
}
