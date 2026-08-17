import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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

  const sampleInvoices: Invoice[] = [
    {
      id: 1,
      number: 1,
      status: 'Open',
      createdAtUtc: '2026-08-17T10:00:00Z',
      items: [],
    },
    {
      id: 2,
      number: 2,
      status: 'Closed',
      createdAtUtc: '2026-08-17T11:00:00Z',
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
    fixture.detectChanges();
  }

  it('should render the page title', () => {
    setup(of([]));

    const title = fixture.nativeElement.querySelector('#invoices-title');
    expect(title?.textContent?.trim()).toBe('Notas');
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

  it('should render a table row for each invoice, most recent number first', () => {
    setup(of(sampleInvoices));

    const rows = fixture.nativeElement.querySelectorAll('tr[mat-row]');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('2');
    expect(fixture.nativeElement.textContent).toContain('Aberta');
    expect(fixture.nativeElement.textContent).toContain('Fechada');
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
    expect(fixture.nativeElement.querySelectorAll('tr[mat-row]').length).toBe(2);
  });
});
