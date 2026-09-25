import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Bloque l'acces aux pages reservees aux visiteurs non connectes (login/register) :
// un utilisateur deja connecte n'a rien a faire sur ces ecrans.
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return !authService.isAuthenticated() ? true : router.parseUrl('/upload');
};
