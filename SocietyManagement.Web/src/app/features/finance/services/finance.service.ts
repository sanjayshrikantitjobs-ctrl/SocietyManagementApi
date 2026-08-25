import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  ExpenseDto, FinanceExpenseRowDto, FinanceIncomeRowDto, FinanceLedgerPageDto, FinanceOutstandingRowDto,
  FinanceOverviewDto, FinanceReportSummaryDto
} from '../models/finance.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the whole Finance module — mirrors MaintenanceService's
 * "one service per module" shape. */
@Injectable({ providedIn: 'root' })
export class FinanceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getOverview(societyId: number): Observable<FinanceOverviewDto> {
    return this.http.get<ApiResponse<FinanceOverviewDto>>(`${this.baseUrl}/finance/overview`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }

  getIncome(params: {
    societyId: number; source?: number; dateFrom?: string; dateTo?: string; search?: string;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FinanceIncomeRowDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FinanceIncomeRowDto>>>(`${this.baseUrl}/finance/income`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  downloadReceiptPdf(source: number, id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/finance/receipts/${source}/${id}/pdf`, { responseType: 'blob' });
  }

  getExpenses(params: {
    societyId: number; source?: number; category?: number; dateFrom?: string; dateTo?: string; search?: string;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FinanceExpenseRowDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FinanceExpenseRowDto>>>(`${this.baseUrl}/finance/expenses`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getExpenseById(id: number): Observable<ExpenseDto> {
    return this.http.get<ApiResponse<ExpenseDto>>(`${this.baseUrl}/finance/expenses/${id}`).pipe(map((r) => r.data!));
  }
  createExpense(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/finance/expenses`, payload).pipe(map((r) => r.data!));
  }
  updateExpense(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/finance/expenses/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteExpense(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/finance/expenses/${id}`).pipe(map(() => void 0));
  }

  getOutstanding(params: {
    societyId: number; source?: number; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FinanceOutstandingRowDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FinanceOutstandingRowDto>>>(`${this.baseUrl}/finance/outstanding`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  getLedger(params: {
    societyId: number; dateFrom?: string; dateTo?: string; pageNumber?: number; pageSize?: number;
  }): Observable<FinanceLedgerPageDto> {
    return this.http.get<ApiResponse<FinanceLedgerPageDto>>(`${this.baseUrl}/finance/ledger`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }

  getReportSummary(societyId: number, dateFrom?: string, dateTo?: string): Observable<FinanceReportSummaryDto> {
    return this.http.get<ApiResponse<FinanceReportSummaryDto>>(`${this.baseUrl}/finance/reports/summary`, { params: toHttpParams({ societyId, dateFrom, dateTo }) })
      .pipe(map((r) => r.data!));
  }
  exportReportPdf(societyId: number, dateFrom?: string, dateTo?: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/finance/reports/export/pdf`, { params: toHttpParams({ societyId, dateFrom, dateTo }), responseType: 'blob' });
  }
  exportReportExcel(societyId: number, dateFrom?: string, dateTo?: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/finance/reports/export/excel`, { params: toHttpParams({ societyId, dateFrom, dateTo }), responseType: 'blob' });
  }
}
