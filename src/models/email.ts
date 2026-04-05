export interface EmailAddress {
  name?: string;
  email: string;
}

export interface Email {
  id?: number;
  messageId?: string;
  subject: string;
  from: EmailAddress;
  to: EmailAddress[];
  cc: EmailAddress[];
  bcc: EmailAddress[];
  date?: Date;
  bodyText?: string;
  bodyHtml?: string;
  hasAttachments: boolean;
  attachmentCount: number;
  sourceFile: string;
  importedAt: Date;
  folderId?: number;
  folderPath?: string;
}

export interface EmailRow {
  id: number;
  message_id: string | null;
  subject: string;
  from_name: string | null;
  from_email: string;
  to_json: string;
  cc_json: string;
  bcc_json: string;
  date: string | null;
  body_text: string | null;
  body_html: string | null;
  has_attachments: number;
  attachment_count: number;
  source_file: string;
  imported_at: string;
  folder_id: number | null;
  folder_path: string | null;
}

export interface EmailSearchResult extends Email {
  snippet?: string;
  rank?: number;
}

export function rowToEmail(row: EmailRow): Email {
  return {
    id: row.id,
    messageId: row.message_id ?? undefined,
    subject: row.subject,
    from: { name: row.from_name ?? undefined, email: row.from_email },
    to: JSON.parse(row.to_json) as EmailAddress[],
    cc: JSON.parse(row.cc_json) as EmailAddress[],
    bcc: JSON.parse(row.bcc_json) as EmailAddress[],
    date: row.date ? new Date(row.date) : undefined,
    bodyText: row.body_text ?? undefined,
    bodyHtml: row.body_html ?? undefined,
    hasAttachments: row.has_attachments === 1,
    attachmentCount: row.attachment_count,
    sourceFile: row.source_file,
    importedAt: new Date(row.imported_at),
    folderId: row.folder_id ?? undefined,
    folderPath: row.folder_path ?? undefined,
  };
}
