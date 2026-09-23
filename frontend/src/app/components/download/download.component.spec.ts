/* eslint-disable @typescript-eslint/no-explicit-any */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FileMetadataResponse } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';
import { DownloadComponent } from './download.component';

const mockFileService = {
  getMetadata: jest.fn(),
  download: jest.fn()
};

const mockActivatedRoute = {
  snapshot: { paramMap: convertToParamMap({ token: 'abc123' }) }
};

function createMetadata(overrides: Partial<FileMetadataResponse> = {}): FileMetadataResponse {
  return {
    filename: 'report.pdf',
    contentType: 'application/pdf',
    sizeBytes: 100,
    expiresAt: '2099-01-10T00:00:00Z',
    requiresPassword: false,
    ...overrides
  };
}

describe('DownloadComponent', () => {
  let component: any;
  let fixture: ComponentFixture<DownloadComponent>;

  beforeEach(async () => {
    mockFileService.getMetadata.mockReset();
    mockFileService.download.mockReset();
    mockFileService.getMetadata.mockReturnValue(of(createMetadata()));

    await TestBed.configureTestingModule({
      imports: [DownloadComponent],
      providers: [
        { provide: FileService, useValue: mockFileService },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DownloadComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('ngOnInit reads the token from the route and fetches its metadata', () => {
    fixture.detectChanges();

    expect(component.token).toBe('abc123');
    expect(mockFileService.getMetadata).toHaveBeenCalledWith('abc123');
  });

  it('ngOnInit stores the metadata on success', () => {
    const metadata = createMetadata({ filename: 'photo.jpg' });
    mockFileService.getMetadata.mockReturnValue(of(metadata));

    fixture.detectChanges();

    expect(component.metadata).toEqual(metadata);
    expect(component.notFound).toBe(false);
  });

  it('ngOnInit sets notFound when the token is invalid or expired (404)', () => {
    mockFileService.getMetadata.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 404 }))
    );

    fixture.detectChanges();

    expect(component.notFound).toBe(true);
    expect(component.metadata).toBeNull();
  });

  it('ngOnInit sets a generic error message for a non-404 failure', () => {
    // Regression : avant, ce cas ne renseignait ni notFound ni metadata utilement
    // affichable - le template n'avait pas de branche pour errorMessage seul.
    mockFileService.getMetadata.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 }))
    );

    fixture.detectChanges();

    expect(component.notFound).toBe(false);
    expect(component.errorMessage).toBe('Erreur lors de la récupération des métadonnées du fichier.');
  });

  describe('formatFileSize', () => {
    beforeEach(() => fixture.detectChanges());

    it('formats sizes under 1 Mo in Ko', () => {
      expect(component.formatFileSize(2048)).toBe('2 Ko');
    });

    it('formats sizes of 1 Mo or more in Mo', () => {
      expect(component.formatFileSize(2.6 * 1024 * 1024)).toBe('2.6 Mo');
    });
  });

  describe('onDownload', () => {
    // JSDOM (l'environnement des tests Jest) n'implemente pas URL.createObjectURL/
    // revokeObjectURL - jest.spyOn exige que la propriete existe deja sur l'objet
    // pour pouvoir l'espionner, donc on l'assigne directement plutot que de la "spy".
    let createObjectURLMock: jest.Mock;
    let revokeObjectURLMock: jest.Mock;
    let clickSpy: jest.SpyInstance;

    beforeEach(() => {
      fixture.detectChanges();
      createObjectURLMock = jest.fn().mockReturnValue('blob:mock-url');
      revokeObjectURLMock = jest.fn();
      (URL as any).createObjectURL = createObjectURLMock;
      (URL as any).revokeObjectURL = revokeObjectURLMock;
      clickSpy = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    });

    afterEach(() => {
      delete (URL as any).createObjectURL;
      delete (URL as any).revokeObjectURL;
      clickSpy.mockRestore();
    });

    it('triggers a browser download of the received blob on success', () => {
      const blob = new Blob(['contenu']);
      mockFileService.download.mockReturnValue(of(blob));

      component.onDownload();

      // form.controls.password est nonNullable : sa valeur par defaut est '' (pas
      // undefined) tant que le champ n'est pas affiche/rempli - sans consequence
      // cote backend, qui ignore le mot de passe quand le fichier n'en requiert pas.
      expect(mockFileService.download).toHaveBeenCalledWith('abc123', '');
      expect(createObjectURLMock).toHaveBeenCalledWith(blob);
      expect(clickSpy).toHaveBeenCalled();
      expect(revokeObjectURLMock).toHaveBeenCalledWith('blob:mock-url');
    });

    it('sets a specific error message on a wrong password (401)', () => {
      mockFileService.download.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 401 }))
      );

      component.onDownload();

      expect(component.errorMessage).toBe('Mot de passe incorrect.');
      expect(clickSpy).not.toHaveBeenCalled();
    });

    it('sets a generic error message on any other failure', () => {
      mockFileService.download.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 500 }))
      );

      component.onDownload();

      expect(component.errorMessage).toBe('Erreur lors du téléchargement du fichier.');
    });
  });
});
