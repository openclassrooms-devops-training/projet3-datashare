import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const refreshInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Ne jamais tenter de refresh sur les appels d'auth eux-memes (login/register/refresh/logout)
  // sinon un 401 de login (mauvais mot de passe) declencherait un refresh a tort, et un 401
  // sur /refresh lui-meme bouclerait indefiniment.
  const isAuthCall = request.url.includes('/api/auth/');

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthCall) {
        return authService.refresh().pipe(
          switchMap((tokens) => {
            localStorage.setItem('access_token', tokens.accessToken);
            localStorage.setItem('refresh_token', tokens.refreshToken);

            const retriedRequest = request.clone({
              setHeaders: { Authorization: `Bearer ${tokens.accessToken}` }
            });
            return next(retriedRequest);
          }),
          catchError((refreshError: unknown) => {
            localStorage.removeItem('access_token');
            localStorage.removeItem('refresh_token');
            router.navigate(['/login']);
            return throwError(() => refreshError);
          })
        );
      }

      return throwError(() => error);
    })
  );
};
