import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { InvoiceStatusBadge } from '../invoice-status-badge/invoice-status-badge';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';

/**
 * Lists all registered invoices, consuming Billing.Api directly.
 * Registration and details live in their own routes/components.
 */
@Component({
  selector: 'app-invoices-list-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    InvoiceStatusBadge,
  ],
  templateUrl: './invoices-list-page.html',
  styleUrl: './invoices-list-page.scss',
})
export class InvoicesListPage {
  private readonly invoicesService = inject(InvoicesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  protected readonly invoices = signal<Invoice[]>([]);
  protected readonly loading = signal(true);
  protected readonly listError = signal<string | null>(null);

  constructor() {
    this.loadInvoices();
  }

  protected reload(): void {
    this.loadInvoices();
  }

  /** Opens the invoice detail route. Used by both click and keyboard (Enter/Space) row activation. */
  protected openInvoice(id: number): void {
    this.router.navigate(['/notas', id]);
  }

  protected onRowKeydown(event: KeyboardEvent, id: number): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openInvoice(id);
    }
  }

  private loadInvoices(): void {
    this.loading.set(true);
    this.listError.set(null);

    this.invoicesService
      .getAll()
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (invoices) => this.invoices.set([...invoices].sort((a, b) => b.number - a.number)),
        error: () =>
          this.listError.set('Não foi possível carregar a lista de notas. Tente novamente.'),
      });
  }
}
