import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FirstAccess } from './first-access';

describe('FirstAccess', () => {
  let component: FirstAccess;
  let fixture: ComponentFixture<FirstAccess>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FirstAccess],
    }).compileComponents();

    fixture = TestBed.createComponent(FirstAccess);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
