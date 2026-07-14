import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { describe, it, expect } from 'vitest';
import { Integrations } from './integrations';

describe('Integrations', () => {
  it('should create', async () => {
    await TestBed.configureTestingModule({
      imports: [Integrations],
      providers: [provideTranslateService()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Integrations);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
