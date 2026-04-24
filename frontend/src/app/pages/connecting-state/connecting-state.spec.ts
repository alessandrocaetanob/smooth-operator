import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConnectingState } from './connecting-state';

describe('ConnectingState', () => {
  let component: ConnectingState;
  let fixture: ComponentFixture<ConnectingState>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConnectingState],
    }).compileComponents();

    fixture = TestBed.createComponent(ConnectingState);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
