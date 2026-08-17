import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { Shell } from './shell';

@Component({ standalone: true, template: '<p>produtos placeholder</p>' })
class FakeProductsPage {}

@Component({ standalone: true, template: '<p>notas placeholder</p>' })
class FakeInvoicesPage {}

describe('Shell', () => {
  let fixture: ComponentFixture<Shell>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideNoopAnimations(),
        provideRouter([
          { path: '', redirectTo: 'produtos', pathMatch: 'full' },
          { path: 'produtos', component: FakeProductsPage },
          { path: 'notas', component: FakeInvoicesPage },
        ]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
  });

  it('should create the shell', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render navigation links for Produtos and Notas', () => {
    const links = fixture.nativeElement.querySelectorAll('a[mat-list-item]');
    const labels = Array.from(links as NodeListOf<HTMLAnchorElement>).map((link) =>
      link.textContent?.trim(),
    );

    expect(labels.some((label) => label?.includes('Produtos'))).toBe(true);
    expect(labels.some((label) => label?.includes('Notas'))).toBe(true);
  });

  it('should render the toolbar title', () => {
    const title = fixture.nativeElement.querySelector('.shell__title');
    expect(title?.textContent?.trim()).toBe('Korp - Emissão de Notas');
  });
});
