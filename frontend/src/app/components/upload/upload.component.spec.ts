/* eslint-disable @typescript-eslint/no-explicit-any */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { FileResponse } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';
import { UploadComponent } from './upload.component';

const mockFileService = {
  upload: jest.fn()
};

function createFile(name: string, sizeBytes: number, type = 'application/pdf'): File {
  const file = new File(['x'], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

function createFileResponse(overrides: Partial<FileResponse> = {}): FileResponse {
  return {
    id: '1',
    filename: 'report.pdf',
    contentType: 'application/pdf',
    sizeBytes: 100,
    downloadUrl: 'http://localhost:4200/download/abc',
    hasPassword: false,
    expiresAt: '2026-09-26T00:00:00Z',
    createdAt: '2026-09-19T00:00:00Z',
    status: 'valid',
    tags: [],
    ...overrides
  };
}

describe('UploadComponent', () => {
  let component: any;
  let fixture: ComponentFixture<UploadComponent>;

  beforeEach(async () => {
    mockFileService.upload.mockReset();

    await TestBed.configureTestingModule({
      imports: [UploadComponent],
      providers: [
        { provide: FileService, useValue: mockFileService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UploadComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('onFileSelected sets the selected file from the input', () => {
    const file = createFile('photo.jpg', 1000);
    const input = document.createElement('input');
    input.type = 'file';
    Object.defineProperty(input, 'files', { value: [file] });

    component.onFileSelected({ target: input } as unknown as Event);

    expect(component.selectedFile).toBe(file);
  });

  it('onFileSelected sets selectedFile to null when no file is chosen (dialog cancelled)', () => {
    const input = document.createElement('input');
    input.type = 'file';
    Object.defineProperty(input, 'files', { value: [] });

    component.onFileSelected({ target: input } as unknown as Event);

    expect(component.selectedFile).toBeNull();
  });

  it('onDragOver sets isDraggingOver to true', () => {
    const event = { preventDefault: jest.fn() } as unknown as DragEvent;

    component.onDragOver(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(component.isDraggingOver).toBe(true);
  });

  it('onDragLeave sets isDraggingOver to false', () => {
    component.isDraggingOver = true;
    const event = { preventDefault: jest.fn() } as unknown as DragEvent;

    component.onDragLeave(event);

    expect(component.isDraggingOver).toBe(false);
  });

  it('onDrop sets the selected file and resets isDraggingOver', () => {
    const file = createFile('dropped.pdf', 500);
    const event = {
      preventDefault: jest.fn(),
      dataTransfer: { files: [file] }
    } as unknown as DragEvent;
    component.isDraggingOver = true;

    component.onDrop(event);

    expect(component.selectedFile).toBe(file);
    expect(component.isDraggingOver).toBe(false);
  });

  it('onDrop sets selectedFile to null when the drop has no files', () => {
    const event = {
      preventDefault: jest.fn(),
      dataTransfer: { files: [] }
    } as unknown as DragEvent;

    component.onDrop(event);

    expect(component.selectedFile).toBeNull();
  });

  it('sizeError is false when no file or file under 1 Go', () => {
    expect(component.sizeError).toBe(false);

    component.selectedFile = createFile('small.pdf', 1024);
    expect(component.sizeError).toBe(false);
  });

  it('sizeError is true when the file exceeds 1 Go', () => {
    component.selectedFile = createFile('big.pdf', 1024 * 1024 * 1024 + 1);

    expect(component.sizeError).toBe(true);
  });

  it('formatFileSize formats bytes in Ko below 1 Mo, and in Mo above', () => {
    expect(component.formatFileSize(500)).toBe('0 Ko');
    expect(component.formatFileSize(2 * 1024 * 1024)).toBe('2.0 Mo');
  });

  it('copyLink copies the download URL to the clipboard', () => {
    const writeText = jest.fn();
    Object.assign(navigator, { clipboard: { writeText } });
    component.uploadResult = createFileResponse({ downloadUrl: 'http://localhost:4200/download/xyz' });

    component.copyLink();

    expect(writeText).toHaveBeenCalledWith('http://localhost:4200/download/xyz');
  });

  it('copyLink does nothing when there is no upload result', () => {
    const writeText = jest.fn();
    Object.assign(navigator, { clipboard: { writeText } });

    component.copyLink();

    expect(writeText).not.toHaveBeenCalled();
  });

  describe('onSubmit', () => {
    beforeEach(() => {
      component.selectedFile = createFile('report.pdf', 1000);
    });

    it('sends a FormData with the file, expiration and tags', () => {
      mockFileService.upload.mockReturnValue(of(createFileResponse()));
      component.form.setValue({ password: '', expiresInDays: 5, tags: 'facture, 2026' });

      component.onSubmit();

      const sentFormData = mockFileService.upload.mock.calls[0][0] as FormData;
      expect(sentFormData.get('file')).toBe(component.selectedFile);
      expect(sentFormData.get('expiresInDays')).toBe('5');
      expect(sentFormData.getAll('tags')).toEqual(['facture', '2026']);
      expect(sentFormData.has('password')).toBe(false);
    });

    it('includes the password in the FormData when provided', () => {
      mockFileService.upload.mockReturnValue(of(createFileResponse()));
      component.form.setValue({ password: 'secret123', expiresInDays: 7, tags: '' });

      component.onSubmit();

      const sentFormData = mockFileService.upload.mock.calls[0][0] as FormData;
      expect(sentFormData.get('password')).toBe('secret123');
    });

    it('on success, stores the result and clears the error message', () => {
      const response = createFileResponse();
      mockFileService.upload.mockReturnValue(of(response));
      component.errorMessage = 'ancienne erreur';

      component.onSubmit();

      expect(component.uploadResult).toBe(response);
      expect(component.errorMessage).toBeNull();
    });

    it.each([
      ['MISSING_FILE', 'Sélectionne un fichier avant de téléverser.'],
      ['FILE_TOO_LARGE', 'La taille des fichiers est limitée à 1 Go.'],
      ['INVALID_EXPIRATION', 'La durée d\'expiration doit être entre 1 et 7 jours.'],
      ['WEAK_FILE_PASSWORD', 'Le mot de passe doit contenir au moins 6 caractères.'],
      ['UNSUPPORTED_FILE_TYPE', 'Ce type de fichier n\'est pas autorisé.'],
      ['SOMETHING_ELSE', 'Une erreur est survenue, réessaie.']
    ])('on error code %s, displays the matching message', (code, expectedMessage) => {
      const httpError = new HttpErrorResponse({ status: 400, error: { code, message: 'ignored' } });
      mockFileService.upload.mockReturnValue(throwError(() => httpError));

      component.onSubmit();

      expect(component.errorMessage).toBe(expectedMessage);
      expect(component.uploadResult).toBeNull();
    });
  });
});
