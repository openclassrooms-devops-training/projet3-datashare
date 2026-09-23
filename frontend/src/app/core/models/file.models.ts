export interface FileResponse {
  id: string;
  filename: string;
  contentType: string;
  sizeBytes: number;
  downloadUrl: string;
  hasPassword: boolean;
  expiresAt: string;
  createdAt: string;
  status: string;      // "valid" | "expired"
  tags: string[];
}

export interface FileUploadRequest {
  file: File;
  password?: string;
  expiresInDays: number;
  tags: string[];
}

export type FileErrorCode =
  | 'MISSING_FILE'
  | 'FILE_TOO_LARGE'
  | 'INVALID_EXPIRATION'
  | 'WEAK_FILE_PASSWORD'
  | 'UNSUPPORTED_FILE_TYPE';

export interface FileMetadataResponse {
  filename: string;
  contentType: string;
  sizeBytes: number;
  expiresAt: string;
  requiresPassword: boolean;
}

export type FileStatusFilter = 'all' | 'active' | 'expired';