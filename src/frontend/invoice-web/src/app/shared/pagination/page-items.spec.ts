import { buildPageItems } from './page-items';

describe('buildPageItems', () => {
  it('should return no items when there are no pages', () => {
    expect(buildPageItems(1, 0)).toEqual([]);
  });

  it('should return only page 1 when there is a single page', () => {
    expect(buildPageItems(1, 1)).toEqual([1]);
  });

  it('should show every page number when the total fits without an ellipsis', () => {
    expect(buildPageItems(1, 4)).toEqual([1, 2, 3, 4]);
    expect(buildPageItems(2, 4)).toEqual([1, 2, 3, 4]);
    expect(buildPageItems(4, 4)).toEqual([1, 2, 3, 4]);
  });

  it('should show 1 2 3 … 20 on the first page of a long range', () => {
    expect(buildPageItems(1, 20)).toEqual([1, 2, 3, 'ellipsis', 20]);
  });

  it('should show 1 … 9 10 11 … 20 in the middle of a long range', () => {
    expect(buildPageItems(10, 20)).toEqual([1, 'ellipsis', 9, 10, 11, 'ellipsis', 20]);
  });

  it('should show 1 … 18 19 20 on the last page of a long range', () => {
    expect(buildPageItems(20, 20)).toEqual([1, 'ellipsis', 18, 19, 20]);
  });

  it('should fill a single-page gap instead of using an ellipsis for it', () => {
    // Gap between the window and a boundary is exactly one page (page 5),
    // so it is shown directly rather than collapsed behind '...'.
    expect(buildPageItems(3, 6)).toEqual([1, 2, 3, 4, 5, 6]);
  });

  it('should never render more than a small, bounded number of items for very large page counts', () => {
    const items = buildPageItems(500, 1000);
    // 1, ellipsis, 499, 500, 501, ellipsis, 1000 => 7 items regardless of totalPages.
    expect(items.length).toBe(7);
    expect(items).toEqual([1, 'ellipsis', 499, 500, 501, 'ellipsis', 1000]);
  });

  it('should clamp the sliding window at the start (currentPage before the first page)', () => {
    // Defensive: an out-of-range currentPage should not throw or produce
    // negative/duplicate page numbers.
    expect(buildPageItems(0, 10)).toEqual([1, 2, 3, 'ellipsis', 10]);
  });
});
