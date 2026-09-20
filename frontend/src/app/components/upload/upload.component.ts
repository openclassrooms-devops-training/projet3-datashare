import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FileResponse } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';

@Component({
  selector: 'app-upload',
  imports: [ReactiveFormsModule],
  templateUrl: './upload.component.html',
  styleUrl: './upload.component.css'
})


export class UploadComponent {

  protected form = new FormGroup({
    password: new FormControl('', { nonNullable: true }),
    expiresInDays: new FormControl(7, { nonNullable: true, validators: [Validators.min(1), Validators.max(7)] }),
    tags: new FormControl('', { nonNullable: true })  // texte libre separe par virgules, a splitter au submit
  });

  protected selectedFile: File | null = null;
  protected errorMessage: string | null = null;
  protected uploadResult: FileResponse | null = null;
  protected isDraggingOver = false;
   private fileService = inject(FileService);

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDraggingOver = true;
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDraggingOver = false;
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDraggingOver = false;
    this.selectedFile = event.dataTransfer?.files?.[0] ?? null;
  }

  protected get sizeError(): boolean {
    return !!this.selectedFile && this.selectedFile.size > 1024 * 1024 * 1024;
  }

  protected formatFileSize(sizeBytes: number): string {
    const megabytes = sizeBytes / (1024 * 1024);
    return megabytes >= 1 ? `${megabytes.toFixed(1)} Mo` : `${(sizeBytes / 1024).toFixed(0)} Ko`;
  }

  protected copyLink(): void {
    if (this.uploadResult) {
      navigator.clipboard.writeText(this.uploadResult.downloadUrl);
    }
  }

  protected onSubmit(): void {
    const formData = new FormData();
    formData.append('file', this.selectedFile!);
    if (this.form.value.password) {
      formData.append('password', this.form.value.password);
    }
    formData.append('expiresInDays', String(this.form.value.expiresInDays));
    for (const tag of (this.form.value.tags ?? '').split(',').map(t => t.trim()).filter(Boolean)) {
      formData.append('tags', tag);
    }
    this.fileService.upload(formData).subscribe({
      next: (response) => {
        this.uploadResult = response;
        this.errorMessage = null;
      },
      error: (error) => {
        this.errorMessage = this.mapFileErrorCode(error.error?.code) ?? 'An error occurred during file upload.';
        this.uploadResult = null;
      }
    });
  }

  private mapFileErrorCode(code?: string): string {
  switch (code) {
    case 'MISSING_FILE': return 'Sélectionne un fichier avant de téléverser.';
    case 'FILE_TOO_LARGE': return 'La taille des fichiers est limitée à 1 Go.';
    case 'INVALID_EXPIRATION': return 'La durée d\'expiration doit être entre 1 et 7 jours.';
    case 'WEAK_FILE_PASSWORD': return 'Le mot de passe doit contenir au moins 6 caractères.';
    case 'UNSUPPORTED_FILE_TYPE': return 'Ce type de fichier n\'est pas autorisé.';
    default: return 'Une erreur est survenue, réessaie.';
  }
}



}
