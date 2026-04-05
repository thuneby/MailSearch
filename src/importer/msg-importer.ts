import path from 'path';
import fs from 'fs';
import MsgReader, { FieldsData } from '@kenjiuno/msgreader';
import { Email, EmailAddress } from '../models/email';
import { MailSearchRepository } from '../database/repository';
import { buildAttachment, parseAddress, RawAttachment } from './email-normalizer';

export interface ImportResult {
  imported: number;
  skipped: number;
  errors: number;
}

/**
 * Import a single .msg file into the MailSearch database.
 * Attachments are extracted to `attachmentDir`.
 */
export function importMsg(
  msgFilePath: string,
  repo: MailSearchRepository,
  attachmentDir: string
): ImportResult {
  const result: ImportResult = { imported: 0, skipped: 0, errors: 0 };

  try {
    const fileBuffer = fs.readFileSync(msgFilePath);
    // MsgReader requires ArrayBuffer or DataView; convert Node Buffer accordingly
    const arrayBuffer = fileBuffer.buffer.slice(
      fileBuffer.byteOffset,
      fileBuffer.byteOffset + fileBuffer.byteLength
    ) as ArrayBuffer;
    const reader = new MsgReader(arrayBuffer);
    const msg = reader.getFileData();

    if (!msg) {
      result.errors++;
      return result;
    }

    const fromAddr: EmailAddress = parseAddress(msg.senderEmail || '');
    if (!fromAddr.name && msg.senderName) {
      fromAddr.name = msg.senderName;
    }

    const toAddresses = parseRecipients(msg.recipients, 'to');
    const ccAddresses = parseRecipients(msg.recipients, 'cc');
    const bccAddresses = parseRecipients(msg.recipients, 'bcc');

    const attachmentCount = msg.attachments ? msg.attachments.length : 0;

    const email: Email = {
      subject: msg.subject || '(no subject)',
      from: fromAddr,
      to: toAddresses,
      cc: ccAddresses,
      bcc: bccAddresses,
      date: msg.creationTime ? new Date(msg.creationTime) : undefined,
      bodyText: msg.body || undefined,
      bodyHtml: msg.bodyHtml || undefined,
      hasAttachments: attachmentCount > 0,
      attachmentCount,
      sourceFile: path.resolve(msgFilePath),
      importedAt: new Date(),
    };

    const emailId = repo.insertEmail(email);
    if (emailId === 0) {
      result.skipped++;
      return result;
    }

    // Extract attachments using reader.getAttachment()
    if (msg.attachments) {
      for (const attRef of msg.attachments) {
        if (!attRef.fileName) continue;
        try {
          const attData = reader.getAttachment(attRef);
          if (!attData || !attData.content) continue;

          const raw: RawAttachment = {
            filename: attData.fileName,
            data: Buffer.from(attData.content),
          };

          const attachmentRecord = buildAttachment(emailId, raw, attachmentDir);
          repo.insertAttachment(attachmentRecord);
        } catch {
          // Skip unreadable attachments
        }
      }
    }

    result.imported++;
  } catch (err) {
    result.errors++;
  }

  return result;
}

function parseRecipients(
  recipients: FieldsData[] | undefined,
  type: 'to' | 'cc' | 'bcc'
): EmailAddress[] {
  if (!recipients) return [];

  return recipients
    .filter((r) => r.recipType === type)
    .map((r) => ({
      name: r.name || undefined,
      email: r.email || r.smtpAddress || r.name || '',
    }));
}
