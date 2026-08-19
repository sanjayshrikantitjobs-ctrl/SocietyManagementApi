import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  FineRecordDto, MaintenanceBillDetailDto, MaintenanceBillDto, MaintenanceCategoryDto,
  MaintenanceDashboardDto, MaintenanceSettingsDto, SpecialChargeDto
} from '../models/maintenance.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the whole Maintenance Management module (Categories,
 * Settings, Special Charges, Fines, Bills, Dashboard) — mirrors the
 * FestivalService/SocietyService "one service per module" pattern. */
@Injectable({ providedIn: 'root' })
export class MaintenanceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Categories ------------------------------------------------------------
  getCategories(societyId: number): Observable<MaintenanceCategoryDto[]> {
    return this.http.get<ApiResponse<MaintenanceCategoryDto[]>>(`${this.baseUrl}/maintenance-categories`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  createCategory(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/maintenance-categories`, payload).pipe(map((r) => r.data!));
  }
  updateCategory(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/maintenance-categories/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteCategory(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/maintenance-categories/${id}`).pipe(map(() => void 0));
  }

  // ---- Settings ----------------------------------------------------------------
  getSettings(societyId: number): Observable<MaintenanceSettingsDto> {
    return this.http.get<ApiResponse<MaintenanceSettingsDto>>(`${this.baseUrl}/maintenance-settings`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  saveSettings(payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/maintenance-settings`, payload).pipe(map(() => void 0));
  }

  // ---- Special Charges -----------------------------------------------------------
  getSpecialCharges(societyId: number, flatId?: number): Observable<SpecialChargeDto[]> {
    return this.http.get<ApiResponse<SpecialChargeDto[]>>(`${this.baseUrl}/special-charges`, { params: toHttpParams({ societyId, flatId }) })
      .pipe(map((r) => r.data!));
  }
  createSpecialCharge(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/special-charges`, payload).pipe(map((r) => r.data!));
  }
  updateSpecialCharge(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/special-charges/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteSpecialCharge(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/special-charges/${id}`).pipe(map(() => void 0));
  }

  // ---- Fines ---------------------------------------------------------------------
  getFines(societyId: number, flatId?: number, status?: number): Observable<FineRecordDto[]> {
    return this.http.get<ApiResponse<FineRecordDto[]>>(`${this.baseUrl}/fine-records`, { params: toHttpParams({ societyId, flatId, status }) })
      .pipe(map((r) => r.data!));
  }
  createFine(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/fine-records`, payload).pipe(map((r) => r.data!));
  }
  waiveFine(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/fine-records/${id}/waive`, {}).pipe(map(() => void 0));
  }
  deleteFine(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/fine-records/${id}`).pipe(map(() => void 0));
  }

  // ---- Bills ---------------------------------------------------------------------
  getBills(params: {
    societyId: number; flatId?: number; status?: number; billMonth?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<MaintenanceBillDto>> {
    return this.http.get<ApiResponse<PaginatedResult<MaintenanceBillDto>>>(`${this.baseUrl}/maintenance/bills`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getBillById(id: number): Observable<MaintenanceBillDetailDto> {
    return this.http.get<ApiResponse<MaintenanceBillDetailDto>>(`${this.baseUrl}/maintenance/bills/${id}`).pipe(map((r) => r.data!));
  }
  downloadBillPdf(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/maintenance/bills/${id}/pdf`, { responseType: 'blob' });
  }
  generateBills(societyId: number, billMonth?: string): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/maintenance/generate`, { societyId, billMonth })
      .pipe(map((r) => r.data!));
  }
  recordPayment(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/maintenance/payment`, payload).pipe(map((r) => r.data!));
  }
  resendWhatsApp(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/maintenance/bills/${id}/resend-whatsapp`, {}).pipe(map(() => void 0));
  }

  // ---- Dashboard -----------------------------------------------------------------
  getDashboard(societyId: number): Observable<MaintenanceDashboardDto> {
    return this.http.get<ApiResponse<MaintenanceDashboardDto>>(`${this.baseUrl}/maintenance/dashboard`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
}
