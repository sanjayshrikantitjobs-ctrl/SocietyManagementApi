import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { SupportTicketDto } from '../models/support.model';

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
export class SupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getMyTickets(): Observable<SupportTicketDto[]> {
    return this.http.get<ApiResponse<SupportTicketDto[]>>(`${this.baseUrl}/support-tickets/mine`)
      .pipe(map((r) => r.data!));
  }

  createTicket(subject: string, description: string): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/support-tickets`, { subject, description })
      .pipe(map((r) => r.data!));
  }

  // ---- Super Admin ---------------------------------------------------------------
  getAllTickets(params: {
    status?: number; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<SupportTicketDto>> {
    return this.http.get<ApiResponse<PaginatedResult<SupportTicketDto>>>(`${this.baseUrl}/support-tickets`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  updateTicketStatus(id: number, status: number, resolutionNotes?: string | null): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/support-tickets/${id}/status`, { id, status, resolutionNotes })
      .pipe(map(() => void 0));
  }
}
