import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect } from 'vitest';
import { Settings } from './settings';

describe('Settings', () => {
  it('should create', () => {
    TestBed.configureTestingModule({
      imports: [Settings],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(Settings);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
