import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Mascot } from './mascot';

describe('Mascot', () => {
  let component: Mascot;
  let fixture: ComponentFixture<Mascot>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Mascot],
    }).compileComponents();

    fixture = TestBed.createComponent(Mascot);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
