export interface Attachment {
  id?: number;
  emailId: number;
  filename: string;
  mimeType?: string;
  size: number;
  storagePath?: string;
  data?: Buffer;
}

export interface AttachmentRow {
  id: number;
  email_id: number;
  filename: string;
  mime_type: string | null;
  size: number;
  storage_path: string | null;
}

export function rowToAttachment(row: AttachmentRow): Attachment {
  return {
    id: row.id,
    emailId: row.email_id,
    filename: row.filename,
    mimeType: row.mime_type ?? undefined,
    size: row.size,
    storagePath: row.storage_path ?? undefined,
  };
}
