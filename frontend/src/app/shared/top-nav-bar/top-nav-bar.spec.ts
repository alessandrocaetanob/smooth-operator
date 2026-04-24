import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { TopNavBar } from './top-nav-bar';
import { of } from 'rxjs';

describe('TopNavBar', () => {
  let component: TopNavBar;
  let fixture: ComponentFixture<TopNavBar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TopNavBar],
    }).compileComponents();

    fixture = TestBed.createComponent(TopNavBar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
