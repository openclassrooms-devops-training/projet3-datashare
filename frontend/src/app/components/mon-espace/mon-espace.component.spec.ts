/* eslint-disable @typescript-eslint/no-explicit-any */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FileResponse } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';
import { AuthService } from '../../core/services/auth.service';
import { MonEspaceComponent } from './mon-espace.component';

const mockFileService = {
  list: jest.fn(),
  delete: jest.fn()
};

const mockAuthService = {
  logout: jest.fn()
};

function createFileResponse(overrides: Partial<FileResponse> = {}): FileResponse {
  return {
    id: '1',
    filename: 'report.pdf',
    contentType: 'application/pdf',
    sizeBytes: 100,
    downloadUrl: 'http://localhost:4200/download/abc',
    hasPassword: false,
    expiresAt: '2099-01-10T00:00:00Z',
    createdAt: '2026-09-19T00:00:00Z',
    status: 'valid',
    tags: [],
    ...overrides
  };
}

describe('MonEspaceComponent', () => {
  let component: any;
  let fixture: ComponentFixture<MonEspaceComponent>;

  beforeEach(async () => {
    mockFileService.list.mockReset();
    mockFileService.delete.mockReset();
    mockAuthService.logout.mockReset();
    mockFileService.list.mockReturnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [MonEspaceComponent],
      providers: [
        provideRouter([]),
        { provide: FileService, useValue: mockFileService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MonEspaceComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('ngOnInit loads the file list filtered on the default tab', () => {
    const files = [createFileResponse()];
    mockFileService.list.mockReturnValue(of(files));

    fixture.detectChanges();

    expect(mockFileService.list).toHaveBeenCalledWith('all');
    expect(component.files).toEqual(files);
  });

  it('loadFiles sets an error message when the request fails', () => {
    mockFileService.list.mockReturnValue(throwError(() => new Error('network error')));

    fixture.detectChanges();

    expect(component.errorMessage).toBe('Erreur lors du chargement des fichiers.');
  });

  it('setTab updates activeTab and reloads with the new status', () => {
    fixture.detectChanges();
    mockFileService.list.mockClear();

    component.setTab('expired');

    expect(component.activeTab).toBe('expired');
    expect(mockFileService.list).toHaveBeenCalledWith('expired');
  });

  describe('onDelete', () => {
    let confirmSpy: jest.SpyInstance;

    afterEach(() => {
      confirmSpy.mockRestore();
    });

    it('does nothing when the user cancels the confirmation', () => {
      confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);
      fixture.detectChanges();

      component.onDelete(createFileResponse());

      expect(mockFileService.delete).not.toHaveBeenCalled();
    });

    it('removes the file from the list once the server confirms the deletion', () => {
      confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      const file = createFileResponse({ id: 'to-delete' });
      mockFileService.list.mockReturnValue(of([file, createFileResponse({ id: 'kept' })]));
      mockFileService.delete.mockReturnValue(of(undefined));
      fixture.detectChanges();

      component.onDelete(file);

      expect(mockFileService.delete).toHaveBeenCalledWith('to-delete');
      expect(component.files.map((f: FileResponse) => f.id)).toEqual(['kept']);
    });

    it('sets an error message when the deletion fails', () => {
      confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      mockFileService.delete.mockReturnValue(throwError(() => new Error('forbidden')));
      fixture.detectChanges();

      component.onDelete(createFileResponse());

      expect(component.errorMessage).toBe('Erreur lors de la suppression du fichier.');
    });
  });

  describe('formatExpiry', () => {
    beforeEach(() => {
      jest.useFakeTimers().setSystemTime(new Date('2026-09-20T12:00:00Z'));
      fixture.detectChanges();
    });

    afterEach(() => {
      jest.useRealTimers();
    });

    it('returns "Expire dans X jours" when more than one day remains', () => {
      const file = createFileResponse({ expiresAt: '2026-09-23T12:00:00Z' });
      expect(component.formatExpiry(file)).toBe('Expire dans 3 jours');
    });

    it('returns "Expire demain" when less than a day remains', () => {
      const file = createFileResponse({ expiresAt: '2026-09-21T02:00:00Z' });
      expect(component.formatExpiry(file)).toBe('Expire demain');
    });

    it('returns "Expiré" when the expiry date is in the past', () => {
      const file = createFileResponse({ expiresAt: '2026-09-19T12:00:00Z' });
      expect(component.formatExpiry(file)).toBe('Expiré');
    });
  });

  describe('fileIconType', () => {
    beforeEach(() => fixture.detectChanges());

    it.each([
      ['image/png', 'image'],
      ['audio/mpeg', 'audio'],
      ['video/mp4', 'video'],
      ['application/pdf', 'other'],
      ['application/zip', 'other']
    ])('maps contentType %s to icon type %s', (contentType, expected) => {
      const file = createFileResponse({ contentType });
      expect(component.fileIconType(file)).toBe(expected);
    });
  });

  it('logout logs out then navigates to /login', () => {
    fixture.detectChanges();
    mockAuthService.logout.mockReturnValue(of(undefined));
    const router = TestBed.inject(Router);
    const navigateSpy = jest.spyOn(router, 'navigate');

    component.logout();

    expect(mockAuthService.logout).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });
});
