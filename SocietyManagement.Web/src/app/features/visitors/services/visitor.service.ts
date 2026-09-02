import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { Gate, VisitorDto, VisitorPurpose, VisitorSettingsDto, VisitorVisitDto } from '../models/visitor.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the Visitor & Gate Management module (Phase 1: Gates,
 * Purposes, Visitors, VisitorVisits) — mirrors EventService's shape. */
@Injectable({ providedIn: 'root' })
export class VisitorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Gates -----------------------------------------------------------------
  getGates(societyId: number, isActive?: boolean): Observable<Gate[]> {
    return this.http.get<ApiResponse<Gate[]>>(`${this.baseUrl}/gates`, { params: toHttpParams({ societyId, isActive }) })
      .pipe(map((r) => r.data!));
  }
  createGate(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/gates`, payload).pipe(map((r) => r.data!));
  }
  updateGate(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/gates/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteGate(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/gates/${id}`).pipe(map(() => void 0));
  }

  // ---- Purposes --------------------------------------------------------------
  getPurposes(societyId: number, isActive?: boolean): Observable<VisitorPurpose[]> {
    return this.http.get<ApiResponse<VisitorPurpose[]>>(`${this.baseUrl}/visitor-purposes`, { params: toHttpParams({ societyId, isActive }) })
      .pipe(map((r) => r.data!));
  }
  createPurpose(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/visitor-purposes`, payload).pipe(map((r) => r.data!));
  }
  updatePurpose(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/visitor-purposes/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deletePurpose(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/visitor-purposes/${id}`).pipe(map(() => void 0));
  }

  // ---- Visitors (the reusable person record) ----------------------------------
  getVisitors(societyId: number, search?: string): Observable<PaginatedResult<VisitorDto>> {
    return this.http.get<ApiResponse<PaginatedResult<VisitorDto>>>(`${this.baseUrl}/visitors`, { params: toHttpParams({ societyId, search, pageSize: 20 }) })
      .pipe(map((r) => r.data!));
  }

  // ---- Visitor Visits ----------------------------------------------------------
  getVisits(params: {
    societyId: number; status?: number; gateId?: number; flatId?: number;
    fromDate?: string; toDate?: string; search?: string; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<VisitorVisitDto>> {
    return this.http.get<ApiResponse<PaginatedResult<VisitorVisitDto>>>(`${this.baseUrl}/visitor-visits`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getPendingApprovals(): Observable<VisitorVisitDto[]> {
    return this.http.get<ApiResponse<VisitorVisitDto[]>>(`${this.baseUrl}/visitor-visits/pending`)
      .pipe(map((r) => r.data!));
  }
  getMyVisits(params: {
    fromDate?: string; toDate?: string; search?: string; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<VisitorVisitDto>> {
    return this.http.get<ApiResponse<PaginatedResult<VisitorVisitDto>>>(`${this.baseUrl}/visitor-visits/mine`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getCurrentlyInside(societyId: number): Observable<VisitorVisitDto[]> {
    return this.http.get<ApiResponse<VisitorVisitDto[]>>(`${this.baseUrl}/visitor-visits/currently-inside`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  createVisit(payload: Record<string, unknown>): Observable<VisitorVisitDto> {
    return this.http.post<ApiResponse<VisitorVisitDto>>(`${this.baseUrl}/visitor-visits`, payload)
      .pipe(map((r) => r.data!));
  }
  approveVisit(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/visitor-visits/${id}/approve`, {}).pipe(map(() => void 0));
  }
  rejectVisit(id: number, reason?: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/visitor-visits/${id}/reject`, { reason }).pipe(map(() => void 0));
  }
  checkInVisit(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/visitor-visits/${id}/check-in`, {}).pipe(map(() => void 0));
  }
  checkOutVisit(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/visitor-visits/${id}/check-out`, {}).pipe(map(() => void 0));
  }
  cancelVisit(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/visitor-visits/${id}/cancel`, {}).pipe(map(() => void 0));
  }

  // ---- Settings ------------------------------------------------------------------
  getSettings(societyId: number): Observable<VisitorSettingsDto> {
    return this.http.get<ApiResponse<VisitorSettingsDto>>(`${this.baseUrl}/visitor-settings`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  saveSettings(payload: { societyId: number; approvalRequestExpiryMinutes: number; retentionDays: number }): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/visitor-settings`, payload).pipe(map(() => void 0));
  }
}
