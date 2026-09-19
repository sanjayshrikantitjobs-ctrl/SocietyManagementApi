import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';

export interface PurchaseItemDto {
  id: number;
  itemName: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice: number;
  receivedQuantity: number;
  inventoryItemId?: number | null;
}

export interface PurchaseRequestDto {
  id: number;
  societyId: number;
  title: string;
  description?: string | null;
  priority: number;
  status: number;
  dueDate?: string | null;
  vendorId?: number | null;
  vendorName?: string | null;
  requestedByName: string;
  createdAt: string;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  orderedAt?: string | null;
  estimatedTotal: number;
  items: PurchaseItemDto[];
}

export interface PurchaseItemInput {
  itemName: string;
  quantity: number;
  unit: string;
  estimatedUnitPrice: number;
  inventoryItemId: number | null;
}

export const PURCHASE_STATUS = {
  Draft: 1, PendingApproval: 2, Approved: 3, Rejected: 4, Ordered: 5, PartiallyReceived: 6, Received: 7, Cancelled: 8
} as const;

export const PURCHASE_STATUS_LABELS: Record<number, string> = {
  1: 'Draft', 2: 'Pending approval', 3: 'Approved', 4: 'Rejected', 5: 'Ordered', 6: 'Partially received', 7: 'Received', 8: 'Cancelled'
};
export const PURCHASE_PRIORITY_LABELS: Record<number, string> = { 1: 'Low', 2: 'Normal', 3: 'High' };

@Injectable({ providedIn: 'root' })
export class PurchaseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/purchases`;

  getAll(params: { societyId: number; search?: string; status?: number; pageNumber?: number; pageSize?: number }): Observable<PaginatedResult<PurchaseRequestDto>> {
    let p = new HttpParams();
    Object.entries(params).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v)); });
    return this.http.get<ApiResponse<PaginatedResult<PurchaseRequestDto>>>(this.baseUrl, { params: p }).pipe(map((r) => r.data!));
  }
  create(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(this.baseUrl, payload).pipe(map((r) => r.data!));
  }
  update(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  submit(id: number): Observable<void> { return this.post(id, 'submit', {}); }
  approve(id: number): Observable<void> { return this.post(id, 'approve', {}); }
  reject(id: number, reason: string): Observable<void> { return this.post(id, 'reject', { reason }); }
  order(id: number, vendorId: number | null): Observable<void> { return this.post(id, 'order', { vendorId }); }
  receive(id: number, lines: { itemId: number; quantity: number }[]): Observable<void> { return this.post(id, 'receive', { lines }); }
  cancel(id: number): Observable<void> { return this.post(id, 'cancel', {}); }

  private post(id: number, action: string, body: unknown): Observable<void> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/${id}/${action}`, body).pipe(map(() => void 0));
  }
}
