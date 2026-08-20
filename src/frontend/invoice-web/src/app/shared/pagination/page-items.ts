/** A single slot rendered by `Pagination`'s page-number row: either a page number button or a non-interactive ellipsis. */
export type PageItem = number | 'ellipsis';

/**
 * Pure function computing which page-number buttons `Pagination` should
 * render for a given `currentPage`/`totalPages`, collapsing the middle of
 * long ranges into a single `'ellipsis'` slot. Kept free of any Angular/DOM
 * dependency so it is trivially unit-testable on its own.
 *
 * Rules:
 * - The first and last page are always included when they exist.
 * - The current page and its immediate neighbours are always included,
 *   sliding a fixed-width 3-page window so it stays centered on the current
 *   page and clamps at the edges (this is what keeps e.g. page 1 of 20
 *   showing `1 2 3 … 20` and page 20 of 20 showing `1 … 18 19 20`, rather
 *   than a sparse `1 2 … 20` / `1 … 19 20`).
 * - A gap of exactly one page between two shown numbers is filled with that
 *   page instead of an ellipsis (an ellipsis standing in for a single hidden
 *   page reads worse than just showing it).
 * - Larger gaps collapse to a single `'ellipsis'` entry.
 * - `totalPages <= 0` yields no items; `totalPages === 1` yields `[1]`.
 */
export function buildPageItems(currentPage: number, totalPages: number): PageItem[] {
  if (totalPages <= 0) {
    return [];
  }

  const windowStart = Math.max(1, Math.min(currentPage - 1, totalPages - 2));
  const windowPages = [windowStart, windowStart + 1, windowStart + 2].filter(
    (page) => page >= 1 && page <= totalPages,
  );

  const pages = [...new Set([1, totalPages, ...windowPages])].sort((a, b) => a - b);

  const items: PageItem[] = [];
  pages.forEach((page, index) => {
    if (index > 0) {
      const previous = pages[index - 1];
      const gap = page - previous;
      if (gap === 2) {
        items.push(previous + 1);
      } else if (gap > 2) {
        items.push('ellipsis');
      }
    }
    items.push(page);
  });

  return items;
}
