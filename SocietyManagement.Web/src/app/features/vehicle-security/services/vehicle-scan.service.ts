import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  PlateOcrResultDto, VehicleScanHistoryDto, VehicleScanResultDto, VehicleScanSource, VehicleSearchItemDto
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

  confirm(payload: {
    societyId: number; normalizedRegistrationNumber: string; rawOcrText?: string | null; confidence?: number | null;
    source: VehicleScanSource; gateId?: number | null; imageBytes?: string | null;
  }): Observable<VehicleScanResultDto> {
    return this.http.post<ApiResponse<VehicleScanResultDto>>(`${this.baseUrl}/confirm`, payload)
      .pipe(map((r) => r.data!));
  }

  /** OCR assist — imageBase64 is the FULL photo; corners are the user's 4
   * dragged points (TopLeft/TopRight/BottomRight/BottomLeft) in that photo's
   * own natural pixel space. The server perspective-warps the marked region
   * before running OCR. Purely advisory: the result is a prefill
   * suggestion, never sent on its own to /confirm. */
  recognizePlate(imageBase64: string, corners: { x: number; y: number }[]): Observable<PlateOcrResultDto> {
    return this.http.post<ApiResponse<PlateOcrResultDto>>(`${this.baseUrl}/ocr-preview`, { imageBytes: imageBase64, corners })
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
