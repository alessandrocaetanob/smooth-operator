import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Hosts } from './hosts';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('Hosts', () => {
  let component: Hosts;
  let fixture: ComponentFixture<Hosts>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Hosts],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Hosts);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
