import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LoginComponent } from './login.component';
import { provideHttpClient } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { of, throwError } from 'rxjs';

const mockAuthService = {
  login: jest.fn()
};

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let form: FormGroup;

  beforeEach(async () => {
    mockAuthService.login.mockReset();
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    // Cast `as any` pour lire le champ `protected form` du composant depuis le test.
    // Autre méthode possible, plus typée : `as unknown as { form: FormGroup }` (cast via
    // unknown vers une interface minimale) au lieu de `any` - plus verbeux mais type-safe.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    form = (component as any).form;
    fixture.detectChanges();
  });

  it('exposes the reactive form', () => {
    // Assert
    expect(form).toBeInstanceOf(FormGroup);
  });

  it('should display validation errors for an empty form', () => {
    // Arrange: form is left empty (default state after creation)

    // Act
    component.onSubmit();

    // Assert
    expect(form.invalid).toBe(true);
    expect(mockAuthService.login).not.toHaveBeenCalled();
  });

  it('should store the token and redirect after a successful login', () => {
    // Arrange
    mockAuthService.login.mockReturnValue(of({ accessToken: 'jwt-token', refreshToken: 'refresh-token' }));
    const navigateSpy = jest.spyOn(TestBed.inject(Router), 'navigate');
    form.setValue({ email: 'alice@example.com', password: 'password' });

    // Act
    component.onSubmit();

    // Assert
    expect(mockAuthService.login).toHaveBeenCalledWith({ email: 'alice@example.com', password: 'password' });
    expect(localStorage.getItem('access_token')).toBe('jwt-token');
    expect(localStorage.getItem('refresh_token')).toBe('refresh-token');
    expect(navigateSpy).toHaveBeenCalledWith(['/upload']);
  });

  it('should display an error after an unauthorized login', () => {
    // Arrange
    mockAuthService.login.mockReturnValue(throwError(() => ({ status: 401 })));
    form.setValue({ email: 'alice@example.com', password: 'wrong-password' });

    // Act
    component.onSubmit();

    // Assert
    // Meme cast que pour `form` plus haut (acces a un membre protected) - meme alternative possible.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    expect((component as any).errorMessage).toBe('Identifiants incorrects.');
    expect(localStorage.getItem('access_token')).toBeNull();
  });

  it('should display a generic error for other failures', () => {
    // Arrange
    mockAuthService.login.mockReturnValue(throwError(() => ({ status: 500 })));
    form.setValue({ email: 'alice@example.com', password: 'password' });

    // Act
    component.onSubmit();

    // Assert
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    expect((component as any).errorMessage).toBe('Une erreur est survenue. Veuillez réessayer.');
    expect(localStorage.getItem('access_token')).toBeNull();
  });
});
