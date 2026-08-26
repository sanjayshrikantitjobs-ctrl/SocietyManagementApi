import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  VehicleOcrReadDto, VehicleScanHistoryDto, VehicleScanResultDto, VehicleScanSource, VehicleSearchItemDto
} from '../models/vehicle-scan.model';

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
export class VehicleScanService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/vehicle-scans`;

  /** No DB write — a retried/discarded photo never leaves a trace. */
  recognize(societyId: number, file: File): Observable<VehicleOcrReadDto> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ApiResponse<VehicleOcrReadDto>>(`${this.baseUrl}/recognize`, formData, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }

  confirm(payload: {
    societyId: number; normalizedRegistrationNumber: string; rawOcrText?: string | null; confidence?: number | null;
    source: VehicleScanSource; gateId?: number | null; imageBytes?: string | null;
  }): Observable<VehicleScanResultDto> {
    return this.http.post<ApiResponse<VehicleScanResultDto>>(`${this.baseUrl}/confirm`, payload)
      .pipe(map((r) => r.data!));
  }

  search(societyId: number, query: string): Observable<VehicleSearchItemDto[]> {
    return this.http.get<ApiResponse<VehicleSearchItemDto[]>>(`${this.baseUrl}/search`, { params: toHttpParams({ societyId, query }) })
      .pipe(map((r) => r.data!));
  }

  getHistory(params: {
    societyId: number; fromDate?: string; toDate?: string; result?: number; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<VehicleScanHistoryDto>> {
    return this.http.get<ApiResponse<PaginatedResult<VehicleScanHistoryDto>>>(`${this.baseUrl}/history`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
}
