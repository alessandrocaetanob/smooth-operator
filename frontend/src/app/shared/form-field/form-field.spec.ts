import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { FormField } from './form-field';

describe('FormField', () => {
  it('should create with defaults', () => {
    TestBed.configureTestingModule({ imports: [FormField] });
    const fixture = TestBed.createComponent(FormField);
    expect(fixture.componentInstance.label).toBe('');
  });
});
