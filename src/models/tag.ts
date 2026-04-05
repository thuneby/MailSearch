export interface Tag {
  id?: number;
  name: string;
  createdAt: Date;
}

export interface EmailTag {
  emailId: number;
  tagId: number;
}

export interface TagRow {
  id: number;
  name: string;
  created_at: string;
}

export function rowToTag(row: TagRow): Tag {
  return {
    id: row.id,
    name: row.name,
    createdAt: new Date(row.created_at),
  };
}
