import { Component, DestroyRef, inject } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';


@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
 
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  protected errorMessage: string | null = null;
  protected form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  onSubmit() {
    if (this.form.valid) {
      this.authService.login(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))  
      .subscribe({
        next: (token) => {
          localStorage.setItem('access_token', token.accessToken);
          localStorage.setItem('refresh_token', token.refreshToken);
          localStorage.setItem('refresh_token', token.refreshToken);
          //navigate to upload page
          this.router.navigate(['/upload']);
        },
        error: (err :  HttpErrorResponse) => {
          this.errorMessage = err.status === 401
            ? 'Identifiants incorrects.'
            : 'Une erreur est survenue. Veuillez réessayer.';
        }
      });
    }
}
}
