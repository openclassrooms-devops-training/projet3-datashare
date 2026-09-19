import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.css'
})
export class HeaderComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  protected get isAuthenticated(): boolean {
    return this.authService.isAuthenticated();
  }

  protected get initials(): string {
    const email = this.authService.getCurrentUserEmail();
    return (email ?? '').slice(0, 2).toUpperCase();
  }

  protected logout(): void {
    this.authService.logout().subscribe({
      complete: () => this.afterLogout(),
      error: () => this.afterLogout()
    });
  }

  private afterLogout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.router.navigate(['/login']);
  }
}
