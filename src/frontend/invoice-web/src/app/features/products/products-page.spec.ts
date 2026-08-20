import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Observable, Subject, of, throwError } from 'rxjs';

import { NotificationService } from '../../core/services/notification.service';
import { PagedResponse } from '../../shared/pagination/paged-response';
import { Product } from './models/product';
import { ProductsPage } from './products-page';
import { ProductsService } from './products.service';

interface ProductsServiceStub {
  getPaged: ReturnType<typeof vi.fn>;
  create: ReturnType<typeof vi.fn>;
}

describe('ProductsPage', () => {
  let harness: RouterTestingHarness;
  let productsService: ProductsServiceStub;
  let location: Location;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const sampleProducts: Product[] = [
    { id: 1, code: 'A1', description: 'Produto A', balance: 10 },
    { id: 2, code: 'B2', description: 'Produto B', balance: 5 },
  ];

  function pageOf(
    items: Product[],
    overrides: Partial<PagedResponse<Product>> = {},
  ): PagedResponse<Product> {
    return {
      items,
      pageNumber: 1,
      pageSize: 10,
      totalCount: items.length,
      totalPages: items.length === 0 ? 0 : 1,
      hasPreviousPage: false,
      hasNextPage: false,
      ...overrides,
    };
  }

  async function setup(
    getPagedResult: Observable<PagedResponse<Product>> = of(pageOf(sampleProducts)),
    initialUrl = '/produtos',
  ): Promise<void> {
    productsService = {
      getPaged: vi.fn().mockReturnValue(getPagedResult),
      create: vi.fn(),
    };
    notification = { success: vi.fn(), error: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'produtos', component: ProductsPage }]),
        provideNoopAnimations(),
        { provide: ProductsService, useValue: productsService },
        { provide: NotificationService, useValue: notification },
      ],
    });

    location = TestBed.inject(Location);
    harness = await RouterTestingHarness.create(initialUrl);
    harness.detectChanges();
  }

  function el(): HTMLElement {
    return harness.routeNativeElement!;
  }

  function newProductButton(): HTMLButtonElement {
    return el().querySelector('.products-page__header button')!;
  }

  function sortButton(field: 'code' | 'description' | 'balance'): HTMLButtonElement {
    const labels: Record<typeof field, string> = {
      code: 'Código',
      description: 'Descrição',
      balance: 'Saldo',
    };
    return Array.from(el().querySelectorAll<HTMLButtonElement>('.data-table__sort-button')).find(
      (button) => button.textContent?.trim().startsWith(labels[field]),
    )!;
  }

  function columnHeader(field: 'code' | 'description' | 'balance'): HTMLElement {
    return sortButton(field).closest('th')!;
  }

  async function openDialog(): Promise<void> {
    const button = newProductButton();
    button.focus();
    button.click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function dialogEl(): HTMLElement {
    return document.querySelector('.product-form-dialog') as HTMLElement;
  }

  function setInputValue(selector: string, value: string): void {
    const input: HTMLInputElement = dialogEl().querySelector(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function fillForm(code: string, description: string, balance: string): void {
    setInputValue('input[formcontrolname="code"]', code);
    setInputValue('input[formcontrolname="description"]', description);
    setInputValue('input[formcontrolname="balance"]', balance);
    harness.detectChanges();
  }

  async function submitDialogForm(): Promise<void> {
    const form: HTMLFormElement = dialogEl().querySelector('form')!;
    form.dispatchEvent(new Event('submit'));
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  it('should render the page title', async () => {
    await setup(of(pageOf([])));

    const title = el().querySelector('#products-title');
    expect(title?.textContent?.trim()).toBe('Produtos');
  });

  it('should not expose the registration form permanently on the page', async () => {
    await setup(of(pageOf([])));

    expect(el().querySelector('form')).toBeFalsy();
    expect(el().querySelector('input[formcontrolname="code"]')).toBeFalsy();
    expect(newProductButton().textContent).toContain('Novo produto');
  });

  it('should show a loading indicator while fetching the product list', async () => {
    const pending = new Subject<PagedResponse<Product>>();
    await setup(pending.asObservable());

    expect(el().querySelector('[role="status"]')).toBeTruthy();
  });

  it('should render the empty state when there are no products', async () => {
    await setup(of(pageOf([])));

    const empty = el().querySelector('.products-page__empty');
    expect(empty?.textContent).toContain('Nenhum produto cadastrado');
    expect(el().querySelector('table')).toBeFalsy();
  });

  it('should render a table row for each registered product and default to page 1/size 5, sorted by code asc', async () => {
    await setup(of(pageOf(sampleProducts)));

    const rows = el().querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(el().textContent).toContain('A1');
    expect(el().textContent).toContain('Produto B');
    expect(productsService.getPaged).toHaveBeenCalledWith(1, 5, 'code', 'asc');
  });

  it('should normalize a URL without query params to page=1/pageSize=5/sortBy=code/sortDirection=asc, using replaceUrl', async () => {
    await setup(of(pageOf(sampleProducts)), '/produtos');

    expect(productsService.getPaged).toHaveBeenCalledTimes(1);
    expect(productsService.getPaged).toHaveBeenCalledWith(1, 5, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
  });

  it('should reflect the effective pageSize (not a stray default) in the page-size select', async () => {
    await setup(
      of(pageOf(sampleProducts)),
      '/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc',
    );

    expect(productsService.getPaged).toHaveBeenCalledWith(1, 10, 'code', 'asc');
    const select: HTMLSelectElement = el().querySelector('select')!;
    expect(select.value).toBe('10');
  });

  it('should place the page-size select and pagination before the table, never duplicating them after it', async () => {
    await setup(of(pageOf(sampleProducts)));

    const select = el().querySelector('select');
    const pagination = el().querySelector('app-pagination, .pagination');
    const table = el().querySelector('table');
    expect(select).toBeTruthy();
    expect(pagination).toBeTruthy();
    expect(table).toBeTruthy();

    // DOCUMENT_POSITION_FOLLOWING (4): table comes after select/pagination in the DOM.
    expect(select!.compareDocumentPosition(table!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(
      pagination!.compareDocumentPosition(table!) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(el().querySelectorAll('select').length).toBe(1);
    expect(el().querySelectorAll('.pagination').length).toBe(1);
  });

  it('should load the page indicated by valid query params directly from the URL', async () => {
    await setup(
      of(
        pageOf(sampleProducts, {
          pageNumber: 2,
          pageSize: 25,
          totalCount: 30,
          totalPages: 2,
          hasPreviousPage: true,
          hasNextPage: false,
        }),
      ),
      '/produtos?page=2&pageSize=25&sortBy=balance&sortDirection=desc',
    );

    expect(productsService.getPaged).toHaveBeenCalledWith(2, 25, 'balance', 'desc');
    expect(location.path()).toBe('/produtos?page=2&pageSize=25&sortBy=balance&sortDirection=desc');
  });

  it('should normalize invalid query params (non-numeric/out-of-range) to the defaults without looping', async () => {
    await setup(of(pageOf(sampleProducts)), '/produtos?page=abc&pageSize=999');

    expect(productsService.getPaged).toHaveBeenCalledTimes(1);
    expect(productsService.getPaged).toHaveBeenCalledWith(1, 5, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
  });

  it('should normalize invalid sortBy/sortDirection to the defaults without looping', async () => {
    await setup(
      of(pageOf(sampleProducts)),
      '/produtos?page=2&pageSize=10&sortBy=unknown&sortDirection=up',
    );

    expect(productsService.getPaged).toHaveBeenCalledTimes(1);
    expect(productsService.getPaged).toHaveBeenCalledWith(1, 5, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
  });

  it('should request the next page and update the URL when "Próxima" is clicked', async () => {
    await setup(
      of(
        pageOf(sampleProducts, {
          pageNumber: 1,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: false,
          hasNextPage: true,
        }),
      ),
      '/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc',
    );

    const nextPage = pageOf(sampleProducts, {
      pageNumber: 2,
      pageSize: 10,
      totalCount: 25,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    });
    productsService.getPaged.mockReturnValue(of(nextPage));

    const buttons = el().querySelectorAll('.pagination__button');
    const next = buttons[1] as HTMLButtonElement;
    next.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(productsService.getPaged).toHaveBeenCalledWith(2, 10, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=2&pageSize=10&sortBy=code&sortDirection=asc');
  });

  it('should request the previous page when "Anterior" is clicked', async () => {
    await setup(
      of(
        pageOf(sampleProducts, {
          pageNumber: 2,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: true,
          hasNextPage: true,
        }),
      ),
      '/produtos?page=2&pageSize=10&sortBy=code&sortDirection=asc',
    );

    const previousPage = pageOf(sampleProducts, {
      pageNumber: 1,
      pageSize: 10,
      totalCount: 25,
      totalPages: 3,
      hasPreviousPage: false,
      hasNextPage: true,
    });
    productsService.getPaged.mockReturnValue(of(previousPage));

    const buttons = el().querySelectorAll('.pagination__button');
    const previous = buttons[0] as HTMLButtonElement;
    previous.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(productsService.getPaged).toHaveBeenCalledWith(1, 10, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc');
  });

  it('should change the page size and return to page 1, preserving the active sort', async () => {
    await setup(
      of(
        pageOf(sampleProducts, {
          pageNumber: 2,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: true,
          hasNextPage: true,
        }),
      ),
      '/produtos?page=2&pageSize=10&sortBy=description&sortDirection=desc',
    );

    const resizedPage = pageOf(sampleProducts, {
      pageNumber: 1,
      pageSize: 25,
      totalCount: 25,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });
    productsService.getPaged.mockReturnValue(of(resizedPage));

    const select: HTMLSelectElement = el().querySelector('select')!;
    select.value = '25';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(productsService.getPaged).toHaveBeenCalledWith(1, 25, 'description', 'desc');
    expect(location.path()).toBe(
      '/produtos?page=1&pageSize=25&sortBy=description&sortDirection=desc',
    );
  });

  it('should toggle page size 5 -> 10 -> 5, each change resolving in a single request/URL update', async () => {
    await setup(
      of(pageOf(sampleProducts)),
      '/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc',
    );

    expect(productsService.getPaged).toHaveBeenCalledTimes(1);
    expect(productsService.getPaged).toHaveBeenNthCalledWith(1, 1, 5, 'code', 'asc');

    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 10 })),
    );
    let select: HTMLSelectElement = el().querySelector('select')!;
    select.value = '10';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(productsService.getPaged).toHaveBeenCalledTimes(2);
    expect(productsService.getPaged).toHaveBeenNthCalledWith(2, 1, 10, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc');
    select = el().querySelector('select')!;
    expect(select.value).toBe('10');

    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 5 })),
    );
    select.value = '5';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(productsService.getPaged).toHaveBeenCalledTimes(3);
    expect(productsService.getPaged).toHaveBeenNthCalledWith(3, 1, 5, 'code', 'asc');
    expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
    select = el().querySelector('select')!;
    expect(select.value).toBe('5');
  });

  it('should restore the requested page/size on refresh (fresh navigation to the same URL)', async () => {
    await setup(
      of(pageOf(sampleProducts, { pageNumber: 2, pageSize: 25, totalCount: 30, totalPages: 2 })),
      '/produtos?page=2&pageSize=25&sortBy=code&sortDirection=asc',
    );

    expect(productsService.getPaged).toHaveBeenCalledWith(2, 25, 'code', 'asc');
    const select: HTMLSelectElement = el().querySelector('select')!;
    expect(select.value).toBe('25');
  });

  it('should restore the select/URL/listing when the browser navigates to a previous/next history entry', async () => {
    // Simulates back/forward: from the component's perspective, a browser
    // history navigation is indistinguishable from any other navigation to a
    // URL with different query params — both surface as a new
    // `ActivatedRoute.queryParamMap` emission, which is exactly what
    // `handleQueryParams` reacts to.
    await setup(
      of(pageOf(sampleProducts)),
      '/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc',
    );

    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 25 })),
    );
    await harness.navigateByUrl('/produtos?page=1&pageSize=25&sortBy=code&sortDirection=asc');
    harness.detectChanges();
    await harness.fixture.whenStable();
    expect(location.path()).toBe('/produtos?page=1&pageSize=25&sortBy=code&sortDirection=asc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('25');

    // "Back": returning to the previously visited URL.
    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 5 })),
    );
    await harness.navigateByUrl('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('5');

    // "Forward": returning to the later-visited URL.
    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 25 })),
    );
    await harness.navigateByUrl('/produtos?page=1&pageSize=25&sortBy=code&sortDirection=asc');
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(location.path()).toBe('/produtos?page=1&pageSize=25&sortBy=code&sortDirection=asc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('25');
  });

  it('should show an inline error state (with a retry action) and a single toast on the first load failure', async () => {
    await setup(
      throwError(() => new HttpErrorResponse({ status: 503 })),
      '/produtos?page=2&pageSize=25&sortBy=code&sortDirection=asc',
    );

    const alert = el().querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('indisponível');
    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(
      'O serviço de estoque está temporariamente indisponível. Tente novamente em alguns instantes.',
    );

    const retryButton: HTMLButtonElement = el().querySelector('.products-page__state button')!;

    // Retrying while it keeps failing must not stack a second toast.
    retryButton.click();
    harness.detectChanges();
    expect(notification.error).toHaveBeenCalledTimes(1);

    productsService.getPaged.mockReturnValue(
      of(pageOf(sampleProducts, { pageNumber: 2, pageSize: 25 })),
    );
    retryButton.click();
    harness.detectChanges();

    expect(productsService.getPaged).toHaveBeenNthCalledWith(3, 2, 25, 'code', 'asc');
    expect(el().querySelectorAll('tbody tr').length).toBe(2);
  });

  it('should show a friendly message and not break the screen when the URL is manually tampered with an invalid sortBy (INVALID_SORT)', async () => {
    // The frontend always normalizes sortBy/sortDirection before sending a
    // request (see the two normalization tests above), so a 400
    // INVALID_SORT from the backend can only happen via direct URL/API
    // manipulation bypassing the app. Simulated here by making the (already
    // normalized) request itself fail with that errorCode.
    await setup(
      throwError(
        () => new HttpErrorResponse({ status: 400, error: { errorCode: 'INVALID_SORT' } }),
      ),
    );

    const alert = el().querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Confira os dados informados');
  });

  describe('sortable column headers', () => {
    it('should mark the default sort column (Código, ascending) with aria-sort and a ▲ indicator', async () => {
      await setup(of(pageOf(sampleProducts)));

      expect(columnHeader('code').getAttribute('aria-sort')).toBe('ascending');
      expect(sortButton('code').textContent).toContain('▲');
      expect(columnHeader('description').hasAttribute('aria-sort')).toBe(false);
      expect(columnHeader('balance').hasAttribute('aria-sort')).toBe(false);
    });

    it('should select a new column ascending and reset to page 1, preserving pageSize', async () => {
      await setup(
        of(pageOf(sampleProducts, { pageNumber: 2, pageSize: 10, totalCount: 25, totalPages: 3 })),
        '/produtos?page=2&pageSize=10&sortBy=code&sortDirection=asc',
      );

      productsService.getPaged.mockReturnValue(
        of(pageOf(sampleProducts, { pageNumber: 1, pageSize: 10 })),
      );
      sortButton('balance').click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(productsService.getPaged).toHaveBeenLastCalledWith(1, 10, 'balance', 'asc');
      expect(location.path()).toBe('/produtos?page=1&pageSize=10&sortBy=balance&sortDirection=asc');
      expect(columnHeader('balance').getAttribute('aria-sort')).toBe('ascending');
    });

    it('should flip asc -> desc on a second click of the same column', async () => {
      await setup(
        of(pageOf(sampleProducts)),
        '/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc',
      );

      productsService.getPaged.mockReturnValue(of(pageOf(sampleProducts)));
      sortButton('code').click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(productsService.getPaged).toHaveBeenLastCalledWith(1, 5, 'code', 'desc');
      expect(location.path()).toBe('/produtos?page=1&pageSize=5&sortBy=code&sortDirection=desc');
      expect(columnHeader('code').getAttribute('aria-sort')).toBe('descending');
      expect(sortButton('code').textContent).toContain('▼');
    });

    it('should read the active sort directly from the URL on initial load', async () => {
      await setup(
        of(pageOf(sampleProducts)),
        '/produtos?page=1&pageSize=5&sortBy=description&sortDirection=desc',
      );

      expect(productsService.getPaged).toHaveBeenCalledWith(1, 5, 'description', 'desc');
      expect(columnHeader('description').getAttribute('aria-sort')).toBe('descending');
    });
  });

  describe('column alignment', () => {
    it('should mark the Saldo header and every Saldo value cell with the same numeric column class', async () => {
      await setup(of(pageOf(sampleProducts)));

      expect(columnHeader('balance').classList).toContain('data-table__col-numeric');

      const balanceCells: HTMLTableCellElement[] = Array.from(
        el().querySelectorAll('tbody td.data-table__col-numeric'),
      );
      expect(balanceCells.length).toBe(sampleProducts.length);
      balanceCells.forEach((cell, index) =>
        expect(cell.textContent?.trim()).toBe(String(sampleProducts[index].balance)),
      );
    });

    it('should mark the Código and Descrição headers with the text column class', async () => {
      await setup(of(pageOf(sampleProducts)));

      expect(columnHeader('code').classList).toContain('data-table__col-text');
      expect(columnHeader('description').classList).toContain('data-table__col-text');
    });
  });

  describe('registration dialog', () => {
    it('should open the dialog with the registration form when "+ Novo produto" is clicked', async () => {
      await setup(of(pageOf([])));

      await openDialog();

      expect(dialogEl()).toBeTruthy();
      expect(dialogEl().querySelector('#product-form-dialog-title')?.textContent?.trim()).toBe(
        'Cadastrar produto',
      );
      expect(dialogEl().querySelector('input[formcontrolname="code"]')).toBeTruthy();
      expect(productsService.create).not.toHaveBeenCalled();
    });

    it('should not create a product and leave the listing/pagination unchanged when cancelled', async () => {
      await setup(of(pageOf([])), '/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc');
      expect(productsService.getPaged).toHaveBeenCalledTimes(1);

      await openDialog();
      fillForm('C3', 'Produto C', '7');

      const cancelButton = Array.from(dialogEl().querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cancelar',
      ) as HTMLButtonElement;
      cancelButton.click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(productsService.create).not.toHaveBeenCalled();
      expect(productsService.getPaged).toHaveBeenCalledTimes(1);
      expect(location.path()).toBe('/produtos?page=1&pageSize=10&sortBy=code&sortDirection=asc');
      expect(document.querySelector('.product-form-dialog')).toBeFalsy();
    });

    it('should return focus to the "+ Novo produto" button after cancelling', async () => {
      await setup(of(pageOf([])));

      await openDialog();

      const cancelButton = Array.from(dialogEl().querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cancelar',
      ) as HTMLButtonElement;
      cancelButton.click();
      harness.detectChanges();
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(document.activeElement).toBe(newProductButton());
    });

    it('should create the product, close the dialog and reload page 1 preserving pageSize and the active sort on success', async () => {
      await setup(
        of(pageOf(sampleProducts, { pageNumber: 2, pageSize: 10, totalCount: 15, totalPages: 2 })),
        '/produtos?page=2&pageSize=10&sortBy=balance&sortDirection=desc',
      );

      const created: Product = { id: 10, code: 'C3', description: 'Produto C', balance: 7 };
      productsService.create.mockReturnValue(of(created));
      productsService.getPaged.mockReturnValue(of(pageOf([created], { pageSize: 10 })));

      await openDialog();
      fillForm('C3', 'Produto C', '7');
      await submitDialogForm();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(productsService.create).toHaveBeenCalledWith({
        code: 'C3',
        description: 'Produto C',
        balance: 7,
      });
      expect(document.querySelector('.product-form-dialog')).toBeFalsy();
      expect(location.path()).toBe(
        '/produtos?page=1&pageSize=10&sortBy=balance&sortDirection=desc',
      );
      expect(productsService.getPaged).toHaveBeenLastCalledWith(1, 10, 'balance', 'desc');
    });

    it('should keep the dialog open and flag the code field as duplicate on 409', async () => {
      await setup(of(pageOf([])));

      const error = new HttpErrorResponse({
        status: 409,
        error: { detail: "Product code 'C3' is already registered." },
      });
      productsService.create.mockReturnValue(throwError(() => error));

      await openDialog();
      fillForm('C3', 'Produto C', '7');
      await submitDialogForm();

      expect(dialogEl()).toBeTruthy();
      expect(dialogEl().textContent).toContain('Este código já está cadastrado.');
      expect(productsService.getPaged).toHaveBeenCalledTimes(1);
    });
  });
});
