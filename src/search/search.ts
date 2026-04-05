import { MailSearchRepository } from '../database/repository';
import { EmailSearchResult } from '../models/email';

export interface SearchOptions {
  query: string;
  limit?: number;
  offset?: number;
  tag?: string;
}

/**
 * Full-text search over imported emails using SQLite FTS5.
 *
 * The query supports FTS5 syntax:
 *   - Simple terms:   "meeting"
 *   - Phrases:        '"quarterly meeting"'
 *   - Boolean ops:    "meeting AND Q4"
 *   - Prefix search:  "meet*"
 *   - Column filter:  "subject:meeting"
 */
export function search(
  repo: MailSearchRepository,
  options: SearchOptions
): EmailSearchResult[] {
  return repo.searchEmails(options.query, {
    limit: options.limit,
    offset: options.offset,
  });
}
