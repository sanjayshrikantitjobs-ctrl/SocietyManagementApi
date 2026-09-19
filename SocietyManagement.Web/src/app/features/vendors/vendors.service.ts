import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';

export interface VendorDto {
  id: number;
  societyId: number;
  name: string;
  category: number;
  contactPerson?: string | null;
  phone: string;
  email?: string | null;
  address?: string | null;
  gstNumber?: string | null;
  contractStart?: string | null;
  contractEnd?: string | null;
  performanceNotes?: string | null;
  isActive: boolean;
  totalPaid: number;
}

export const VENDOR_CATEGORY_LABELS: Record<number, string> = {
  1: 'Electrical', 2: 'Plumbing', 3: 'Security', 4: 'Housekeeping', 5: 'Lift', 6: 'Garden', 7: 'Pest Control', 8: 'Other'
};

@Injectable({ providedIn: 'root' })
export class VendorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/vendors`;

  getVendors(params: {
    societyId: number; search?: string; category?: number; isActive?: boolean; expiringWithinDays?: number;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<VendorDto>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') httpParams = httpParams.set(key, String(value));
    });
    return this.http.get<ApiResponse<PaginatedResult<VendorDto>>>(this.baseUrl, { params: httpParams }).pipe(map((r) => r.data!));
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
