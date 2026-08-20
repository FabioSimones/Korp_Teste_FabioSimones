import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { getUserFriendlyErrorMessage } from '../../../core/utils/http-error-messages';
import { NotificationService } from '../../../core/services/notification.service';
import {
  DEFAULT_PAGE_NUMBER,
  DEFAULT_PAGE_SIZE,
  PAGE_SIZE_OPTIONS,
  PagedResponse,
} from '../../../shared/pagination/paged-response';
import { PageSizeSelect } from '../../../shared/pagination/page-size-select';
import { Pagination } from '../../../shared/pagination/pagination';
import { SortDirection, resolveSort, toggleSortDirection } from '../../../shared/pagination/sort';
import { InvoiceFormComponent } from '../invoice-form/invoice-form';
import { InvoiceStatusBadge } from '../invoice-status-badge/invoice-status-badge';
import {
  DEFAULT_INVOICE_SORT_FIELD,
  INVOICE_SORT_FIELDS,
  InvoiceSortField,
  InvoiceSummary,
} from '../models/invoice';
import { InvoicesService } from '../invoices.service';

const DEFAULT_SORT_DIRECTION: SortDirection = 'desc';

/**
 * Lists all registered invoices (server-side paginated and sortable),
 * consuming Billing.Api directly. Registration happens in an
 * `InvoiceFormComponent` opened as a `MatDialog` from the "+ Nova nota
 * fiscal" button (the same component also backs the standalone `/notas/nova`
 * route, see `InvoiceFormPage`). Details live in their own route.
 */
@Component({
  selector: 'app-invoices-list-page',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    InvoiceStatusBadge,
    PageSizeSelect,
    Pagination,
  ],
  templateUrl: './invoices-list-page.html',
  styleUrl: './invoices-list-page.scss',
})
export class InvoicesListPage {
  private readonly invoicesService = inject(InvoicesService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  // See ProductsPage.loadErrorNotified for the reasoning: one toast per
  // failure streak, not one per retry/page attempt.
  private loadErrorNotified = false;

  protected readonly invoices = signal<InvoiceSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly listError = signal<string | null>(null);

  protected readonly pageNumber = signal(DEFAULT_PAGE_NUMBER);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly sortBy = signal<InvoiceSortField>(DEFAULT_INVOICE_SORT_FIELD);
  protected readonly sortDirection = signal<SortDirection>(DEFAULT_SORT_DIRECTION);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly hasPreviousPage = signal(false);
  protected readonly hasNextPage = signal(false);

  constructor() {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => this.handleQueryParams(params));
  }

  protected reload(): void {
    this.loadInvoices();
  }

  protected onPageChange(page: number): void {
    if (this.loading()) {
      return;
    }
    this.navigateToPage(page, this.pageSize(), this.sortBy(), this.sortDirection(), false);
  }

  protected onPageSizeChange(size: number): void {
    if (this.loading()) {
      return;
    }
    this.navigateToPage(DEFAULT_PAGE_NUMBER, size, this.sortBy(), this.sortDirection(), false);
  }

  /**
   * Clicking a different column selects it with ascending order; clicking
   * the already-active column flips its direction. Either way the listing
   * returns to page 1 (preserving `pageSize`) and the URL is updated.
   */
  protected changeSort(field: InvoiceSortField): void {
    if (this.loading()) {
      return;
    }

    const nextDirection: SortDirection =
      field === this.sortBy() ? toggleSortDirection(this.sortDirection()) : 'asc';

    this.navigateToPage(DEFAULT_PAGE_NUMBER, this.pageSize(), field, nextDirection, false);
  }

  protected ariaSortFor(field: InvoiceSortField): 'ascending' | 'descending' | null {
    if (this.sortBy() !== field) {
      return null;
    }
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }

  protected sortIndicator(field: InvoiceSortField): string {
    if (this.sortBy() !== field) {
      return '';
    }
    return this.sortDirection() === 'asc' ? '▲' : '▼';
  }

