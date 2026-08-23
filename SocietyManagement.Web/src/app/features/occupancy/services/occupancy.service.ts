import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  FlatOccupancyDto, FlatOccupancyOverviewDto, OccupancyMemberDto, OccupancySettingsDto, OccupancyType,
  PersonDetailDto, PersonDto, PersonRelationship, ResidentStatus
} from '../models/occupancy.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the whole Owner/Tenant Occupancy module (Person,
 * FlatOccupancy/OccupancyMember, RentalAgreement, OccupancySettings) —
 * mirrors the "one service per feature module" convention (EventService,
 * VisitorService). Deliberately its own service, not folded into
 * ResidentService, since this is a parallel model to Members/FlatResidency. */
@Injectable({ providedIn: 'root' })
export class OccupancyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Persons -------------------------------------------------------------
  searchPerson(societyId: number, phone: string): Observable<PersonDto | null> {
    return this.http.get<ApiResponse<PersonDto | null>>(`${this.baseUrl}/persons/search`, { params: { societyId, phone } })
      .pipe(map((r) => r.data ?? null));
  }
  getPersonById(id: number): Observable<PersonDetailDto> {
    return this.http.get<ApiResponse<PersonDetailDto>>(`${this.baseUrl}/persons/${id}`).pipe(map((r) => r.data!));
  }
  createPerson(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/persons`, payload).pipe(map((r) => r.data!));
  }
  updatePerson(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/persons/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  // ---- Flat Occupancies -----------------------------------------------------
  getOverview(flatId: number): Observable<FlatOccupancyOverviewDto> {
    return this.http.get<ApiResponse<FlatOccupancyOverviewDto>>(`${this.baseUrl}/flat-occupancies/overview`, { params: { flatId } })
      .pipe(map((r) => r.data!));
  }
  getMembers(flatOccupancyId: number): Observable<OccupancyMemberDto[]> {
    return this.http.get<ApiResponse<OccupancyMemberDto[]>>(`${this.baseUrl}/flat-occupancies/${flatOccupancyId}/members`)
      .pipe(map((r) => r.data!));
  }
  getHistory(flatId: number, type?: OccupancyType): Observable<FlatOccupancyDto[]> {
    return this.http.get<ApiResponse<FlatOccupancyDto[]>>(`${this.baseUrl}/flat-occupancies/history`, { params: toHttpParams({ flatId, type }) })
      .pipe(map((r) => r.data!));
  }
  addOwnerMember(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flat-occupancies/owner-member`, payload).pipe(map((r) => r.data!));
  }
  addTenant(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flat-occupancies/tenant`, payload).pipe(map((r) => r.data!));
  }
  addFamilyMember(flatOccupancyId: number, payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flat-occupancies/${flatOccupancyId}/family-member`, payload)
      .pipe(map((r) => r.data!));
  }
  endOccupancy(flatOccupancyId: number, endDate: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-occupancies/${flatOccupancyId}/end`, { endDate })
      .pipe(map(() => void 0));
  }
  removeMember(memberId: number, leftDate: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/flat-occupancies/members/${memberId}/remove`, { leftDate })
      .pipe(map(() => void 0));
  }
  updateMember(memberId: number, relationship: PersonRelationship, residentStatus: ResidentStatus): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/flat-occupancies/members/${memberId}`, { relationship, residentStatus })
      .pipe(map(() => void 0));
  }

  // ---- Rental Agreements -----------------------------------------------------
  createRentalAgreement(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/rental-agreements`, payload).pipe(map((r) => r.data!));
  }
  updateRentalAgreement(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/rental-agreements/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  // ---- Settings --------------------------------------------------------------
  getSettings(societyId: number): Observable<OccupancySettingsDto> {
    return this.http.get<ApiResponse<OccupancySettingsDto>>(`${this.baseUrl}/occupancy-settings`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  updateSettings(societyId: number, allowMultiplePrimaryOwners: boolean): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/occupancy-settings`, { societyId, allowMultiplePrimaryOwners })
      .pipe(map(() => void 0));
  }
}
