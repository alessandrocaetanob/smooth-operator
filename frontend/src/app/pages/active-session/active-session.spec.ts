import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach } from 'vitest';

import { ActiveSession } from './active-session';

describe('ActiveSession', () => {
  let component: ActiveSession;
  let fixture: ComponentFixture<ActiveSession>;

  beforeEach(async () => {
    const paramMap = convertToParamMap({ id: 'test-id' });
    await TestBed.configureTestingModule({
      imports: [ActiveSession],
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap },
            paramMap: of(paramMap),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ActiveSession);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
