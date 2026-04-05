import { MailSearchRepository } from '../database/repository';
import { Tag } from '../models/tag';
import { Email } from '../models/email';

/**
 * Organizer provides tagging and listing operations for emails.
 */
export class Organizer {
  constructor(private repo: MailSearchRepository) {}

  /** Add one or more tags to an email. */
  tag(emailId: number, ...tagNames: string[]): void {
    for (const name of tagNames) {
      this.repo.addTagToEmail(emailId, name.trim());
    }
  }

  /** Remove a tag from an email. */
  untag(emailId: number, tagName: string): void {
    this.repo.removeTagFromEmail(emailId, tagName.trim());
  }

  /** List all tags on a specific email. */
  getTagsForEmail(emailId: number): Tag[] {
    return this.repo.getTagsForEmail(emailId);
  }

  /** List all tags in the database. */
  listTags(): Tag[] {
    return this.repo.listTags();
  }

  /** List emails with a specific tag. */
  listEmailsByTag(tagName: string, limit = 50, offset = 0): Email[] {
    return this.repo.listEmails({ tag: tagName, limit, offset });
  }
}
