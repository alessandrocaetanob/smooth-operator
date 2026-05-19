import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { SectionCard } from './section-card';

describe('SectionCard', () => {
  it('should create with defaults', () => {
    TestBed.configureTestingModule({ imports: [SectionCard] });
    const fixture = TestBed.createComponent(SectionCard);
    expect(fixture.componentInstance.noPadding).toBe(false);
  });
});
