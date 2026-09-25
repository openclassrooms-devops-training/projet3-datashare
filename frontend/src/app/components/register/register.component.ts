import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { Router, RouterLink } from '@angular/router';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';

function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  return group.get('password')?.value === group.get('passwordConfirmation')?.value
    ? null
    : { passwordsMismatch: true };
}

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent {

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected errorMessage: string | null = null;
  protected form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, 
                                                                    Validators.minLength(8),
                                                                    Validators.pattern(/[!@#$%^&*(),.?":{}|<>_-]/)
    ] }),
    passwordConfirmation: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  }, { validators: passwordsMatchValidator });

  onSubmit() {
    if (this.form.valid) {
      this.authService.register(this.form.getRawValue()).subscribe({
        next: () => this.router.navigate(['/login']),
        error: (err: HttpErrorResponse) => {
           this.errorMessage = err.status === 400
            ? 'Cet email est déjà utilisé.'
            : 'Une erreur est survenue. Veuillez réessayer.'; }
      });
    }
  }

}
