import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, RegisterRequest, RefreshRequest, TokenResponse, UserResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/auth`;

  login(request: LoginRequest): Observable<TokenResponse> {
    //send login request to the backend
    return this.http.post<TokenResponse>(`${this.apiUrl}/login`, request);
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    //send register request to the backend
     return this.http.post<UserResponse>(`${this.apiUrl}/register`, request);
  }

  refresh(): Observable<TokenResponse> {
    const request: RefreshRequest = { refreshToken: localStorage.getItem('refresh_token') ?? '' };
    return this.http.post<TokenResponse>(`${this.apiUrl}/refresh`, request);
  }

  logout(): Observable<void> {
    const request: RefreshRequest = { refreshToken: localStorage.getItem('refresh_token') ?? '' };
    return this.http.post<void>(`${this.apiUrl}/logout`, request);
  }

  isAuthenticated(): boolean {
    //check if the user is authenticated
    return !!localStorage.getItem('access_token');
  }
}
