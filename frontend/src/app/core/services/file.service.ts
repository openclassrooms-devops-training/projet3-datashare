import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FileMetadataResponse, FileResponse, FileStatusFilter } from '../models/file.models';

@Injectable({ providedIn: 'root' })
export class FileService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/files`;

  upload(formData: FormData): Observable<FileResponse> {
    return this.http.post<FileResponse>(this.apiUrl, formData);
  }

  list(status: FileStatusFilter): Observable<FileResponse[]> {
    return this.http.get<FileResponse[]>(this.apiUrl, { params: { status } });
  }

  delete(fileId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${fileId}`);
  }

  getMetadata(token: string): Observable<FileMetadataResponse> {
    return this.http.get<FileMetadataResponse>(`${this.apiUrl}/download/${token}`);
  }

  download(token: string, password?: string): Observable<Blob> {
    return this.http.post(`${this.apiUrl}/download/${token}`, { password }, { responseType: 'blob' });
  }
}