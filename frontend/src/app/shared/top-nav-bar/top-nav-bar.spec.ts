import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TopNavBar } from './top-nav-bar';

describe('TopNavBar', () => {
  let component: TopNavBar;
  let fixture: ComponentFixture<TopNavBar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TopNavBar],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(TopNavBar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
