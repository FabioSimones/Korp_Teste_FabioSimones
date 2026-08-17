import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { NotFound } from './not-found';

describe('NotFound', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotFound],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(NotFound);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render a message explaining the page was not found', () => {
    const fixture = TestBed.createComponent(NotFound);
    fixture.detectChanges();

    const heading = fixture.nativeElement.querySelector('#not-found-title');
    expect(heading?.textContent).toContain('Página não encontrada');
  });

  it('should provide a link back to the home route', () => {
    const fixture = TestBed.createComponent(NotFound);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a[routerLink="/"]');
    expect(link).toBeTruthy();
  });
});
