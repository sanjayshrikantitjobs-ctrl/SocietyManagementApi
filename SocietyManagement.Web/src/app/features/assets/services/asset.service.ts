import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { AssetBookingDto, AssetBookingStatus, AssetDto } from '../models/asset.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    httpParams = httpParams.set(key, String(value));
  });
  return httpParams;
}

@Injectable({ providedIn: 'root' })
export class AssetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Assets -----------------------------------------------------------
  getAssets(societyId: number, activeOnly = false): Observable<AssetDto[]> {
    return this.http.get<ApiResponse<AssetDto[]>>(`${this.baseUrl}/assets`, { params: { societyId, activeOnly } })
      .pipe(map((r) => r.data!));
  }
  getAsset(id: number): Observable<AssetDto> {
    return this.http.get<ApiResponse<AssetDto>>(`${this.baseUrl}/assets/${id}`).pipe(map((r) => r.data!));
  }
  getAvailability(assetId: number, startDate: string, endDate: string): Observable<number> {
    return this.http.get<ApiResponse<number>>(`${this.baseUrl}/assets/${assetId}/availability`, { params: { startDate, endDate } })
      .pipe(map((r) => r.data!));
  }
  createAsset(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/assets`, payload).pipe(map((r) => r.data!));
  }
  updateAsset(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/assets/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteAsset(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/assets/${id}`).pipe(map(() => void 0));
  }

  // ---- Bookings ---------------------------------------------------------------
  getBookings(params: { societyId: number; status?: AssetBookingStatus; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<AssetBookingDto>> {
    return this.http.get<ApiResponse<PaginatedResult<AssetBookingDto>>>(`${this.baseUrl}/asset-bookings`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getMyBookings(params: { status?: AssetBookingStatus; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<AssetBookingDto>> {
    return this.http.get<ApiResponse<PaginatedResult<AssetBookingDto>>>(`${this.baseUrl}/asset-bookings/mine`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getBookingById(id: number): Observable<AssetBookingDto> {
    return this.http.get<ApiResponse<AssetBookingDto>>(`${this.baseUrl}/asset-bookings/${id}`).pipe(map((r) => r.data!));
  }
  createBooking(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/asset-bookings`, payload).pipe(map((r) => r.data!));
  }
  updateBookingStatus(id: number, status: AssetBookingStatus, rejectionReason?: string | null): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/asset-bookings/${id}/status`, { status, rejectionReason }).pipe(map(() => void 0));
  }
  cancelBooking(id: number, reason?: string | null): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/asset-bookings/${id}/cancel`, { reason }).pipe(map(() => void 0));
  }
  recordIssue(itemId: number, quantityIssued: number): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/asset-bookings/items/${itemId}/issue`, { quantityIssued }).pipe(map(() => void 0));
  }
  recordReturn(itemId: number, quantityReturned: number, quantityDamaged: number, quantityLost: number): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/asset-bookings/items/${itemId}/return`,
      { quantityReturned, quantityDamaged, quantityLost }).pipe(map(() => void 0));
  }
  recordDepositRefund(id: number, depositRefundAmount: number): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/asset-bookings/${id}/deposit-refund`, { depositRefundAmount }).pipe(map(() => void 0));
  }
}
