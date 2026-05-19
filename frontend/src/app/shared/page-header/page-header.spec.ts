import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { PageHeader } from './page-header';

describe('PageHeader', () => {
  it('should create', () => {
    TestBed.configureTestingModule({ imports: [PageHeader] });
    const fixture = TestBed.createComponent(PageHeader);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
