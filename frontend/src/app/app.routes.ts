import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

// app.routes.ts
export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', canActivate: [guestGuard], loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) },
  { path: 'upload', canActivate: [authGuard], loadComponent: () => import('./components/upload/upload.component').then(m => m.UploadComponent) },
];
