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

    const rows = fixture.nativeElement.querySelectorAll(
      '.invoice-detail-page__table-wrapper tbody tr',
    );
    expect(rows.length).toBe(2);
  });

  it('should render the item count in the summary', () => {
    setup();

    expect(fixture.nativeElement.textContent).toContain('2');
  });

  it('should align the Quantidade header and its values on the same (numeric) column class', () => {
    setup();

    const headerCells: HTMLTableCellElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.invoice-detail-page__table-wrapper thead th'),
    );
    const quantidadeHeader = headerCells.find((th) => th.textContent?.trim() === 'Quantidade')!;
    expect(quantidadeHeader.classList).toContain('data-table__col-numeric');

    const quantidadeCells: HTMLTableCellElement[] = Array.from(
      fixture.nativeElement.querySelectorAll(
        '.invoice-detail-page__table-wrapper tbody td.data-table__col-numeric',
      ),
    );
    expect(quantidadeCells.length).toBe(2);
    quantidadeCells.forEach((cell) => expect(cell.classList).toContain('data-table__col-numeric'));

    const codigoHeader = headerCells.find((th) => th.textContent?.trim() === 'Código')!;
    expect(codigoHeader.classList).toContain('data-table__col-text');
  });

  it('should show the irreversible stock debit notice only while the invoice is open', () => {
    setup(of(openInvoice));

    expect(
      fixture.nativeElement.querySelector('.invoice-detail-page__notice')?.textContent,
    ).toContain(
      'Imprimir fecha a nota e realiza a baixa no estoque. A ação não pode ser desfeita.',
    );
  });

  it('should not show the irreversible stock debit notice for a closed invoice', () => {
    setup(of(closedInvoice));

    expect(fixture.nativeElement.querySelector('.invoice-detail-page__notice')).toBeFalsy();
  });

  it('should show a not-found message on 404 (no errorCode)', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 404 })));

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('não foi encontrado');
  });

  it('should show the INVOICE_NOT_FOUND errorCode message on 404', () => {
    setup(
      throwError(
        () => new HttpErrorResponse({ status: 404, error: { errorCode: 'INVOICE_NOT_FOUND' } }),
      ),
    );

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Nota não encontrada.');
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

  it('should call the print endpoint exactly once, update the status and trigger window.print only after the HTTP success', () => {
    setup(of(openInvoice), of(closedInvoice));

    printButton().click();
    fixture.detectChanges();

    expect(invoicesService.print).toHaveBeenCalledTimes(1);
    expect(invoicesService.print).toHaveBeenCalledWith(5);
    expect(fixture.nativeElement.textContent).toContain('Fechada');
    expect(notification.error).not.toHaveBeenCalled();
    expect(printSpy).toHaveBeenCalledTimes(1);
  });

  it('should have flushed the closed invoice to the DOM (including app-invoice-print-view) by the time window.print() is called', () => {
    let printViewTextAtPrintTime: string | null = null;
    setup(of(openInvoice), of(closedInvoice));
    // Overrides the default no-op mock installed by `setup()` so we can
    // inspect the DOM synchronously from inside the call itself — the only
    // way to prove the print view was already re-rendered *before*
    // `window.print()` ran, not merely by the time the test assertions run
    // (which could pass even with stale DOM, since `fixture.detectChanges()`
    // below would paper over the bug).
    printSpy.mockRestore();
    printSpy = vi.spyOn(window, 'print').mockImplementation(() => {
      printViewTextAtPrintTime =
        fixture.nativeElement.querySelector('app-invoice-print-view')?.textContent ?? null;
    });

    printButton().click();

    expect(printSpy).toHaveBeenCalledTimes(1);
    expect(printViewTextAtPrintTime).not.toBeNull();
    expect(printViewTextAtPrintTime).toContain('Fechada');
    expect(printViewTextAtPrintTime).not.toContain('Aberta');
  });

  it('should not show the success toast before window.print(), and show it exactly once with the invoice number after the print dialog closes (afterprint)', () => {
    setup(of(openInvoice), of(closedInvoice));

    printButton().click();
    fixture.detectChanges();

    expect(printSpy).toHaveBeenCalledTimes(1);
    expect(notification.success).not.toHaveBeenCalled();

    window.dispatchEvent(new Event('afterprint'));

    expect(notification.success).toHaveBeenCalledTimes(1);
    expect(notification.success).toHaveBeenCalledWith('Nota Nº 42 impressa e fechada com sucesso.');
  });

  it('should show the success toast only once even if afterprint fires more than once', () => {
    setup(of(openInvoice), of(closedInvoice));

    printButton().click();
    fixture.detectChanges();

    window.dispatchEvent(new Event('afterprint'));
    window.dispatchEvent(new Event('afterprint'));

    expect(notification.success).toHaveBeenCalledTimes(1);
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

  it('should show exactly one toast on a 409 without a recognized errorCode, no inline duplicate, keeping the invoice open', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 409 })),
    );

    printButton().click();
    fixture.detectChanges();

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(
      expect.stringContaining('Não foi possível concluir a impressão'),
    );
    expect(fixture.nativeElement.querySelector('.invoice-detail-page__print-error')).toBeFalsy();
    expect(printSpy).not.toHaveBeenCalled();
    expect(notification.success).not.toHaveBeenCalled();
    // The invoice is still rendered as Open: the frontend never flips
    // status locally on a failed print, only Billing.Api's response does.
    expect(fixture.nativeElement.textContent).toContain('Aberta');
    // The irreversibility notice stays visible — it is not the removed
    // transient print-error text, and printing is still retryable.
    expect(fixture.nativeElement.querySelector('.invoice-detail-page__notice')).toBeTruthy();
  });

  it('should show exactly one toast with the backend-provided insufficient-stock detail, no inline duplicate, keeping the invoice open', () => {
    setup(
      of(openInvoice),
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              errorCode: 'INSUFFICIENT_STOCK',
              detail: 'O produto "A1" não possui saldo suficiente. Disponível: 2; solicitado: 3.',
            },
          }),
      ),
    );

    printButton().click();
    fixture.detectChanges();

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(
      'O produto "A1" não possui saldo suficiente. Disponível: 2; solicitado: 3.',
    );
    expect(fixture.nativeElement.querySelector('.invoice-detail-page__print-error')).toBeFalsy();
    expect(printSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Aberta');
  });

  it('should show the INVOICE_ALREADY_CLOSED toast message when printing an already-closed invoice', () => {
    setup(
      of(openInvoice),
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { errorCode: 'INVOICE_ALREADY_CLOSED' },
          }),
      ),
    );

    printButton().click();
    fixture.detectChanges();

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(expect.stringContaining('já foi fechada'));
  });

  it('should show a friendly toast on 404 when printing, with no inline duplicate', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 404 })),
    );

    printButton().click();
    fixture.detectChanges();

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(expect.stringContaining('não foi encontrado'));
    expect(fixture.nativeElement.querySelector('.invoice-detail-page__print-error')).toBeFalsy();
  });

  it('should show a friendly, Portuguese-only toast on 503 (Inventory unavailable), with no inline duplicate', () => {
    setup(
      of(openInvoice),
      throwError(() => new HttpErrorResponse({ status: 503 })),
    );

    printButton().click();
    fixture.detectChanges();

    expect(notification.error).toHaveBeenCalledTimes(1);
    const message = notification.error.mock.calls[0][0] as string;
    expect(message).toContain('indisponível');
    expect(message).not.toMatch(/Http failure|\[object Object\]/);
    expect(fixture.nativeElement.querySelector('.invoice-detail-page__print-error')).toBeFalsy();
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

  describe('"Voltar para a listagem" link', () => {
    function backLink(): HTMLAnchorElement {
      return fixture.nativeElement.querySelector('.invoice-detail-page__header a');
    }

    it('should point back to the listing without extra query params when arriving directly (no router state)', () => {
      setup();

      expect(backLink().getAttribute('href')).toBe('/notas');
    });

    it('should preserve the listing page/pageSize/sortBy/sortDirection carried over as router navigation state', () => {
      history.replaceState(
        {
          listQueryParams: {
            page: '2',
            pageSize: '10',
            sortBy: 'createdAtUtc',
            sortDirection: 'asc',
          },
        },
        '',
      );

      setup();

      const href = backLink().getAttribute('href')!;
      expect(href).toContain('/notas?');
      expect(href).toContain('page=2');
      expect(href).toContain('pageSize=10');
      expect(href).toContain('sortBy=createdAtUtc');
      expect(href).toContain('sortDirection=asc');

      // Reset to avoid leaking history state into other test files.
      history.replaceState({}, '');
    });
  });
});
