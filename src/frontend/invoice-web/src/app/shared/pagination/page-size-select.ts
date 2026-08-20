import { Component, EventEmitter, Input, Output } from '@angular/core';

import { PAGE_SIZE_OPTIONS } from './paged-response';

/**
 * Reusable "items per page" selector, shared by the Products and Invoices
 * listing toolbars. Placed above the table/list it controls (never below —
 * see `docs/technical-details.md`, "Paginação server-side"). Purely
 * presentational: the host page owns the current `pageSize` state and reacts
 * to `pageSizeChange`.
 *
 * Each `<option>` binds `[selected]` explicitly instead of relying on the
 * parent `<select>`'s `[value]` binding. Native `<select>` elements combined
 * with `@for`-generated `<option>`s can otherwise select the browser's
 * default (first) option on initial render, before Angular has finished
 * matching the bound value against its (not-yet-existing) children.
 */
@Component({
  selector: 'app-page-size-select',
  standalone: true,
  templateUrl: './page-size-select.html',
  styleUrl: './page-size-select.scss',
})
export class PageSizeSelect {
  @Input({ required: true }) pageSize!: number;
  /** While `true`, the select is disabled to avoid overlapping requests. */
  @Input() loading = false;

  @Output() readonly pageSizeChange = new EventEmitter<number>();

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  protected onPageSizeChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (!Number.isNaN(value) && value !== this.pageSize) {
      this.pageSizeChange.emit(value);
    }
  }
}