  /**
   * Opens the invoice detail route. Used by both click and keyboard
   * (Enter/Space) row activation. The current listing query params travel
   * along as router navigation state (not the URL itself, so the detail
   * route stays a plain `/notas/:id`), so the "Voltar para a listagem" link
   * on the detail page can restore this exact page/size/sort.
   */
  protected openInvoice(id: number): void {
    this.router.navigate(['/notas', id], {
      state: { listQueryParams: { ...this.route.snapshot.queryParams } },
    });
  }

  protected onRowKeydown(event: KeyboardEvent, id: number): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openInvoice(id);
    }
  }

  protected openCreateDialog(): void {
    const dialogRef = this.dialog.open(InvoiceFormComponent, {
      autoFocus: '.invoice-form__add-item button',
      restoreFocus: true,
      panelClass: 'invoice-form-dialog-panel',
      width: '860px',
      maxWidth: '95vw',
      ariaLabelledBy: 'invoice-form-dialog-title',
    });

    dialogRef
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((invoice) => {
        if (!invoice) {
          return;
        }

        // Server-side pagination + the currently active sort means the newly
        // created invoice may not belong on the currently viewed page:
        // reload from page 1, keeping the current sort, so it (and the
        // updated totalCount) are reflected.
        if (this.pageNumber() === DEFAULT_PAGE_NUMBER) {
          this.loadInvoices();
        } else {
          this.navigateToPage(
            DEFAULT_PAGE_NUMBER,
            this.pageSize(),
            this.sortBy(),
            this.sortDirection(),
            false,
          );
        }
      });
  }

  private handleQueryParams(params: ParamMap): void {
    const rawPage = params.get('page');
    const rawSize = params.get('pageSize');
    const page = Number(rawPage);
    const size = Number(rawSize);

    const isValidPage = rawPage !== null && Number.isInteger(page) && page >= 1;
    const isValidSize = rawSize !== null && (PAGE_SIZE_OPTIONS as readonly number[]).includes(size);

    const sort = resolveSort(
      params.get('sortBy'),
      params.get('sortDirection'),
      INVOICE_SORT_FIELDS,
    );

    if (!isValidPage || !isValidSize || !sort) {
      this.navigateToPage(
        DEFAULT_PAGE_NUMBER,
        DEFAULT_PAGE_SIZE,
        DEFAULT_INVOICE_SORT_FIELD,
        DEFAULT_SORT_DIRECTION,
        true,
      );
      return;
    }

    this.pageNumber.set(page);
    this.pageSize.set(size);
    this.sortBy.set(sort.sortBy);
    this.sortDirection.set(sort.sortDirection);
    this.loadInvoices();
  }

  private navigateToPage(
    page: number,
    size: number,
    sortBy: InvoiceSortField,
    sortDirection: SortDirection,
    replaceUrl: boolean,
  ): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page, pageSize: size, sortBy, sortDirection },
      replaceUrl,
    });
  }

  private loadInvoices(): void {
    this.loading.set(true);
    this.listError.set(null);

    this.invoicesService
      .getPaged(this.pageNumber(), this.pageSize(), this.sortBy(), this.sortDirection())
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          this.loadErrorNotified = false;
          this.applyPage(response);
        },
        error: (error: HttpErrorResponse) => this.handleLoadError(error),
      });
  }

  private handleLoadError(error: HttpErrorResponse): void {
    const message = getUserFriendlyErrorMessage(error, 'invoice-list');
    this.listError.set(message);

    if (!this.loadErrorNotified) {
      this.loadErrorNotified = true;
      this.notification.error(message);
    }
  }

  private applyPage(response: PagedResponse<InvoiceSummary>): void {
    this.invoices.set(response.items);
    this.totalCount.set(response.totalCount);
    this.totalPages.set(response.totalPages);
    this.hasPreviousPage.set(response.hasPreviousPage);
    this.hasNextPage.set(response.hasNextPage);

    if (
      response.items.length === 0 &&
      response.totalCount > 0 &&
      response.pageNumber > response.totalPages
    ) {
      this.navigateToPage(
        response.totalPages,
        this.pageSize(),
        this.sortBy(),
        this.sortDirection(),
        true,
      );
    }
  }
}
