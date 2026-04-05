import { parseAddress, parseAddressList } from '../src/importer/email-normalizer';

describe('email-normalizer', () => {
  describe('parseAddress', () => {
    it('parses "Name <email>" format', () => {
      const result = parseAddress('Alice Smith <alice@example.com>');
      expect(result).toEqual({ name: 'Alice Smith', email: 'alice@example.com' });
    });

    it('parses plain email address', () => {
      const result = parseAddress('bob@example.com');
      expect(result).toEqual({ email: 'bob@example.com' });
    });

    it('trims surrounding whitespace', () => {
      const result = parseAddress('  carol@example.com  ');
      expect(result).toEqual({ email: 'carol@example.com' });
    });

    it('handles email with angle brackets and extra spaces', () => {
      const result = parseAddress('Dave  <dave@example.com>');
      expect(result).toEqual({ name: 'Dave', email: 'dave@example.com' });
    });
  });

  describe('parseAddressList', () => {
    it('returns empty array for null input', () => {
      expect(parseAddressList(null)).toEqual([]);
    });

    it('returns empty array for empty string', () => {
      expect(parseAddressList('')).toEqual([]);
    });

    it('parses comma-separated list', () => {
      const result = parseAddressList('alice@example.com, bob@example.com');
      expect(result).toHaveLength(2);
      expect(result[0].email).toBe('alice@example.com');
      expect(result[1].email).toBe('bob@example.com');
    });

    it('parses semicolon-separated list', () => {
      const result = parseAddressList('alice@example.com; bob@example.com');
      expect(result).toHaveLength(2);
    });

    it('parses named addresses in a list', () => {
      const result = parseAddressList('Alice <alice@example.com>, Bob <bob@example.com>');
      expect(result[0]).toEqual({ name: 'Alice', email: 'alice@example.com' });
      expect(result[1]).toEqual({ name: 'Bob', email: 'bob@example.com' });
    });

    it('filters out empty parts', () => {
      const result = parseAddressList('alice@example.com,,bob@example.com');
      expect(result).toHaveLength(2);
    });
  });
});
