import { describe, it, expect } from 'vitest';
import { pick, pickOr } from './json-utils';

describe('json-utils', () => {
  describe('pick', () => {
    it('returns undefined when raw is null or undefined', () => {
      expect(pick(null, 'a')).toBeUndefined();
      expect(pick(undefined, 'a')).toBeUndefined();
    });

    it('returns the first present key', () => {
      expect(pick({ a: 1, b: 2 }, 'a', 'b')).toBe(1);
      expect(pick({ Foo: 'hi' }, 'foo', 'Foo')).toBe('hi');
    });

    it('skips null and undefined values', () => {
      expect(pick({ a: null, b: 'x' }, 'a', 'b')).toBe('x');
      expect(pick({ a: undefined, b: 'y' }, 'a', 'b')).toBe('y');
    });

    it('returns undefined when no keys match', () => {
      expect(pick({ a: 1 }, 'x', 'y')).toBeUndefined();
    });
  });

  describe('pickOr', () => {
    it('returns fallback when raw is null/undefined', () => {
      expect(pickOr(null, 'fallback', 'a')).toBe('fallback');
    });

    it('returns the picked value when present', () => {
      expect(pickOr({ a: 'hit' }, 'fallback', 'a')).toBe('hit');
    });

    it('returns fallback when value is null', () => {
      expect(pickOr({ a: null }, 'fallback', 'a')).toBe('fallback');
    });
  });
});
