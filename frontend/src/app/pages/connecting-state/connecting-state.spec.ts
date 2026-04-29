import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach } from 'vitest';

import { ConnectingState } from './connecting-state';

describe('ConnectingState', () => {
  let component: ConnectingState;
  let fixture: ComponentFixture<ConnectingState>;

  beforeEach(async () => {
    const paramMap = convertToParamMap({ id: 'test-id' });
    await TestBed.configureTestingModule({
      imports: [ConnectingState],
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

    fixture = TestBed.createComponent(ConnectingState);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
