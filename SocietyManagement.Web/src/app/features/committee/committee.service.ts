import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { CommitteeMember } from '../../core/models/committee.model';

@Injectable({ providedIn: 'root' })
export class CommitteeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/committee`;

  getCommitteeMembers(societyId: number): Observable<CommitteeMember[]> {
    return this.http.get<ApiResponse<CommitteeMember[]>>(this.baseUrl, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }

  createCommitteeMember(payload: {
    societyId: number; name: string; designation: string; flatNumber?: string; phone?: string; email?: string; displayOrder?: number;
  }): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }

  updateCommitteeMember(id: number, payload: {
    name: string; designation: string; flatNumber?: string; phone?: string; email?: string; displayOrder?: number;
  }): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }

  deleteCommitteeMember(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`).pipe(map(() => void 0));
  }
}
