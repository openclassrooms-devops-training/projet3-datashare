import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { FileMetadataResponse } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';

@Component({
  selector: 'app-download',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './download.component.html',
  styleUrl: './download.component.css'
})
export class DownloadComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private fileService = inject(FileService);

  protected token = '';
  protected metadata: FileMetadataResponse | null = null;
  protected notFound = false;
  protected errorMessage: string | null = null;

  protected form = new FormGroup({
    password: new FormControl('', { nonNullable: true })
  });

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') || '' ;
    this.fileService.getMetadata(this.token).subscribe({
      next: (metadata) => {
        this.metadata = metadata;
        this.notFound = false;
      },
      error: (err) => {
        if (err.status === 404) {
          this.notFound = true;
        } else {
          this.errorMessage = 'Erreur lors de la récupération des métadonnées du fichier.';
        }
      }
    });
  }

  protected formatFileSize(sizeBytes: number): string {
    // Meme convention que upload.component.ts : unites francaises (Mo/Ko), pas
    // les unites anglaises par defaut que l'autocompletion a generees - la
    // maquette figma-telechargement.md montre explicitement "2,6 Mo".
    const megabytes = sizeBytes / (1024 * 1024);
    return megabytes >= 1 ? `${megabytes.toFixed(1)} Mo` : `${(sizeBytes / 1024).toFixed(0)} Ko`;
  }

  protected onDownload(): void {
    this.fileService.download(this.token, this.form.value.password).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.metadata?.filename || 'download';
        a.click();
        URL.revokeObjectURL(url);
        this.errorMessage = null;
      },
      error: (err) => {
        if (err.status === 401) {
          this.errorMessage = 'Mot de passe incorrect.';
        } else {
          this.errorMessage = 'Erreur lors du téléchargement du fichier.';
        }
      }
    });
  }
}
