import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import { Monitoring } from './monitoring';

describe('Monitoring', () => {
  let component: Monitoring;
  let fixture: ComponentFixture<Monitoring>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Monitoring],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Monitoring);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture.destroy();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
