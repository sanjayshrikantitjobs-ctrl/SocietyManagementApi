import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse, PaginatedResult } from '../../../core/models/api-response.model';
import {
  Building, Flat, Floor, ParkingSlot, Society, Wing
} from '../../../core/models/society.model';

/** One service for the whole Society Setup hierarchy (Society/Building/Wing/
 * Floor/Flat/ParkingSlot) — mirrors the six matching backend controllers. */
@Injectable({ providedIn: 'root' })
export class SocietyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ---- Societies -----------------------------------------------------------
  getSocieties(): Observable<Society[]> {
    return this.http.get<ApiResponse<Society[]>>(`${this.baseUrl}/societies`).pipe(map((r) => r.data!));
  }
  getSociety(id: number): Observable<Society> {
    return this.http.get<ApiResponse<Society>>(`${this.baseUrl}/societies/${id}`).pipe(map((r) => r.data!));
  }
  createSociety(payload: Partial<Society>): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/societies`, payload).pipe(map((r) => r.data!));
  }
  updateSociety(id: number, payload: Partial<Society>): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/societies/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteSociety(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/societies/${id}`).pipe(map(() => void 0));
  }

  // ---- Buildings -------------------------------------------------------------
  getBuildings(societyId: number): Observable<Building[]> {
    return this.http.get<ApiResponse<Building[]>>(`${this.baseUrl}/buildings`, { params: { societyId } })
      .pipe(map((r) => r.data!));
  }
  createBuilding(societyId: number, name: string, description?: string): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/buildings`, { societyId, name, description })
      .pipe(map((r) => r.data!));
  }
  deleteBuilding(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/buildings/${id}`).pipe(map(() => void 0));
  }

  // ---- Wings -------------------------------------------------------------------
  getWings(buildingId: number): Observable<Wing[]> {
    return this.http.get<ApiResponse<Wing[]>>(`${this.baseUrl}/wings`, { params: { buildingId } })
      .pipe(map((r) => r.data!));
  }
  createWing(buildingId: number, name: string): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/wings`, { buildingId, name }).pipe(map((r) => r.data!));
  }
  deleteWing(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/wings/${id}`).pipe(map(() => void 0));
  }

  // ---- Floors ------------------------------------------------------------------
  getFloors(wingId: number): Observable<Floor[]> {
    return this.http.get<ApiResponse<Floor[]>>(`${this.baseUrl}/floors`, { params: { wingId } })
      .pipe(map((r) => r.data!));
  }
  createFloor(wingId: number, floorNumber: number, name?: string): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/floors`, { wingId, floorNumber, name })
      .pipe(map((r) => r.data!));
  }
  deleteFloor(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/floors/${id}`).pipe(map(() => void 0));
  }

  // ---- Flats -------------------------------------------------------------------
  getFlats(params: {
    floorId?: number; status?: number; search?: string; pageNumber?: number; pageSize?: number;
  }): Observable<PaginatedResult<Flat>> {
    let httpParams = new HttpParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    });
    return this.http.get<ApiResponse<PaginatedResult<Flat>>>(`${this.baseUrl}/flats`, { params: httpParams })
      .pipe(map((r) => r.data!));
  }
  getFlat(id: number): Observable<Flat> {
    return this.http.get<ApiResponse<Flat>>(`${this.baseUrl}/flats/${id}`).pipe(map((r) => r.data!));
  }
  getMyFlats(): Observable<Flat[]> {
    return this.http.get<ApiResponse<Flat[]>>(`${this.baseUrl}/flats/mine`).pipe(map((r) => r.data!));
  }
  createFlat(payload: {
    floorId: number; flatNumber: string; flatType: number; areaSqFt?: number;
    ownerName?: string; ownerPhone?: string; ownerEmail?: string;
  }): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/flats`, payload).pipe(map((r) => r.data!));
  }
  updateFlat(id: number, payload: {
    flatNumber: string; flatType: number; areaSqFt?: number; status: number;
    ownerName?: string; ownerPhone?: string; ownerEmail?: string;
  }): Observable<void> {
    return this.http.put<ApiResponse<void>>(`${this.baseUrl}/flats/${id}`, { id, ...payload }).pipe(map(() => void 0));
  }
  deleteFlat(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/flats/${id}`).pipe(map(() => void 0));
  }

  // ---- Parking Slots -------------------------------------------------------------
  getParkingSlots(societyId: number, status?: number): Observable<ParkingSlot[]> {
    const params: Record<string, string> = { societyId: String(societyId) };
    if (status) params['status'] = String(status);
    return this.http.get<ApiResponse<ParkingSlot[]>>(`${this.baseUrl}/parkingslots`, { params })
      .pipe(map((r) => r.data!));
  }
  createParkingSlot(societyId: number, slotNumber: string, type: number): Observable<number> {
    return this.http.post<ApiResponse<number>>(`${this.baseUrl}/parkingslots`, { societyId, slotNumber, type })
      .pipe(map((r) => r.data!));
  }
  allocateParkingSlot(id: number, flatId: number | null): Observable<void> {
    const params: Record<string, string> = flatId ? { flatId: String(flatId) } : {};
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/parkingslots/${id}/allocate`, {}, { params })
      .pipe(map(() => void 0));
  }
  deleteParkingSlot(id: number): Observable<void> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/parkingslots/${id}`).pipe(map(() => void 0));
  }
}
