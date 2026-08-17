import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoiceDetailPage } from './invoice-detail-page';

interface InvoicesServiceStub {
  getById: ReturnType<typeof vi.fn>;
}

describe('InvoiceDetailPage', () => {
  let fixture: ComponentFixture<InvoiceDetailPage>;
  let invoicesService: InvoicesServiceStub;

  const sampleInvoice: Invoice = {
    id: 5,
    number: 42,
    status: 'Open',
    createdAtUtc: '2026-08-17T10:00:00Z',
    items: [
      { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 3 },
      { id: 2, productId: 2, productCode: 'B2', productDescription: 'Produto B', quantity: 1 },
    ],
  };

  function setup(getByIdResult = of(sampleInvoice), routeId = '5'): void {
    invoicesService = { getById: vi.fn().mockReturnValue(getByIdResult) };

    TestBed.configureTestingModule({
      imports: [InvoiceDetailPage],
      providers: [
        provideNoopAnimations(),
        { provide: InvoicesService, useValue: invoicesService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(InvoiceDetailPage);
    fixture.detectChanges();
  }

  it('should request the invoice using the route id', () => {
    setup();

    expect(invoicesService.getById).toHaveBeenCalledWith(5);
  });

  it('should render number, date, status badge and items', () => {
    setup();

    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('42');
    expect(content).toContain('Aberta');
    expect(content).toContain('A1');
    expect(content).toContain('Produto A');
    expect(content).toContain('B2');

    const rows = fixture.nativeElement.querySelectorAll('tr[mat-row]');
    expect(rows.length).toBe(2);
  });

  it('should show a not-found message on 404', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 404 })));

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Nota não encontrada');
  });

  it('should show an unavailable-service message when the API cannot be reached', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 0 })));

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('indisponível');
  });

  it('should allow retrying after a failed load', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 503 })));

    invoicesService.getById.mockReturnValue(of(sampleInvoice));
    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.invoice-detail-page__state button',
    );
    retryButton.click();
    fixture.detectChanges();

    expect(invoicesService.getById).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('42');
  });
});
