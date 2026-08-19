import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

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

  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  protected readonly printing = signal(false);
  protected readonly printError = signal<string | null>(null);

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

    this.printError.set(null);
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

  private handlePrintSuccess(invoice: Invoice): void {
    this.invoice.set(invoice);
    this.notification.success(`Nota Nº ${invoice.number} fechada com sucesso.`);
    window.print();
  }

  private handlePrintError(error: HttpErrorResponse): void {
    if (error.status === 404) {
      this.printError.set('Nota não encontrada.');
      return;
    }

    if (error.status === 409) {
      this.printError.set(
        error.error?.detail ??
          'Não foi possível fechar a nota: ela já está fechada ou o saldo em estoque é insuficiente.',
      );
      return;
    }

    if (error.status === 0 || error.status === 503) {
      this.printError.set(
        'Serviço de estoque indisponível no momento. Tente novamente em instantes.',
      );
      return;
    }

    this.printError.set('Não foi possível imprimir a nota. Tente novamente.');
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
    if (error.status === 404) {
      this.loadError.set('Nota não encontrada.');
      return;
    }

    if (error.status === 0 || error.status === 503) {
      this.loadError.set(
        'Serviço de faturamento indisponível no momento. Tente novamente em instantes.',
      );
      return;
    }

    this.loadError.set('Não foi possível carregar a nota. Tente novamente.');
  }
}
