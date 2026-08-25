import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import {
  AdminDashboardSummary, MemberDashboardSummary, MonthlyCollectionPoint, RecentActivityItem, UpcomingItems
} from '../../core/models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/dashboard`;

  getAdminSummary(societyId: number): Observable<AdminDashboardSummary> {
    return this.http.get<ApiResponse<AdminDashboardSummary>>(`${this.baseUrl}/admin-summary`, { params: { societyId } })
      .pipe(map((res) => res.data!));
  }

  getMonthlyCollectionTrend(societyId: number, months = 6): Observable<MonthlyCollectionPoint[]> {
    return this.http.get<ApiResponse<MonthlyCollectionPoint[]>>(`${this.baseUrl}/monthly-collection-trend`, { params: { societyId, months } })
      .pipe(map((res) => res.data!));
  }

  getUpcoming(societyId: number): Observable<UpcomingItems> {
    return this.http.get<ApiResponse<UpcomingItems>>(`${this.baseUrl}/upcoming`, { params: { societyId } })
      .pipe(map((res) => res.data!));
  }

  getRecentActivity(societyId: number, take = 10): Observable<RecentActivityItem[]> {
    return this.http.get<ApiResponse<RecentActivityItem[]>>(`${this.baseUrl}/recent-activity`, { params: { societyId, take } })
      .pipe(map((res) => res.data!));
  }

  getMemberSummary(): Observable<MemberDashboardSummary> {
    return this.http.get<ApiResponse<MemberDashboardSummary>>(`${this.baseUrl}/member-summary`)
      .pipe(map((res) => res.data!));
  }
}
