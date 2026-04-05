export { MailSearchRepository, DEFAULT_DB_PATH } from './database/repository';
export { importPst } from './importer/pst-importer';
export { importMsg } from './importer/msg-importer';
export { search } from './search/search';
export { Organizer } from './organizer/organizer';
export type { Email, EmailAddress, EmailSearchResult } from './models/email';
export type { Attachment } from './models/attachment';
export type { Tag } from './models/tag';
