import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';

import { MyAccess } from './my-access';

describe('MyAccess', () => {
  let component: MyAccess;
  let fixture: ComponentFixture<MyAccess>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyAccess],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(MyAccess);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
