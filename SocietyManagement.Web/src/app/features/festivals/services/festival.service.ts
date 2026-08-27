import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  BudgetVsActualPointDto, ChildPoolStatusDto, ContributableFlatDto, ContributionPoolDto, Festival,
  FestivalBudgetCategoryDto, FestivalBudgetRevisionDto, FestivalContributionDto, FestivalDashboardDto,
  FestivalExpenseDto, FestivalSponsorDto, FestivalTaskDto, FestivalVendorDto, FestivalVolunteerDto,
  FlatContributionDto, FlatContributionKpisDto, FlatContributionStatus, PendingContributorDto, PoolSummaryDto,
  TopContributorDto
} from '../models/festival.model';

function toHttpParams(params: Record<string, unknown>): HttpParams {
  let httpParams = new HttpParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      httpParams = httpParams.set(key, String(value));
    }
  });
  return httpParams;
}

/** One service for the whole Festival & Event Management module (Festival,
 * Budget, Contributions, Sponsors, Vendors, Expenses, Dashboard) — mirrors
 * the SocietyService pattern used for the Society Setup hierarchy. */
@Injectable({ providedIn: 'root' })
export class FestivalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Festivals -----------------------------------------------------------
  getFestivals(params: {
    societyId: number; status?: number; year?: number; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<Festival>> {
    return this.http.get<ApiResponse<PaginatedResult<Festival>>>(`${this.baseUrl}/festivals`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getFestival(id: number): Observable<Festival> {
    return this.http.get<ApiResponse<Festival>>(`${this.baseUrl}/festivals/${id}`).pipe(map((r) => r.data!));
  }
  createFestival(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festivals`, payload).pipe(map((r) => r.data!));
  }
  updateFestival(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festivals/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  updateFestivalStatus(id: number, status: number): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festivals/${id}/status`, status).pipe(map(() => void 0));
  }
  deleteFestival(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festivals/${id}`).pipe(map(() => void 0));
  }

  // ---- Budget ----------------------------------------------------------------
  getBudgetCategories(festivalId: number): Observable<FestivalBudgetCategoryDto[]> {
    return this.http.get<ApiResponse<FestivalBudgetCategoryDto[]>>(`${this.baseUrl}/festival-budget-categories`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  getBudgetRevisions(categoryId: number): Observable<FestivalBudgetRevisionDto[]> {
    return this.http.get<ApiResponse<FestivalBudgetRevisionDto[]>>(`${this.baseUrl}/festival-budget-categories/${categoryId}/revisions`)
      .pipe(map((r) => r.data!));
  }
  createBudgetCategory(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-budget-categories`, payload).pipe(map((r) => r.data!));
  }
  updateBudgetCategory(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-budget-categories/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteBudgetCategory(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-budget-categories/${id}`).pipe(map(() => void 0));
  }

  // ---- Contributions -----------------------------------------------------------
  getContributions(params: {
    festivalId: number; search?: string; paymentMethod?: number; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FestivalContributionDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FestivalContributionDto>>>(`${this.baseUrl}/festival-contributions`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getTopContributors(festivalId: number, top = 10): Observable<TopContributorDto[]> {
    return this.http.get<ApiResponse<TopContributorDto[]>>(`${this.baseUrl}/festival-contributions/top-contributors`, { params: { festivalId, top } })
      .pipe(map((r) => r.data!));
  }
  getPendingContributors(festivalId: number): Observable<PendingContributorDto[]> {
    return this.http.get<ApiResponse<PendingContributorDto[]>>(`${this.baseUrl}/festival-contributions/pending-contributors`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  createContribution(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-contributions`, payload).pipe(map((r) => r.data!));
  }
  downloadReceipt(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/festival-contributions/${id}/receipt`, { responseType: 'blob' });
  }
  resendContributionReceipt(id: number, whatsAppNumber?: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/festival-contributions/${id}/resend-whatsapp`, { whatsAppNumber: whatsAppNumber || null })
      .pipe(map(() => void 0));
  }
  getFlatContributions(params: {
    festivalId: number; search?: string; status?: FlatContributionStatus; sortBy?: string; sortDescending?: boolean;
    pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FlatContributionDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FlatContributionDto>>>(`${this.baseUrl}/festival-contributions/flat-summary`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  getFlatContributionKpis(festivalId: number): Observable<FlatContributionKpisDto> {
    return this.http.get<ApiResponse<FlatContributionKpisDto>>(`${this.baseUrl}/festival-contributions/flat-summary/kpis`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  getContributableFlats(festivalId: number): Observable<ContributableFlatDto[]> {
    return this.http.get<ApiResponse<ContributableFlatDto[]>>(`${this.baseUrl}/festival-contributions/contributable-flats`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  setContributionTargets(festivalId: number, targetAmount: number): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-contributions/targets`, { festivalId, targetAmount })
      .pipe(map((r) => r.data!));
  }
  updateFlatContributionTarget(festivalId: number, flatId: number, targetAmount: number): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-contributions/targets`, { festivalId, flatId, targetAmount })
      .pipe(map(() => void 0));
  }

  // ---- Sponsors ------------------------------------------------------------------
  getSponsors(festivalId: number): Observable<FestivalSponsorDto[]> {
    return this.http.get<ApiResponse<FestivalSponsorDto[]>>(`${this.baseUrl}/festival-sponsors`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  createSponsor(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-sponsors`, payload).pipe(map((r) => r.data!));
  }
  updateSponsor(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-sponsors/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteSponsor(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-sponsors/${id}`).pipe(map(() => void 0));
  }

  // ---- Volunteers ----------------------------------------------------------------
  getVolunteers(festivalId: number): Observable<FestivalVolunteerDto[]> {
    return this.http.get<ApiResponse<FestivalVolunteerDto[]>>(`${this.baseUrl}/festival-volunteers`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  createVolunteer(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-volunteers`, payload).pipe(map((r) => r.data!));
  }
  updateVolunteer(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-volunteers/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteVolunteer(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-volunteers/${id}`).pipe(map(() => void 0));
  }

  // ---- Tasks -----------------------------------------------------------------------
  getTasks(festivalId: number): Observable<FestivalTaskDto[]> {
    return this.http.get<ApiResponse<FestivalTaskDto[]>>(`${this.baseUrl}/festival-tasks`, { params: { festivalId } })
      .pipe(map((r) => r.data!));
  }
  createTask(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-tasks`, payload).pipe(map((r) => r.data!));
  }
  updateTask(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-tasks/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteTask(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-tasks/${id}`).pipe(map(() => void 0));
  }

  // ---- Vendors -------------------------------------------------------------------
  getVendors(params: {
    societyId: number; category?: number; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FestivalVendorDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FestivalVendorDto>>>(`${this.baseUrl}/festival-vendors`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  createVendor(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-vendors`, payload).pipe(map((r) => r.data!));
  }
  updateVendor(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-vendors/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteVendor(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-vendors/${id}`).pipe(map(() => void 0));
  }

  // ---- Expenses ------------------------------------------------------------------
  getExpenses(params: {
    festivalId: number; status?: number; festivalBudgetCategoryId?: number; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<FestivalExpenseDto>> {
    return this.http.get<ApiResponse<PaginatedResult<FestivalExpenseDto>>>(`${this.baseUrl}/festival-expenses`, { params: toHttpParams(params) })
      .pipe(map((r) => r.data!));
  }
  createExpense(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/festival-expenses`, payload).pipe(map((r) => r.data!));
  }
  updateExpense(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteExpense(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}`).pipe(map(() => void 0));
  }
  submitExpense(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}/submit`, {}).pipe(map(() => void 0));
  }
  approveExpense(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}/approve`, {}).pipe(map(() => void 0));
  }
  rejectExpense(id: number, reason: string): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}/reject`, { reason }).pipe(map(() => void 0));
  }
  markExpensePaid(id: number): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/festival-expenses/${id}/mark-paid`, {}).pipe(map(() => void 0));
  }

  // ---- Dashboard -----------------------------------------------------------------
  getDashboard(festivalId: number): Observable<FestivalDashboardDto> {
    return this.http.get<ApiResponse<FestivalDashboardDto>>(`${this.baseUrl}/festival-dashboard/${festivalId}`)
      .pipe(map((r) => r.data!));
  }

  // ---- Contribution Pools ----------------------------------------------------
  getContributionPools(societyId: number): Observable<ContributionPoolDto[]> {
    return this.http.get<ApiResponse<ContributionPoolDto[]>>(`${this.baseUrl}/festivals/contribution-pools`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  getPoolSummary(festivalId: number): Observable<PoolSummaryDto> {
    return this.http.get<ApiResponse<PoolSummaryDto>>(`${this.baseUrl}/festivals/${festivalId}/pool-summary`)
      .pipe(map((r) => r.data!));
  }
  getChildPoolStatus(festivalId: number): Observable<ChildPoolStatusDto | null> {
    return this.http.get<ApiResponse<ChildPoolStatusDto | null>>(`${this.baseUrl}/festivals/${festivalId}/pool-status`)
      .pipe(map((r) => r.data ?? null));
  }
}
