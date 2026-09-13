import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { RegisterComponent } from './register.component';
import { FormGroup } from '@angular/forms';
import { of, throwError } from 'rxjs';

const mockAuthService = {
  register: jest.fn()
};

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let component: RegisterComponent;
  let form : FormGroup;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let exposedComponent: any;

  beforeEach(async () => {
    mockAuthService.register.mockReset();

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    exposedComponent = component;
    form = exposedComponent.form;
    fixture.detectChanges();
  });

  it('exposes the reactive form', () => {
    
    expect(form).toBeInstanceOf(FormGroup);
  });

  it('should display validation errors for an empty form', () => {
    //act
    exposedComponent.onSubmit();
    //assert
    expect(form.invalid).toBe(true);
    expect(mockAuthService.register).not.toHaveBeenCalled();
  });

  it('should mark the form invalid when passwords do not match', () => {
    // Arrange : remplir form.setValue avec email/password valides mais passwordConfirmation différent
    form.setValue({
      email: 'test@example.com',
      password: 'Password1!',
      passwordConfirmation: 'Password2!'
    });
    // Assert : vérifier form.errors (le validator est sur le groupe)
    expect(form.errors).toBeTruthy();
    expect(form.errors?.['passwordsMismatch']).toBe(true);
  });

  it('should register and redirect to login on success', () => {
    // Arrange : mockAuthService.register.mockReturnValue(of(...)) + form.setValue(...) valide
    mockAuthService.register.mockReturnValue(of({ id: '1',
                                                 email: 'test@example.com',
                                                 createdAt: '2026-09-13T00:00:00Z' }));
    form.setValue({
      email: 'test@example.com',
      password: 'Password1!',
      passwordConfirmation: 'Password1!'
    });
    const navigateSpy = jest.spyOn(TestBed.inject(Router), 'navigate');
    //act
    exposedComponent.onSubmit();
    // Assert : mockAuthService.register appelé avec les bonnes valeurs + navigation vers '/login'
    expect(mockAuthService.register).toHaveBeenCalledWith({
      email: 'test@example.com',
      password: 'Password1!',
      passwordConfirmation: 'Password1!'
    });
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });

  it('should display an error when the email is already used', () => {
    // Arrange : mockAuthService.register.mockReturnValue(throwError(() => ({ status: 400 })))
    mockAuthService.register.mockReturnValue(throwError(() => ({ status: 400 })));
    form.setValue({
      email: 'test@example.com',
      password: 'Password1!',
      passwordConfirmation: 'Password1!'
    });
    // Act : component.onSubmit()
    component.onSubmit();
    // Assert : errorMessage === 'Cet email est déjà utilisé.'
    expect(exposedComponent.errorMessage).toBe('Cet email est déjà utilisé.');
  });

  it('should display a generic error for other failures', () => {
    // Arrange : throwError avec un status différent de 400 (ex. 500)
    mockAuthService.register.mockReturnValue(throwError(() => ({ status: 500 })));
    form.setValue({
      email: 'test@example.com',
      password: 'Password1!',
      passwordConfirmation: 'Password1!'
    });
    //act
    exposedComponent.onSubmit();
    // Assert : message d'erreur générique
    expect(exposedComponent.errorMessage).toBe('Une erreur est survenue. Veuillez réessayer.');
  });
});



