import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { AdminDashboardSummary, MemberDashboardSummary } from '../../core/models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/dashboard`;

  getAdminSummary(): Observable<AdminDashboardSummary> {
    return this.http.get<ApiResponse<AdminDashboardSummary>>(`${this.baseUrl}/admin-summary`)
      .pipe(map((res) => res.data!));
  }

  getMemberSummary(): Observable<MemberDashboardSummary> {
    return this.http.get<ApiResponse<MemberDashboardSummary>>(`${this.baseUrl}/member-summary`)
      .pipe(map((res) => res.data!));
  }
}
