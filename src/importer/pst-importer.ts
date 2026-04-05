import path from 'path';
import { PSTFile, PSTFolder, PSTMessage, PSTAttachment } from 'pst-extractor';
import { PSTNodeInputStream } from 'pst-extractor/dist/PSTNodeInputStream.class';
import { Email } from '../models/email';
import { MailSearchRepository } from '../database/repository';
import { buildAttachment, parseAddressList, RawAttachment } from './email-normalizer';

export interface ImportResult {
  imported: number;
  skipped: number;
  errors: number;
}

/**
 * Import a PST/OST file into the MailSearch database.
 * Attachments are extracted to `attachmentDir`.
 */
export async function importPst(
  pstFilePath: string,
  repo: MailSearchRepository,
  attachmentDir: string,
  onProgress?: (imported: number) => void
): Promise<ImportResult> {
  const result: ImportResult = { imported: 0, skipped: 0, errors: 0 };

  const pstFile = new PSTFile(pstFilePath);
  const rootFolder = pstFile.getRootFolder();
  await processFolder(rootFolder, '', repo, pstFilePath, attachmentDir, result, onProgress);

  return result;
}

async function processFolder(
  folder: PSTFolder,
  folderPath: string,
  repo: MailSearchRepository,
  sourceFile: string,
  attachmentDir: string,
  result: ImportResult,
  onProgress?: (imported: number) => void
): Promise<void> {
  const currentPath = folderPath
    ? `${folderPath}/${folder.displayName}`
    : folder.displayName;

  if (folder.contentCount > 0) {
    let message = folder.getNextChild();
    while (message !== null) {
      if (message instanceof PSTMessage) {
        try {
          processMessage(message, currentPath, repo, sourceFile, attachmentDir, result);
          if (onProgress) {
            onProgress(result.imported);
          }
        } catch (_err) {
          result.errors++;
        }
      }
      message = folder.getNextChild();
    }
  }

  if (folder.hasSubfolders) {
    let subfolder = folder.getNextChild();
    while (subfolder !== null) {
      if (subfolder instanceof PSTFolder) {
        await processFolder(
          subfolder,
          currentPath,
          repo,
          sourceFile,
          attachmentDir,
          result,
          onProgress
        );
      }
      subfolder = folder.getNextChild();
    }
  }
}

function processMessage(
  message: PSTMessage,
  folderPath: string,
  repo: MailSearchRepository,
  sourceFile: string,
  attachmentDir: string,
  result: ImportResult
): void {
  const recipients = parseAddressList(message.displayTo);
  const ccRecipients = parseAddressList(message.displayCC);
  const bccRecipients = parseAddressList(message.displayBCC);

  const email: Email = {
    messageId: message.internetMessageId || undefined,
    subject: message.subject || '(no subject)',
    from: {
      name: message.senderName || undefined,
      email: message.senderEmailAddress || '',
    },
    to: recipients,
    cc: ccRecipients,
    bcc: bccRecipients,
    date: message.messageDeliveryTime || undefined,
    bodyText: message.body || undefined,
    bodyHtml: message.bodyHTML || undefined,
    hasAttachments: message.hasAttachments,
    attachmentCount: message.numberOfAttachments,
    sourceFile: path.resolve(sourceFile),
    importedAt: new Date(),
    folderPath,
  };

  const emailId = repo.insertEmail(email);
  if (emailId === 0) {
    // INSERT OR IGNORE returned 0 – duplicate message-id
    result.skipped++;
    return;
  }

  // Extract attachments
  if (message.hasAttachments) {
    for (let i = 0; i < message.numberOfAttachments; i++) {
      const attachment = message.getAttachment(i);
      if (!attachment || !attachment.filename) continue;

      const rawData = readPstAttachmentData(attachment);
      if (!rawData) continue;

      const raw: RawAttachment = {
        filename: attachment.longFilename || attachment.filename,
        mimeType: attachment.mimeTag || undefined,
        data: rawData,
      };

      try {
        const attachmentRecord = buildAttachment(emailId, raw, attachmentDir);
        repo.insertAttachment(attachmentRecord);
      } catch (_err) {
        // Skip attachments that cannot be saved
      }
    }
  }

  result.imported++;
}

function readPstAttachmentData(attachment: PSTAttachment): Buffer | null {
  const stream: PSTNodeInputStream | null = attachment.fileInputStream;
  if (!stream) return null;

  const size = attachment.filesize;
  const buffer = Buffer.alloc(size);
  stream.readCompletely(buffer);
  return buffer;
}
