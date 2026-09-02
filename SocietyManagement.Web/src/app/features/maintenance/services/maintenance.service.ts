import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  BulkRecordPaymentResultDto, BulkSetBillsUnpaidResultDto, FineRecordDto, MaintenanceBillDetailDto, MaintenanceBillDto,
  MaintenanceCategoryDto, MaintenanceDashboardDto, MaintenanceSettingsDto, SpecialChargeDto, WaterTankerCollectionDto,
  WaterTankerLogDto, WaterTankerLogMonthSummaryDto, WaterTankerLogPayload, WaterTankerMonthSummaryDto
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
  getSpecialCharges(params: {
    societyId: number; flatId?: number; search?: string; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<SpecialChargeDto>> {
    return this.http.get<ApiResponse<PaginatedResult<SpecialChargeDto>>>(`${this.baseUrl}/special-charges`, { params: toHttpParams(params) })
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
  getFines(params: {
    societyId: number; flatId?: number; status?: number; search?: string; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FineRecordDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FineRecordDto>>>(`${this.baseUrl}/fine-records`, { params: toHttpParams(params) })
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
  /** Pays each bill's own full outstanding balance — mode/date/notes are
   * shared across the batch, amount is never client-supplied (see
   * BulkRecordPaymentCommand's doc comment on the backend). */
  bulkRecordPayment(maintenanceBillIds: number[], payload: {
    paymentDate: string; paymentMode: number; transactionReference?: string | null; notes?: string | null;
  }): Observable<BulkRecordPaymentResultDto[]> {
    return this.http.post<ApiResponse<BulkRecordPaymentResultDto[]>>(`${this.baseUrl}/maintenance/bulk-payment`, { maintenanceBillIds, ...payload })
      .pipe(map((r) => r.data!));
  }
  resendWhatsApp(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/maintenance/bills/${id}/resend-whatsapp`, {}).pipe(map(() => void 0));
  }
  markBillUnpaid(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/maintenance/bills/${id}/mark-unpaid`, {}).pipe(map(() => void 0));
  }
  bulkMarkUnpaid(maintenanceBillIds: number[]): Observable<BulkSetBillsUnpaidResultDto[]> {
    return this.http.post<ApiResponse<BulkSetBillsUnpaidResultDto[]>>(`${this.baseUrl}/maintenance/bulk-mark-unpaid`, { maintenanceBillIds })
      .pipe(map((r) => r.data!));
  }

  // ---- Dashboard -----------------------------------------------------------------
  getDashboard(societyId: number): Observable<MaintenanceDashboardDto> {
    return this.http.get<ApiResponse<MaintenanceDashboardDto>>(`${this.baseUrl}/maintenance/dashboard`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }

  // ---- Water Tanker ----------------------------------------------------------------
  getWaterTankerCollections(params: {
    societyId: number; month: string; isPaid?: boolean; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<WaterTankerCollectionDto>> {
    return this.http.get<ApiResponse<PaginatedResult<WaterTankerCollectionDto>>>(`${this.baseUrl}/water-tanker`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getWaterTankerMonths(societyId: number): Observable<string[]> {
    return this.http.get<ApiResponse<string[]>>(`${this.baseUrl}/water-tanker/months`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  getWaterTankerSummary(societyId: number, month: string): Observable<WaterTankerMonthSummaryDto> {
    return this.http.get<ApiResponse<WaterTankerMonthSummaryDto>>(`${this.baseUrl}/water-tanker/summary`, { params: { societyId, month } })
      .pipe(map((r) => r.data!));
  }
  generateWaterTankerCharges(societyId: number, month: string, amount: number): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/water-tanker/generate`, { societyId, month, amount })
      .pipe(map((r) => r.data!));
  }
  recordWaterTankerPayment(id: number, paymentDate: string, notes?: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/water-tanker/${id}/pay`, { paymentDate, notes })
      .pipe(map(() => void 0));
  }
  getMyWaterTankerCollections(): Observable<WaterTankerCollectionDto[]> {
    return this.http.get<ApiResponse<WaterTankerCollectionDto[]>>(`${this.baseUrl}/water-tanker/mine`)
      .pipe(map((r) => r.data!));
  }

  // ---- Water Tanker Log (operational log, replaces per-flat billing above going forward) ------
  getWaterTankerLogs(params: {
    societyId: number; month: string; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<WaterTankerLogDto>> {
    return this.http.get<ApiResponse<PaginatedResult<WaterTankerLogDto>>>(`${this.baseUrl}/water-tanker-logs`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getWaterTankerLogSummary(societyId: number, month: string): Observable<WaterTankerLogMonthSummaryDto> {
    return this.http.get<ApiResponse<WaterTankerLogMonthSummaryDto>>(`${this.baseUrl}/water-tanker-logs/summary`, { params: { societyId, month } })
      .pipe(map((r) => r.data!));
  }
  createWaterTankerLog(societyId: number, payload: WaterTankerLogPayload): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/water-tanker-logs`, { societyId, ...payload }).pipe(map((r) => r.data!));
  }
  updateWaterTankerLog(id: number, payload: WaterTankerLogPayload): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/water-tanker-logs/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteWaterTankerLog(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/water-tanker-logs/${id}`).pipe(map(() => void 0));
  }
}
