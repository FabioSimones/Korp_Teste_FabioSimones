import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Pagination } from './pagination';

describe('Pagination', () => {
  let fixture: ComponentFixture<Pagination>;
  let component: Pagination;

  function setInputs(overrides: Partial<Pagination> = {}): void {
    TestBed.configureTestingModule({ imports: [Pagination] });
    fixture = TestBed.createComponent(Pagination);
    component = fixture.componentInstance;
    Object.assign(component, {
      pageNumber: 1,
      pageSize: 10,
      totalCount: 47,
      totalPages: 5,
      hasPreviousPage: false,
      hasNextPage: true,
      ...overrides,
    });
    fixture.detectChanges();
  }

  it('should render the accessible landmark and current-page range', () => {
    setInputs();

    const nav: HTMLElement = fixture.nativeElement.querySelector('nav');
    expect(nav.getAttribute('aria-label')).toBe('Paginação');

    const summary: HTMLElement = fixture.nativeElement.querySelector('.pagination__summary');
    expect(summary.getAttribute('aria-live')).toBe('polite');
    expect(summary.textContent).toContain('1–10 de 47');
    expect(summary.textContent).toContain('Página 1 de 5');
  });

  it('should not render an items-per-page select (moved to PageSizeSelect)', () => {
    setInputs();

    expect(fixture.nativeElement.querySelector('select')).toBeNull();
  });

  it('should disable the previous button on the first page', () => {
    setInputs({ pageNumber: 1, hasPreviousPage: false, hasNextPage: true });

    const [previous, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(false);
  });

  it('should disable the next button on the last page', () => {
    setInputs({ pageNumber: 5, hasPreviousPage: true, hasNextPage: false });

    const [previous, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    expect(previous.disabled).toBe(false);
    expect(next.disabled).toBe(true);
  });

  it('should handle an empty result set without errors', () => {
    setInputs({
      pageNumber: 1,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    const summary: HTMLElement = fixture.nativeElement.querySelector('.pagination__summary');
    expect(summary.textContent).toContain('Nenhum resultado encontrado.');

    const [previous, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(true);
  });

  it('should emit pageChange with the next page number', () => {
    setInputs({ pageNumber: 2, hasPreviousPage: true, hasNextPage: true });
    const emitted: number[] = [];
    component.pageChange.subscribe((value) => emitted.push(value));

    const [, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    next.click();

    expect(emitted).toEqual([3]);
  });

  it('should emit pageChange with the previous page number', () => {
    setInputs({ pageNumber: 2, hasPreviousPage: true, hasNextPage: true });
    const emitted: number[] = [];
    component.pageChange.subscribe((value) => emitted.push(value));

    const [previous] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    previous.click();

    expect(emitted).toEqual([1]);
  });

  it('should not emit pageChange when the previous/next buttons are disabled', () => {
    setInputs({ pageNumber: 1, hasPreviousPage: false, hasNextPage: false });
    const emitted: number[] = [];
    component.pageChange.subscribe((value) => emitted.push(value));

    const [previous, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    previous.click();
    next.click();

    expect(emitted).toEqual([]);
  });

  it('should disable the buttons while loading', () => {
    setInputs({ loading: true, hasPreviousPage: true, hasNextPage: true });

    const [previous, next] = fixture.nativeElement.querySelectorAll(
      '.pagination__button',
    ) as NodeListOf<HTMLButtonElement>;
    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(true);
  });

  describe('numbered page buttons', () => {
    function pageButtons(): HTMLButtonElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('.pagination__page'));
    }

    function ellipses(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('.pagination__ellipsis'));
    }

    it('should render a button per page and no ellipsis when the total fits', () => {
      setInputs({
        pageNumber: 2,
        totalCount: 40,
        totalPages: 4,
        hasPreviousPage: true,
        hasNextPage: true,
      });

      expect(pageButtons().map((b) => b.textContent?.trim())).toEqual(['1', '2', '3', '4']);
      expect(ellipses().length).toBe(0);
    });

    it('should collapse a long range into first/last plus a window around the current page', () => {
      setInputs({
        pageNumber: 10,
        pageSize: 5,
        totalCount: 100,
        totalPages: 20,
        hasPreviousPage: true,
        hasNextPage: true,
      });

      expect(pageButtons().map((b) => b.textContent?.trim())).toEqual(['1', '9', '10', '11', '20']);
      expect(ellipses().length).toBe(2);
    });

    it('should render nothing (no page buttons) for an empty result set', () => {
      setInputs({
        pageNumber: 1,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
      });

      expect(pageButtons().length).toBe(0);
      expect(ellipses().length).toBe(0);
    });

    it('should mark the current page with aria-current and a distinct disabled state, without emitting pageChange', () => {
      setInputs({
        pageNumber: 2,
        totalCount: 40,
        totalPages: 4,
        hasPreviousPage: true,
        hasNextPage: true,
      });
      const emitted: number[] = [];
      component.pageChange.subscribe((value) => emitted.push(value));

      const current = pageButtons().find((b) => b.textContent?.trim() === '2')!;
      expect(current.getAttribute('aria-current')).toBe('page');
      expect(current.disabled).toBe(true);

      current.click();
      expect(emitted).toEqual([]);
    });

    it('should leave non-current pages without aria-current', () => {
      setInputs({
        pageNumber: 2,
        totalCount: 40,
        totalPages: 4,
        hasPreviousPage: true,
        hasNextPage: true,
      });

      const other = pageButtons().find((b) => b.textContent?.trim() === '3')!;
      expect(other.hasAttribute('aria-current')).toBe(false);
    });

    it('should emit pageChange with the clicked page number', () => {
      setInputs({
        pageNumber: 2,
        totalCount: 40,
        totalPages: 4,
        hasPreviousPage: true,
        hasNextPage: true,
      });
      const emitted: number[] = [];
      component.pageChange.subscribe((value) => emitted.push(value));

      const target = pageButtons().find((b) => b.textContent?.trim() === '3')!;
      target.click();

      expect(emitted).toEqual([3]);
    });

    it('should disable every page button while loading', () => {
      setInputs({
        pageNumber: 2,
        totalCount: 40,
        totalPages: 4,
        hasPreviousPage: true,
        hasNextPage: true,
        loading: true,
      });

      expect(pageButtons().every((b) => b.disabled)).toBe(true);
    });

    it('should not render the ellipsis as a focusable/clickable element', () => {
      setInputs({
        pageNumber: 10,
        totalCount: 100,
        totalPages: 20,
        hasPreviousPage: true,
        hasNextPage: true,
      });

      const [ellipsis] = ellipses();
      expect(ellipsis.tagName).not.toBe('BUTTON');
      expect(ellipsis.getAttribute('aria-hidden')).toBe('true');
    });
  });
});
