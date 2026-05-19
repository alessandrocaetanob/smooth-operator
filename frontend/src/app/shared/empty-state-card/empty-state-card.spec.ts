import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { EmptyStateCard } from './empty-state-card';

describe('EmptyStateCard', () => {
  it('should create with defaults', () => {
    TestBed.configureTestingModule({ imports: [EmptyStateCard] });
    const fixture = TestBed.createComponent(EmptyStateCard);
    expect(fixture.componentInstance.mascotState).toBe('idle');
  });
});
