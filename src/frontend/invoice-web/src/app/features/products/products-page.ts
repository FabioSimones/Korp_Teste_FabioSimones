import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { getUserFriendlyErrorMessage } from '../../core/utils/http-error-messages';
import { NotificationService } from '../../core/services/notification.service';
import {
  DEFAULT_PAGE_NUMBER,
  DEFAULT_PAGE_SIZE,
  PAGE_SIZE_OPTIONS,
  PagedResponse,
} from '../../shared/pagination/paged-response';
import { PageSizeSelect } from '../../shared/pagination/page-size-select';
import { Pagination } from '../../shared/pagination/pagination';
import { SortDirection, resolveSort, toggleSortDirection } from '../../shared/pagination/sort';
import { ProductFormDialog } from './product-form-dialog/product-form-dialog';
import {
  DEFAULT_PRODUCT_SORT_FIELD,
  PRODUCT_SORT_FIELDS,
  Product,
  ProductSortField,
} from './models/product';
import { ProductsService } from './products.service';

const DEFAULT_SORT_DIRECTION: SortDirection = 'asc';

/**
 * Lists registered products (server-side paginated and sortable), consuming
 * Inventory.Api directly. Registration happens in a `ProductFormDialog`
 * opened from the "+ Novo produto" button. Editing, deletion, invoices and
 * printing are out of scope for this feature.
 */
@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageSizeSelect,
    Pagination,
  ],
  templateUrl: './products-page.html',
  styleUrl: './products-page.scss',
})
export class ProductsPage {
  private readonly productsService = inject(ProductsService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  // Tracks whether the current run of consecutive load failures has already
  // shown its one toast, so retrying (or paging) while still failing does
  // not spam a new toast on every attempt. Reset on the next successful
  // load, so a *new* failure streak later can notify again.
  private loadErrorNotified = false;

  protected readonly products = signal<Product[]>([]);
  protected readonly loading = signal(true);
  protected readonly listError = signal<string | null>(null);

  protected readonly pageNumber = signal(DEFAULT_PAGE_NUMBER);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly sortBy = signal<ProductSortField>(DEFAULT_PRODUCT_SORT_FIELD);
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
    this.loadProducts();
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
  protected changeSort(field: ProductSortField): void {
    if (this.loading()) {
      return;
    }

    const nextDirection: SortDirection =
      field === this.sortBy() ? toggleSortDirection(this.sortDirection()) : DEFAULT_SORT_DIRECTION;

    this.navigateToPage(DEFAULT_PAGE_NUMBER, this.pageSize(), field, nextDirection, false);
  }

  protected ariaSortFor(field: ProductSortField): 'ascending' | 'descending' | null {
    if (this.sortBy() !== field) {
      return null;
    }
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }

  protected sortIndicator(field: ProductSortField): string {
    if (this.sortBy() !== field) {
      return '';
    }
    return this.sortDirection() === 'asc' ? '▲' : '▼';
  }

  protected openCreateDialog(): void {
    const dialogRef = this.dialog.open(ProductFormDialog, {
      autoFocus: 'input[formcontrolname="code"]',
      restoreFocus: true,
      panelClass: 'product-form-dialog-panel',
      width: '640px',
      maxWidth: '95vw',
      ariaLabelledBy: 'product-form-dialog-title',
    });

    dialogRef
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((product) => {
        if (!product) {
          return;
        }

        // Server-side pagination means the freshly created product may not
        // belong on the currently viewed page: reload from the first page so
        // the listing (and its totalCount) reflects the new state, keeping
        // the currently active sort.
        if (this.pageNumber() === DEFAULT_PAGE_NUMBER) {
          this.loadProducts();
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
      PRODUCT_SORT_FIELDS,
    );

    if (!isValidPage || !isValidSize || !sort) {
      this.navigateToPage(
        DEFAULT_PAGE_NUMBER,
        DEFAULT_PAGE_SIZE,
        DEFAULT_PRODUCT_SORT_FIELD,
        DEFAULT_SORT_DIRECTION,
        true,
      );
      return;
    }

    this.pageNumber.set(page);
    this.pageSize.set(size);
    this.sortBy.set(sort.sortBy);
    this.sortDirection.set(sort.sortDirection);
    this.loadProducts();
  }

  private navigateToPage(
    page: number,
    size: number,
    sortBy: ProductSortField,
    sortDirection: SortDirection,
    replaceUrl: boolean,
  ): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page, pageSize: size, sortBy, sortDirection },
      replaceUrl,
    });
  }

  private loadProducts(): void {
    this.loading.set(true);
    this.listError.set(null);

    this.productsService
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
    const message = getUserFriendlyErrorMessage(error, 'product-list');
    this.listError.set(message);

    // The inline error state (with "Tentar novamente") is always shown;
    // the toast is a one-time nudge for the *first* failure in a streak,
    // to avoid stacking a new toast on every retry of the same failure.
    if (!this.loadErrorNotified) {
      this.loadErrorNotified = true;
      this.notification.error(message);
    }
  }

  private applyPage(response: PagedResponse<Product>): void {
    this.products.set(response.items);
    this.totalCount.set(response.totalCount);
    this.totalPages.set(response.totalPages);
    this.hasPreviousPage.set(response.hasPreviousPage);
    this.hasNextPage.set(response.hasNextPage);

    // If the current page became empty (e.g. the requested page was beyond
    // the actual total, or data shrank since the URL was built), fall back
    // to the last valid page instead of showing a misleading empty state.
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
