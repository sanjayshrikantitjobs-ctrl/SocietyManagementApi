import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import { EventCapacitySummaryDto, EventDto, EventRsvpDto } from '../models/event.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the Events module (Event CRUD/transitions + RSVP/check-in),
 * mirrors ResidentService's shape. */
@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Events -------------------------------------------------------------
  getEvents(params: { societyId: number; status?: number; festivalId?: number; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<EventDto>> {
    return this.http.get<ApiResponse<PaginatedResult<EventDto>>>(`${this.baseUrl}/events`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getEventById(id: number): Observable<EventDto> {
    return this.http.get<ApiResponse<EventDto>>(`${this.baseUrl}/events/${id}`).pipe(map((r) => r.data!));
  }
  getCapacitySummary(id: number): Observable<EventCapacitySummaryDto> {
    return this.http.get<ApiResponse<EventCapacitySummaryDto>>(`${this.baseUrl}/events/${id}/capacity-summary`).pipe(map((r) => r.data!));
  }
  createEvent(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/events`, payload).pipe(map((r) => r.data!));
  }
  updateEvent(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/events/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteEvent(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/events/${id}`).pipe(map(() => void 0));
  }
  openEvent(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/events/${id}/open`, {}).pipe(map(() => void 0));
  }
  closeEvent(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/events/${id}/close`, {}).pipe(map(() => void 0));
  }
  completeEvent(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/events/${id}/complete`, {}).pipe(map(() => void 0));
  }
  cancelEvent(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/events/${id}/cancel`, {}).pipe(map(() => void 0));
  }

  // ---- RSVPs ----------------------------------------------------------------
  getMyRsvp(eventId: number): Observable<EventRsvpDto | null> {
    return this.http.get<ApiResponse<EventRsvpDto | null>>(`${this.baseUrl}/event-rsvps/mine`, { params: { eventId } })
      .pipe(map((r) => r.data ?? null));
  }
  getRsvpsForEvent(eventId: number): Observable<EventRsvpDto[]> {
    return this.http.get<ApiResponse<EventRsvpDto[]>>(`${this.baseUrl}/event-rsvps`, { params: { eventId } })
      .pipe(map((r) => r.data!));
  }
  createOrUpdateRsvp(eventId: number, headCount: number): Observable<EventRsvpDto> {
    return this.http.post<ApiResponse<EventRsvpDto>>(`${this.baseUrl}/event-rsvps`, { eventId, headCount })
      .pipe(map((r) => r.data!));
  }
  cancelRsvp(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/event-rsvps/${id}/cancel`, {}).pipe(map(() => void 0));
  }
  checkIn(qrToken: string, actualHeadCount: number): Observable<EventRsvpDto> {
    return this.http.post<ApiResponse<EventRsvpDto>>(`${this.baseUrl}/event-rsvps/check-in`, { qrToken, actualHeadCount })
      .pipe(map((r) => r.data!));
  }
  getRsvpByToken(qrToken: string): Observable<EventRsvpDto> {
    return this.http.get<ApiResponse<EventRsvpDto>>(`${this.baseUrl}/event-rsvps/by-token/${qrToken}`)
      .pipe(map((r) => r.data!));
  }
}
