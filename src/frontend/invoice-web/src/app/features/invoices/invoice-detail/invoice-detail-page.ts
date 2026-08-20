import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { getUserFriendlyErrorMessage } from '../../../core/utils/http-error-messages';
import { NotificationService } from '../../../core/services/notification.service';
import { InvoicePrintView } from '../invoice-print-view/invoice-print-view';
import { InvoiceStatusBadge } from '../invoice-status-badge/invoice-status-badge';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';

/**
 * Displays a single invoice: number, date, status badge and the item
 * snapshot (code/description/quantity) as returned by Billing.Api. Also
 * hosts the print/close action (`POST /api/invoices/{id}/print`): closing
 * an invoice is only possible from here.
 */
@Component({
  selector: 'app-invoice-detail-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    InvoiceStatusBadge,
    InvoicePrintView,
  ],
  templateUrl: './invoice-detail-page.html',
  styleUrl: './invoice-detail-page.scss',
})
export class InvoiceDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly invoicesService = inject(InvoicesService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);

  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  /**
   * The listing's `page`/`pageSize`/`sortBy`/`sortDirection`, as passed by
   * `InvoicesListPage.openInvoice` via router navigation `state` (not the
   * URL), so "Voltar para a listagem" restores the exact page/sort the user
   * came from instead of resetting to the defaults. Falls back to `{}`
   * (defaults) when arriving here any other way — a direct link, a page
   * refresh, or a bookmark — none of which break: `/notas` on its own
   * already normalizes to the default page/size/sort.
   */
  protected readonly backQueryParams: Record<string, string> =
    (history.state as { listQueryParams?: Record<string, string> } | undefined)?.listQueryParams ??
    {};

  protected readonly printing = signal(false);

  /** The print action is only available while the invoice is still `Open`. */
  protected readonly canPrint = computed(() => this.invoice()?.status === 'Open');

  private readonly invoiceId = Number(this.route.snapshot.paramMap.get('id'));

  constructor() {
    this.loadInvoice();
  }

  protected reload(): void {
    this.loadInvoice();
  }

  /**
   * Triggers the print/close flow. Guarded by `printing()` and `canPrint()`
   * so a duplicate click while the request is in flight (or on an already
   * closed invoice) never fires a second call to the endpoint that debits
   * stock.
   */
  protected print(): void {
    if (this.printing() || !this.canPrint()) {
      return;
    }

    this.printing.set(true);

    this.invoicesService
      .print(this.invoiceId)
      .pipe(
        finalize(() => this.printing.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (invoice) => this.handlePrintSuccess(invoice),
        error: (error: HttpErrorResponse) => this.handlePrintError(error),
      });
  }

  /**
   * `window.print()` only runs after the backend has confirmed the
   * print/close (this callback only fires on HTTP success), so the
   * printable view never opens for a request that is still in flight or
   * that failed.
   *
   * `this.cdr.detectChanges()` runs a synchronous change-detection pass
   * *before* `window.print()`. Without it, `window.print()` can execute
   * before Angular has flushed the signal update to the DOM (signals don't
   * guarantee a synchronous render on `.set()`), so the browser would print
   * `app-invoice-print-view` still showing the pre-close snapshot (`Status:
   * Aberta`) — exactly the bug this fixes. `detectChanges()` on this
   * component's own view also walks into `app-invoice-print-view`, since
   * it's a plain child node in this same template (see
   * `invoice-detail-page.html`), so one call synchronizes both.
   */
  private handlePrintSuccess(invoice: Invoice): void {
    this.invoice.set(invoice);
    this.cdr.detectChanges();
    this.printAndNotifyOnceClosed(invoice.number);
  }

  /**
   * Defers the success toast until the browser's print UI has actually been
   * dismissed (printed or cancelled), via the standard `afterprint` event —
   * never before `window.print()` is called, and never on a fixed delay.
   * `afterprint` is the only cross-browser-reliable signal for "the print
   * dialog closed": `window.print()` itself blocks script execution in some
   * browsers (e.g. Chrome) but not others (e.g. Firefox), so code that ran
   * "right after" the call could show the toast while the dialog is still
   * open in some browsers.
   */
  private printAndNotifyOnceClosed(invoiceNumber: number): void {
    const handleAfterPrint = (): void => {
      window.removeEventListener('afterprint', handleAfterPrint);
      this.notification.success(`Nota Nº ${invoiceNumber} impressa e fechada com sucesso.`);
    };

    window.addEventListener('afterprint', handleAfterPrint);
    // Guards against a leaked listener if the user navigates away from this
    // page while the print dialog is still open (afterprint would then
    // never fire on this component instance).
    this.destroyRef.onDestroy(() => window.removeEventListener('afterprint', handleAfterPrint));

    window.print();
  }

  /**
   * Resolves a single, user-facing message via `getUserFriendlyErrorMessage`
   * and shows it as a toast — the only feedback for this failure.
   * `InvoicesService` skips the interceptor's own notification for every
   * request, so this is the *only* toast shown. The invoice itself is left
   * untouched (still `Open`, still fully rendered) so the user can retry
   * once stock is replenished; the irreversibility notice above the button
   * already covers what printing does, so no separate inline error text is
   * needed here.
   */
  private handlePrintError(error: HttpErrorResponse): void {
    this.notification.error(getUserFriendlyErrorMessage(error, 'invoice-print'));
  }

  private loadInvoice(): void {
    if (!Number.isInteger(this.invoiceId) || this.invoiceId <= 0) {
      this.loading.set(false);
      this.loadError.set('Identificador de nota inválido.');
      return;
    }

    this.loading.set(true);
    this.loadError.set(null);

    this.invoicesService
      .getById(this.invoiceId)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (invoice) => this.invoice.set(invoice),
        error: (error: HttpErrorResponse) => this.handleLoadError(error),
      });
  }

  private handleLoadError(error: HttpErrorResponse): void {
    this.loadError.set(getUserFriendlyErrorMessage(error, 'invoice-detail'));
  }
}
