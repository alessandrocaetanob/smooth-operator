import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActiveSession } from './active-session';

describe('ActiveSession', () => {
  let component: ActiveSession;
  let fixture: ComponentFixture<ActiveSession>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActiveSession],
    }).compileComponents();

    fixture = TestBed.createComponent(ActiveSession);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
