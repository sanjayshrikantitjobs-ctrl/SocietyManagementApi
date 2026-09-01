import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../../core/models/api-response.model';
import { CreateParkingFinePayload, ParkingFine } from '../models/parking-fine.model';

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
export class ParkingFineService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/parking-fines`;

  getFines(params: {
    societyId: number; vehicleId?: number; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<ParkingFine>> {
    return this.http.get<ApiResponse<PaginatedResult<ParkingFine>>>(this.baseUrl, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  createFine(payload: CreateParkingFinePayload): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }

  deleteFine(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => void 0));
  }
}
