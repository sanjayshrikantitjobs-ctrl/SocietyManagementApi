import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  FacilityBlackoutDateDto, FacilityBookingDto, FacilityBookingStatus, FacilityDto, FacilityPaymentStatus, FacilitySlotDto
} from '../models/facility.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    httpParams = httpParams.set(key, String(value));
  });
  return httpParams;
}

@Injectable({ providedIn: 'root' })
export class FacilityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Facilities -----------------------------------------------------------
  getFacilities(societyId: number, activeOnly = false): Observable<FacilityDto[]> {
    return this.http.get<ApiResponse<FacilityDto[]>>(`${this.baseUrl}/facilities`, { params: { societyId, activeOnly } })
      .pipe(map((r) => r.data!));
  }
  getFacility(id: number): Observable<FacilityDto> {
    return this.http.get<ApiResponse<FacilityDto>>(`${this.baseUrl}/facilities/${id}`).pipe(map((r) => r.data!));
  }
  getAvailability(facilityId: number, date: string): Observable<FacilitySlotDto[]> {
    return this.http.get<ApiResponse<FacilitySlotDto[]>>(`${this.baseUrl}/facilities/${facilityId}/availability`, { params: { date } })
      .pipe(map((r) => r.data!));
  }
  createFacility(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/facilities`, payload).pipe(map((r) => r.data!));
  }
  updateFacility(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/facilities/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteFacility(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/facilities/${id}`).pipe(map(() => void 0));
  }
  getBlackoutDates(facilityId: number): Observable<FacilityBlackoutDateDto[]> {
    return this.http.get<ApiResponse<FacilityBlackoutDateDto[]>>(`${this.baseUrl}/facilities/${facilityId}/blackout-dates`)
      .pipe(map((r) => r.data!));
  }
  addBlackoutDate(facilityId: number, blackoutDate: string, reason?: string | null): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/facilities/${facilityId}/blackout-dates`, { blackoutDate, reason })
      .pipe(map((r) => r.data!));
  }
  removeBlackoutDate(blackoutId: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/facilities/blackout-dates/${blackoutId}`).pipe(map(() => void 0));
  }

  // ---- Bookings ---------------------------------------------------------------
  getBookings(params: {
    societyId: number; facilityId?: number; status?: FacilityBookingStatus; dateFrom?: string; dateTo?: string;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FacilityBookingDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FacilityBookingDto>>>(`${this.baseUrl}/facility-bookings`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getMyBookings(params: { status?: FacilityBookingStatus; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<FacilityBookingDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FacilityBookingDto>>>(`${this.baseUrl}/facility-bookings/mine`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getBookingById(id: number): Observable<FacilityBookingDto> {
    return this.http.get<ApiResponse<FacilityBookingDto>>(`${this.baseUrl}/facility-bookings/${id}`).pipe(map((r) => r.data!));
  }
  createBooking(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/facility-bookings`, payload).pipe(map((r) => r.data!));
  }
  updateBookingStatus(id: number, status: FacilityBookingStatus, rejectionReason?: string | null): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/facility-bookings/${id}/status`, { status, rejectionReason })
      .pipe(map(() => void 0));
  }
  cancelBooking(id: number, reason?: string | null): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/facility-bookings/${id}/cancel`, { reason }).pipe(map(() => void 0));
  }
  recordPayment(id: number, paymentStatus: FacilityPaymentStatus): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/facility-bookings/${id}/payment`, { paymentStatus }).pipe(map(() => void 0));
  }
}
