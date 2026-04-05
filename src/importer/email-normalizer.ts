import path from 'path';
import fs from 'fs';
import { Email, EmailAddress } from '../models/email';
import { Attachment } from '../models/attachment';

export interface NormalizedEmail {
  email: Email;
  attachments: RawAttachment[];
}

export interface RawAttachment {
  filename: string;
  mimeType?: string;
  data: Buffer;
}

export function parseAddressList(raw: string | null | undefined): EmailAddress[] {
  if (!raw) return [];
  // Handle semicolon or comma separated lists like "Name <email>, Name2 <email2>"
  return raw
    .split(/[;,]/)
    .map((part) => part.trim())
    .filter(Boolean)
    .map(parseAddress);
}

export function parseAddress(raw: string): EmailAddress {
  const match = raw.match(/^(.+)\s*<([^>]+)>$/);
  if (match) {
    return { name: match[1].trim(), email: match[2].trim() };
  }
  return { email: raw.trim() };
}

export function buildAttachment(
  emailId: number,
  raw: RawAttachment,
  attachmentDir: string
): Attachment {
  if (!fs.existsSync(attachmentDir)) {
    fs.mkdirSync(attachmentDir, { recursive: true });
  }

  const safeName = raw.filename.replace(/[/\\:*?"<>|]/g, '_');
  let storagePath = path.join(attachmentDir, safeName);

  // Avoid overwriting existing files
  let counter = 1;
  while (fs.existsSync(storagePath)) {
    const ext = path.extname(safeName);
    const base = path.basename(safeName, ext);
    storagePath = path.join(attachmentDir, `${base}_${counter}${ext}`);
    counter++;
  }

  fs.writeFileSync(storagePath, raw.data);

  return {
    emailId,
    filename: raw.filename,
    mimeType: raw.mimeType,
    size: raw.data.length,
    storagePath,
  };
}
