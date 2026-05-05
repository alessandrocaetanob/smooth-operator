import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach } from 'vitest';

import { Invite } from './invite';

describe('Invite', () => {
  let component: Invite;
  let fixture: ComponentFixture<Invite>;

  beforeEach(async () => {
    const paramMap = convertToParamMap({ token: 'test-token' });
    await TestBed.configureTestingModule({
      imports: [Invite],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap },
            paramMap: of(paramMap),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Invite);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
