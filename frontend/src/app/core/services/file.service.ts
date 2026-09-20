import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FileResponse } from '../models/file.models';

@Injectable({ providedIn: 'root' })
export class FileService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/files`;

  upload(formData: FormData): Observable<FileResponse> {
    return this.http.post<FileResponse>(this.apiUrl, formData);
  }
}