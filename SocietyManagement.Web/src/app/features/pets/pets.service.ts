import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';

export interface PetDto {
  id: number;
  societyId: number;
  flatId: number;
  flatNumber: string;
  name: string;
  petType: number;
  breed?: string | null;
  photoUrl?: string | null;
  registrationNumber?: string | null;
  identification?: string | null;
  lastVaccinationDate?: string | null;
  nextVaccinationDue?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface PetSummaryDto {
  totalPets: number;
  dogs: number;
  cats: number;
  vaccinationOverdue: number;
  neverVaccinated: number;
}

export const PET_TYPE_LABELS: Record<number, string> = { 1: 'Dog', 2: 'Cat', 3: 'Bird', 4: 'Fish', 5: 'Other' };

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') httpParams = httpParams.set(key, String(value));
  });
  return httpParams;
}

@Injectable({ providedIn: 'root' })
export class PetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/pets`;

  getPets(params: {
    societyId: number; search?: string; petType?: number; sortBy?: string; sortDescending?: boolean; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<PetDto>> {
    return this.http.get<ApiResponse<PaginatedResult<PetDto>>>(this.baseUrl, { params: toHttpParams(params) }).pipe(map((r) => r.data!));
  }
  getSummary(societyId: number): Observable<PetSummaryDto> {
    return this.http.get<ApiResponse<PetSummaryDto>>(`${this.baseUrl}/summary`, { params: { societyId } }).pipe(map((r) => r.data!));
  }
  create(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }
  update(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  delete(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => void 0));
  }
}
