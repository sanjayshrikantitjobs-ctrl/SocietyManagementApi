import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  EmergencyContactDto, FlatOccupancySummaryDto, FlatResaleListingDto, FlatResidencyDto,
  MemberDetailDto, MemberDto, VehicleDto
} from '../models/resident.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the whole Resident Management module (Members,
 * FlatResidencies, Vehicles, EmergencyContacts, FlatResaleListings) —
 * mirrors the FestivalService/MaintenanceService "one service per module" pattern. */
@Injectable({ providedIn: 'root' })
export class ResidentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Members -------------------------------------------------------------
  getMembers(params: { societyId: number; search?: string; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<MemberDto>> {
    return this.http.get<ApiResponse<PaginatedResult<MemberDto>>>(`${this.baseUrl}/members`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getMemberById(id: number): Observable<MemberDetailDto> {
    return this.http.get<ApiResponse<MemberDetailDto>>(`${this.baseUrl}/members/${id}`).pipe(map((r) => r.data!));
  }
  createMember(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/members`, payload).pipe(map((r) => r.data!));
  }
  updateMember(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/members/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteMember(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/members/${id}`).pipe(map(() => void 0));
  }
  createLogin(memberId: number, roleId: number): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/members/${memberId}/create-login`, { roleId })
      .pipe(map((r) => r.data!));
  }

  // ---- Flat Residencies ----------------------------------------------------------
  getResidencies(flatId: number): Observable<FlatResidencyDto[]> {
    return this.http.get<ApiResponse<FlatResidencyDto[]>>(`${this.baseUrl}/flat-residencies`, { params: { flatId } })
      .pipe(map((r) => r.data!));
  }
  getOccupancySummary(flatId: number): Observable<FlatOccupancySummaryDto> {
    return this.http.get<ApiResponse<FlatOccupancySummaryDto>>(`${this.baseUrl}/flat-residencies/occupancy-summary`, { params: { flatId } })
      .pipe(map((r) => r.data!));
  }
  getOwnershipHistory(flatId: number): Observable<FlatResidencyDto[]> {
    return this.http.get<ApiResponse<FlatResidencyDto[]>>(`${this.baseUrl}/flat-residencies/ownership-history`, { params: { flatId } })
      .pipe(map((r) => r.data!));
  }
  createResidency(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flat-residencies`, payload).pipe(map((r) => r.data!));
  }
  endResidency(id: number, moveOutDate: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-residencies/${id}/end`, { moveOutDate }).pipe(map(() => void 0));
  }
  deleteResidency(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/flat-residencies/${id}`).pipe(map(() => void 0));
  }

  // ---- Vehicles -------------------------------------------------------------------
  getVehicles(params: { memberId?: number; societyId?: number }): Observable<VehicleDto[]> {
    return this.http.get<ApiResponse<VehicleDto[]>>(`${this.baseUrl}/vehicles`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  createVehicle(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/vehicles`, payload).pipe(map((r) => r.data!));
  }
  updateVehicle(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/vehicles/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteVehicle(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/vehicles/${id}`).pipe(map(() => void 0));
  }

  // ---- Emergency Contacts -----------------------------------------------------------
  getEmergencyContacts(flatId: number): Observable<EmergencyContactDto[]> {
    return this.http.get<ApiResponse<EmergencyContactDto[]>>(`${this.baseUrl}/emergency-contacts`, { params: { flatId } })
      .pipe(map((r) => r.data!));
  }
  createEmergencyContact(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/emergency-contacts`, payload).pipe(map((r) => r.data!));
  }
  updateEmergencyContact(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/emergency-contacts/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteEmergencyContact(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/emergency-contacts/${id}`).pipe(map(() => void 0));
  }

  // ---- Flat Resale Listings ---------------------------------------------------------
  getListings(params: { societyId: number; status?: number; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<FlatResaleListingDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FlatResaleListingDto>>>(`${this.baseUrl}/flat-resale-listings`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  createListing(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flat-resale-listings`, payload).pipe(map((r) => r.data!));
  }
  updateListing(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  markUnderNegotiation(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}/under-negotiation`, {}).pipe(map(() => void 0));
  }
  markSold(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}/sold`, {}).pipe(map(() => void 0));
  }
  withdrawListing(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}/withdraw`, {}).pipe(map(() => void 0));
  }
  issueNoc(id: number, nocIssuedDate: string, nocDocumentUrl: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}/issue-noc`, { nocIssuedDate, nocDocumentUrl })
      .pipe(map(() => void 0));
  }
  deleteListing(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/flat-resale-listings/${id}`).pipe(map(() => void 0));
  }
}
