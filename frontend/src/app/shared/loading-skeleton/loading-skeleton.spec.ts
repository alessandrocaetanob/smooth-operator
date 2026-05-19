import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { LoadingSkeleton } from './loading-skeleton';

describe('LoadingSkeleton', () => {
  function create(rows = 3, type: 'text' | 'card' | 'table-row' = 'text'): LoadingSkeleton {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [LoadingSkeleton] });
    const fixture = TestBed.createComponent(LoadingSkeleton);
    fixture.componentInstance.rows = rows;
    fixture.componentInstance.type = type;
    return fixture.componentInstance;
  }

  it('should create', () => {
    expect(create()).toBeTruthy();
  });

  it('rowArray length matches rows input', () => {
    expect(create(5).rowArray.length).toBe(5);
    expect(create(0).rowArray.length).toBe(0);
  });
});
