import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Observable, Subject, of, throwError } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoiceDetailPage } from './invoice-detail-page';

interface InvoicesServiceStub {
  getById: ReturnType<typeof vi.fn>;
  print: ReturnType<typeof vi.fn>;
}

describe('InvoiceDetailPage', () => {
  let fixture: ComponentFixture<InvoiceDetailPage>;
  let invoicesService: InvoicesServiceStub;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let printSpy: ReturnType<typeof vi.spyOn>;

  const openInvoice: Invoice = {
    id: 5,
    number: 42,
    status: 'Open',
    createdAtUtc: '2026-08-17T10:00:00Z',
    closedAtUtc: null,
    items: [
      { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 3 },
      { id: 2, productId: 2, productCode: 'B2', productDescription: 'Produto B', quantity: 1 },
    ],
  };

  const closedInvoice: Invoice = {
    ...openInvoice,
    status: 'Closed',
    closedAtUtc: '2026-08-18T09:00:00Z',
  };

  function setup(
    getByIdResult: Observable<Invoice> = of(openInvoice),
    printResult: Observable<Invoice> = of(closedInvoice),
    routeId = '5',
  ): void {
    invoicesService = {
      getById: vi.fn().mockReturnValue(getByIdResult),
      print: vi.fn().mockReturnValue(printResult),
    };
    notification = { success: vi.fn(), error: vi.fn() };
    printSpy = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    TestBed.configureTestingModule({
      imports: [InvoiceDetailPage],
      providers: [
        provideNoopAnimations(),
        { provide: InvoicesService, useValue: invoicesService },
        { provide: NotificationService, useValue: notification },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: routeId }) } },
        },
      ],
    });

    fixture = TestBed.createComponent(InvoiceDetailPage);
    fixture.detectChanges();
  }

  function printButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.invoice-detail-page__actions button');
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

    invoicesService.getById.mockReturnValue(of(openInvoice));
    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.invoice-detail-page__state button',
    );
    retryButton.click();
    fixture.detectChanges();

    expect(invoicesService.getById).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should show an enabled print button for an open invoice', () => {
    setup(of(openInvoice));
    expect(printButton().disabled).toBe(false);
  });

  it('should show a disabled print button and a hint for a closed invoice', () => {
    setup(of(closedInvoice));
    expect(printButton().disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('já está fechada');
  });

  it('should show a spinner and disable the button while printing', () => {
    const printSubject = new Subject<Invoice>();
    setup(of(openInvoice), printSubject);

    printButton().click();
    fixture.detectChanges();

    expect(printButton().disabled).toBe(true);
    expect(
      fixture.nativeElement.querySelector('.invoice-detail-page__actions mat-progress-spinner'),
    ).toBeTruthy();

    printSubject.next(closedInvoice);
    printSubject.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Fechada');
  });

  it('should call the print endpoint exactly once, update the status and trigger window.print on success', () => {
    setup(of(openInvoice), of(closedInvoice));

    printButton().click();
    fixture.detectChanges();

    expect(invoicesService.print).toHaveBeenCalledTimes(1);
    expect(invoicesService.print).toHaveBeenCalledWith(5);
    expect(fixture.nativeElement.textContent).toContain('Fechada');
    expect(notification.success).toHaveBeenCalled();
    expect(printSpy).toHaveBeenCalledTimes(1);
  });

  it('should not trigger a second HTTP call when the button is clicked again while a request is in flight', () => {
    const printSubject = new Subject<Invoice>();
    setup(of(openInvoice), printSubject);

    printButton().click();
    fixture.detectChanges();
    printButton().click();
    printButton().click();
    fixture.detectChanges();

    expect(invoicesService.print).toHaveBeenCalledTimes(1);
  });

  it('should show a friendly message on 409 (already closed or insufficient balance)', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 409 })),
    );

    printButton().click();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('.invoice-detail-page__print-error');
    expect(alert?.textContent).toContain('fechada');
    expect(printSpy).not.toHaveBeenCalled();
  });

  it('should show a friendly message on 404 when printing', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 404 })),
    );

    printButton().click();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('.invoice-detail-page__print-error');
    expect(alert?.textContent).toContain('Nota não encontrada');
  });

  it('should show a friendly message on 503 (Inventory unavailable)', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 503 })),
    );

    printButton().click();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('.invoice-detail-page__print-error');
    expect(alert?.textContent).toContain('indisponível');
  });

  it('should re-enable the button after a failed print attempt', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 409 })),
    );

    printButton().click();
    fixture.detectChanges();

    expect(printButton().disabled).toBe(false);
  });
});
