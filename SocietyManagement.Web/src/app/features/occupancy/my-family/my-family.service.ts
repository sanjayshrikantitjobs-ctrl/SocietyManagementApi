import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { OccupancyMember, PersonRelationship } from '../../../core/models/occupancy-member.model';

@Injectable({ providedIn: 'root' })
export class MyFamilyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/flat-occupancies/mine`;

  getMyFamilyMembers(): Observable<OccupancyMember[]> {
    return this.http.get<ApiResponse<OccupancyMember[]>>(`${this.baseUrl}/members`).pipe(map((r) => r.data!));
  }

  addFamilyMember(payload: {
    firstName: string; lastName: string; phone?: string; email?: string; relationship: PersonRelationship;
  }): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/members`, payload).pipe(map((r) => r.data!));
  }
}
