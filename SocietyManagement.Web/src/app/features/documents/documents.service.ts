import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';

export interface SocietyDocumentDto {
  id: number;
  societyId: number;
  title: string;
  description?: string | null;
  category: number;
  fileUrl: string;
  fileName?: string | null;
  expiryDate?: string | null;
  visibility: number;
  createdAt: string;
}

export const DOCUMENT_CATEGORY_LABELS: Record<number, string> = {
  1: 'Bylaws', 2: 'Policy', 3: 'Circular', 4: 'Meeting Minutes', 5: 'Certificate', 6: 'Contract', 7: 'Financial', 8: 'Other'
};
export const DOCUMENT_VISIBILITY_LABELS: Record<number, string> = { 1: 'Admin only', 2: 'All residents' };

@Injectable({ providedIn: 'root' })
export class SocietyDocumentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/society-documents`;

  getDocuments(params: {
    societyId: number; search?: string; category?: number; expiringOnly?: boolean; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<SocietyDocumentDto>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '' && value !== false) httpParams = httpParams.set(key, String(value));
    });
    return this.http.get<ApiResponse<PaginatedResult<SocietyDocumentDto>>>(this.baseUrl, { params: httpParams }).pipe(map((r) => r.data!));
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
