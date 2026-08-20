import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { Shell } from './shell';

@Component({ standalone: true, template: '<p>produtos placeholder</p>' })
class FakeProductsPage {}

@Component({ standalone: true, template: '<p>notas placeholder</p>' })
class FakeInvoicesPage {}

describe('Shell', () => {
  let fixture: ComponentFixture<Shell>;
  let router: Router;

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
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('should create the shell', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the KORP ERP brand', () => {
    const brandName = fixture.nativeElement.querySelector('.app-shell__brand-name');
    const brandTag = fixture.nativeElement.querySelector('.app-shell__brand-tag');
    expect(brandName?.textContent?.trim()).toBe('KORP ERP');
    expect(brandTag?.textContent?.trim()).toBe('Fiscal');
  });

  it('should render exactly two top navigation links: Produtos and Notas fiscais', () => {
    const links: NodeListOf<HTMLAnchorElement> =
      fixture.nativeElement.querySelectorAll('.app-shell__nav-link');

    expect(links.length).toBe(2);
    const labels = Array.from(links).map((link) => link.textContent?.trim());
    expect(labels).toContain('Produtos');
    expect(labels).toContain('Notas fiscais');
  });

  it('should not render a sidenav or drawer at any breakpoint', () => {
    expect(fixture.nativeElement.querySelector('mat-sidenav')).toBeFalsy();
    expect(fixture.nativeElement.querySelector('.app-shell__sidenav')).toBeFalsy();
  });

  it('should mark the active route link with aria-current="page"', async () => {
    await router.navigate(['/produtos']);
    fixture.detectChanges();

    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.app-shell__nav-link'),
    );
    const activeLink = links.find((link) => link.textContent?.trim() === 'Produtos');
    const inactiveLink = links.find((link) => link.textContent?.trim() === 'Notas fiscais');

    expect(activeLink?.getAttribute('aria-current')).toBe('page');
    expect(inactiveLink?.getAttribute('aria-current')).toBeFalsy();
  });
});
