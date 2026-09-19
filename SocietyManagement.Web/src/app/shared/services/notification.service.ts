import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';
import { NotificationDto } from '../models/notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getNotifications(params: { unreadOnly?: boolean; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<NotificationDto>> {
    let httpParams = new HttpParams();
    if (params.unreadOnly) httpParams = httpParams.set('unreadOnly', 'true');
    if (params.pageNumber) httpParams = httpParams.set('pageNumber', String(params.pageNumber));
    if (params.pageSize) httpParams = httpParams.set('pageSize', String(params.pageSize));

    return this.http.get<ApiResponse<PaginatedResult<NotificationDto>>>(`${this.baseUrl}/notifications`, { params: httpParams })
      .pipe(map((r) => r.data!));
  }

  getUnreadCount(): Observable<number> {
    return this.http.get<ApiResponse<number>>(`${this.baseUrl}/notifications/unread-count`).pipe(map((r) => r.data!));
  }

  markRead(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/notifications/${id}/mark-read`, {}).pipe(map(() => void 0));
  }

  markAllRead(): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/notifications/mark-all-read`, {}).pipe(map(() => void 0));
  }
}
