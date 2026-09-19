import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../core/models/api-response.model';

export interface InventoryItemDto {
  id: number;
  societyId: number;
  name: string;
  category?: string | null;
  unit: string;
  minimumStock: number;
  isActive: boolean;
  currentStock: number;
  isLow: boolean;
}

export interface StockTransactionDto {
  id: number;
  inventoryItemId: number;
  itemName: string;
  unit: string;
  type: number;
  quantity: number;
  transactionDate: string;
  issuedTo?: string | null;
  locationOfUse?: string | null;
  notes?: string | null;
  purchaseRequestId?: number | null;
}

export const STOCK_TYPE_LABELS: Record<number, string> = { 1: 'Stock in', 2: 'Issued', 3: 'Adjustment' };

function toParams(params: Record<string, unknown>): HttpParams {
  let p = new HttpParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '' && v !== false) p = p.set(k, String(v));
  });
  return p;
}

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/inventory`;

  getItems(params: { societyId: number; search?: string; lowStockOnly?: boolean; activeOnly?: boolean; pageNumber?: number; pageSize?: number }):
    Observable<PaginatedResult<InventoryItemDto>> {
    return this.http.get<ApiResponse<PaginatedResult<InventoryItemDto>>>(`${this.baseUrl}/items`, { params: toParams(params) }).pipe(map((r) => r.data!));
  }
  createItem(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/items`, payload).pipe(map((r) => r.data!));
  }
  updateItem(id: number, payload: Record<string, unknown>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/items/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteItem(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/items/${id}`).pipe(map(() => void 0));
  }
  getTransactions(params: { societyId: number; inventoryItemId?: number; type?: number; pageNumber?: number; pageSize?: number }):
    Observable<PaginatedResult<StockTransactionDto>> {
    return this.http.get<ApiResponse<PaginatedResult<StockTransactionDto>>>(`${this.baseUrl}/transactions`, { params: toParams(params) })
      .pipe(map((r) => r.data!));
  }
  recordTransaction(payload: Record<string, unknown>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/transactions`, payload).pipe(map((r) => r.data!));
  }
}
