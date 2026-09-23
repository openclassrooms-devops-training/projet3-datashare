import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

// app.routes.ts
export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', canActivate: [guestGuard], loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) },
  { path: 'upload', canActivate: [authGuard], loadComponent: () => import('./components/upload/upload.component').then(m => m.UploadComponent) },
  { path: 'mon-espace', canActivate: [authGuard], data: { hideHeader: true }, loadComponent: () => import('./components/mon-espace/mon-espace.component').then(m => m.MonEspaceComponent) },
  { path: 'download/:token', loadComponent: () => import('./components/download/download.component').then(m => m.DownloadComponent) },
];
