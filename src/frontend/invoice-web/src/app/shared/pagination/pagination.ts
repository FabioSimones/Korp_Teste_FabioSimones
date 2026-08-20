import { Component, EventEmitter, Input, Output } from '@angular/core';

import { PageItem, buildPageItems } from './page-items';

/**
 * Pagination navigation shared by the Products and Invoices listing
 * toolbars: the current-page range summary, numbered page buttons (with
 * ellipses for long ranges, see `buildPageItems`) and the Previous/Next
 * buttons. Purely presentational: it never performs HTTP requests and knows
 * nothing about `Product`/`Invoice` — the host page owns the data fetching
 * and simply feeds the current page metadata in, reacting to the
 * `pageChange` output. Placed above the table/list it controls (never below
 * — see `docs/technical-details.md`, "Paginação server-side").
 *
 * The "items per page" selector lives separately in `PageSizeSelect`, placed
 * alongside this component in the same toolbar.
 */
@Component({
  selector: 'app-pagination',
  standalone: true,
  templateUrl: './pagination.html',
  styleUrl: './pagination.scss',
})
export class Pagination {
  @Input({ required: true }) pageNumber!: number;
  @Input({ required: true }) pageSize!: number;
  @Input({ required: true }) totalCount!: number;
  @Input({ required: true }) totalPages!: number;
  @Input({ required: true }) hasPreviousPage!: boolean;
  @Input({ required: true }) hasNextPage!: boolean;
  /** While `true`, every navigation control is disabled to avoid overlapping requests. */
  @Input() loading = false;

  @Output() readonly pageChange = new EventEmitter<number>();

  protected get pageItems(): PageItem[] {
    return buildPageItems(this.pageNumber, this.displayTotalPages);
  }

  protected isEllipsis(item: PageItem): item is 'ellipsis' {
    return item === 'ellipsis';
  }

  protected isCurrentPage(item: PageItem): boolean {
    return item === this.pageNumber;
  }

  /**
   * Clicking the current page's own button is a no-op: it never emits
   * `pageChange`, avoiding a redundant reload of data already on screen.
   */
  protected goToPage(page: number): void {
    if (this.loading || page === this.pageNumber) {
      return;
    }
    this.pageChange.emit(page);
  }

  protected get rangeStart(): number {
    return this.totalCount === 0 ? 0 : (this.pageNumber - 1) * this.pageSize + 1;
  }

  protected get rangeEnd(): number {
    return Math.min(this.pageNumber * this.pageSize, this.totalCount);
  }

  protected get displayTotalPages(): number {
    return Math.max(this.totalPages, this.totalCount === 0 ? 0 : 1);
  }

  protected get previousDisabled(): boolean {
    return this.loading || !this.hasPreviousPage;
  }

  protected get nextDisabled(): boolean {
    return this.loading || !this.hasNextPage;
  }

  protected goToPreviousPage(): void {
    if (this.previousDisabled) {
      return;
    }
    this.pageChange.emit(this.pageNumber - 1);
  }

  protected goToNextPage(): void {
    if (this.nextDisabled) {
      return;
    }
    this.pageChange.emit(this.pageNumber + 1);
  }
}
