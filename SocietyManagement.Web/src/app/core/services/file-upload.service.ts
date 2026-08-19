import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

/** Wraps POST /api/files/upload — every banner/logo/bill-image picker in the
 * app goes through this one call. */
@Injectable({ providedIn: 'root' })
export class FileUploadService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  upload(file: File, folder: string): Observable<string> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('folder', folder);
    return this.http.post<ApiResponse<string>>(`${this.baseUrl}/files/upload`, formData)
      .pipe(map((r) => r.data!));
  }
}
