import { describe, it, expect } from 'vitest';
import * as anim from './animations';

describe('animations module', () => {
  it('exports trigger metadata objects with names', () => {
    expect(Object.keys(anim).length).toBeGreaterThan(0);
    for (const key of Object.keys(anim)) {
      const trigger = (anim as Record<string, unknown>)[key];
      expect(trigger).toBeTruthy();
    }
  });
});
