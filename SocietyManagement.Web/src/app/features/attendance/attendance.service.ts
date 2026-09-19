import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';

export interface DailyAttendanceDto {
  staffId: number;
  staffName: string;
  category: number;
  photoUrl?: string | null;
  attendanceId?: number | null;
  status?: number | null;
  checkInTime?: string | null;
  checkOutTime?: string | null;
  notes?: string | null;
}

export interface MonthlyAttendanceRowDto {
  staffId: number;
  staffName: string;
  category: number;
  present: number;
  late: number;
  halfDay: number;
  absent: number;
  leave: number;
  unmarked: number;
}

export interface MonthlyAttendanceDto {
  year: number;
  month: number;
  daysInPeriod: number;
  rows: MonthlyAttendanceRowDto[];
}

export const ATTENDANCE_STATUS_OPTIONS = [
  { value: 1, label: 'Present' },
  { value: 2, label: 'Late' },
  { value: 3, label: 'Half day' },
  { value: 4, label: 'Absent' },
  { value: 5, label: 'Leave' }
];

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/staff-attendance`;

  getDaily(societyId: number, date: string): Observable<DailyAttendanceDto[]> {
    return this.http.get<ApiResponse<DailyAttendanceDto[]>>(`${this.baseUrl}/daily`, { params: { societyId, date } }).pipe(map((r) => r.data!));
  }
  getMonthly(societyId: number, year: number, month: number): Observable<MonthlyAttendanceDto> {
    return this.http.get<ApiResponse<MonthlyAttendanceDto>>(`${this.baseUrl}/monthly`, { params: { societyId, year, month } })
      .pipe(map((r) => r.data!));
  }
  mark(payload: {
    societyId: number; staffId: number; date: string; status: number; checkInTime: string | null; checkOutTime: string | null; notes: string | null;
  }): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }
}
