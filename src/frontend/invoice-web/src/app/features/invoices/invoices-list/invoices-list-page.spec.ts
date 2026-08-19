import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Observable, Subject, of, throwError } from 'rxjs';

import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoicesListPage } from './invoices-list-page';

interface InvoicesServiceStub {
  getAll: ReturnType<typeof vi.fn>;
}

describe('InvoicesListPage', () => {
  let fixture: ComponentFixture<InvoicesListPage>;
  let invoicesService: InvoicesServiceStub;
  let router: Router;

  const sampleInvoices: Invoice[] = [
    {
      id: 1,
      number: 1,
      status: 'Open',
      createdAtUtc: '2026-08-17T10:00:00Z',
      closedAtUtc: null,
      items: [
        { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 2 },
      ],
    },
    {
      id: 2,
      number: 2,
      status: 'Closed',
      createdAtUtc: '2026-08-17T11:00:00Z',
      closedAtUtc: '2026-08-17T11:05:00Z',
      items: [],
    },
  ];

  function setup(getAllResult: Observable<Invoice[]> = of(sampleInvoices)): void {
    invoicesService = { getAll: vi.fn().mockReturnValue(getAllResult) };

    TestBed.configureTestingModule({
      imports: [InvoicesListPage],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: InvoicesService, useValue: invoicesService },
      ],
    });

    fixture = TestBed.createComponent(InvoicesListPage);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  }

  it('should render the page title and subtitle', () => {
    setup(of([]));

    const title = fixture.nativeElement.querySelector('#invoices-title');
    expect(title?.textContent?.trim()).toBe('Notas fiscais');
    expect(
      fixture.nativeElement.querySelector('.invoices-list-page__subtitle')?.textContent,
    ).toContain('Emissão e impressão de notas');
  });

  it('should render the "Nova nota fiscal" call to action', () => {
    setup(of([]));

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector(
      'a[routerLink="/notas/nova"]',
    );
    expect(link?.textContent).toContain('Nova nota fiscal');
  });

  it('should show a loading indicator while fetching the invoice list', () => {
    const pending = new Subject<Invoice[]>();
    setup(pending.asObservable());

    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeTruthy();
  });

  it('should render the empty state when there are no invoices', () => {
    setup(of([]));

    const empty = fixture.nativeElement.querySelector('.invoices-list-page__empty');
    expect(empty?.textContent).toContain('Nenhuma nota cadastrada');
    expect(fixture.nativeElement.querySelector('table')).toBeFalsy();
  });

  it('should render a table row for each invoice, most recent number first, with item count and status badges', () => {
    setup(of(sampleInvoices));

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('2');
    expect(fixture.nativeElement.textContent).toContain('Aberta');
    expect(fixture.nativeElement.textContent).toContain('Fechada');
  });

  it('should navigate to the invoice detail route when a row is clicked', () => {
    setup(of(sampleInvoices));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const row: HTMLTableRowElement = fixture.nativeElement.querySelector('tbody tr');
    row.click();

    expect(router.navigate).toHaveBeenCalledWith(['/notas', 2]);
  });

  it('should be reachable and activatable by keyboard (tabindex + Enter)', () => {
    setup(of(sampleInvoices));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const row: HTMLTableRowElement = fixture.nativeElement.querySelector('tbody tr');
    expect(row.getAttribute('tabindex')).toBe('0');
    expect(row.getAttribute('role')).toBe('link');

    row.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(router.navigate).toHaveBeenCalledWith(['/notas', 2]);
  });

  it('should show an error state with a retry action when the list request fails', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 503 })));

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Não foi possível carregar a lista de notas');

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.invoices-list-page__state button',
    );
    invoicesService.getAll.mockReturnValue(of(sampleInvoices));
    retryButton.click();
    fixture.detectChanges();

    expect(invoicesService.getAll).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
  });
});
