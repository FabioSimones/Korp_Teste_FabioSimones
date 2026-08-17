import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { finalize } from 'rxjs';

import { InvoiceStatusBadge } from '../invoice-status-badge/invoice-status-badge';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';

/**
 * Displays a single invoice: number, date, status badge and the item
 * snapshot (code/description/quantity) as returned by Billing.Api.
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
    MatTableModule,
    InvoiceStatusBadge,
  ],
  templateUrl: './invoice-detail-page.html',
  styleUrl: './invoice-detail-page.scss',
})
export class InvoiceDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly invoicesService = inject(InvoicesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly displayedColumns = ['code', 'description', 'quantity'] as const;

  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  private readonly invoiceId = Number(this.route.snapshot.paramMap.get('id'));

  constructor() {
    this.loadInvoice();
  }

  protected reload(): void {
    this.loadInvoice();
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
