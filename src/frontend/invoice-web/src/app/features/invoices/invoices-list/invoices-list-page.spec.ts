import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Component, getDebugNode } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Observable, Subject, of, throwError } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { PagedResponse } from '../../../shared/pagination/paged-response';
import { Product } from '../../products/models/product';
import { ProductsService } from '../../products/products.service';
import { Invoice, InvoiceSummary } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoicesListPage } from './invoices-list-page';

@Component({ selector: 'app-fake-detail', standalone: true, template: '' })
class FakeInvoiceDetailPage {}

interface InvoicesServiceStub {
  getPaged: ReturnType<typeof vi.fn>;
  create: ReturnType<typeof vi.fn>;
}

interface ProductsServiceStub {
  getAll: ReturnType<typeof vi.fn>;
}

describe('InvoicesListPage', () => {
  let harness: RouterTestingHarness;
  let invoicesService: InvoicesServiceStub;
  let productsService: ProductsServiceStub;
  let location: Location;
  let router: Router;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const sampleInvoices: InvoiceSummary[] = [
    {
      id: 1,
      number: 1,
      status: 'Open',
      createdAtUtc: '2026-08-17T10:00:00Z',
      closedAtUtc: null,
      itemsCount: 2,
    },
    {
      id: 2,
      number: 2,
      status: 'Closed',
      createdAtUtc: '2026-08-17T11:00:00Z',
      closedAtUtc: '2026-08-17T11:05:00Z',
      itemsCount: 0,
    },
  ];

  const sampleProducts: Product[] = [{ id: 1, code: 'A1', description: 'Produto A', balance: 10 }];

  function pageOf(
    items: InvoiceSummary[],
    overrides: Partial<PagedResponse<InvoiceSummary>> = {},
  ): PagedResponse<InvoiceSummary> {
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
    getPagedResult: Observable<PagedResponse<InvoiceSummary>> = of(pageOf(sampleInvoices)),
    initialUrl = '/notas',
  ): Promise<void> {
    invoicesService = { getPaged: vi.fn().mockReturnValue(getPagedResult), create: vi.fn() };
    productsService = { getAll: vi.fn().mockReturnValue(of(sampleProducts)) };
    notification = { success: vi.fn(), error: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'notas', component: InvoicesListPage },
          { path: 'notas/:id', component: FakeInvoiceDetailPage },
        ]),
        provideNoopAnimations(),
        { provide: InvoicesService, useValue: invoicesService },
        { provide: ProductsService, useValue: productsService },
        { provide: NotificationService, useValue: notification },
      ],
    });

    location = TestBed.inject(Location);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create(initialUrl);
    harness.detectChanges();
  }

  function el(): HTMLElement {
    return harness.routeNativeElement!;
  }

  function newInvoiceButton(): HTMLButtonElement {
    return el().querySelector('.invoices-list-page__header button')!;
  }

  function sortButton(
    field: 'number' | 'createdAtUtc' | 'itemsCount' | 'status',
  ): HTMLButtonElement {
    const labels: Record<typeof field, string> = {
      number: 'Número',
      createdAtUtc: 'Emissão',
      itemsCount: 'Itens',
      status: 'Status',
    };
    return Array.from(el().querySelectorAll<HTMLButtonElement>('.data-table__sort-button')).find(
      (button) => button.textContent?.trim().startsWith(labels[field]),
    )!;
  }

  function columnHeader(field: 'number' | 'createdAtUtc' | 'itemsCount' | 'status'): HTMLElement {
    return sortButton(field).closest('th')!;
  }

  async function openDialog(): Promise<void> {
    const button = newInvoiceButton();
    button.focus();
    button.click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function dialogEl(): HTMLElement {
    return document.querySelector('.invoice-form--dialog') as HTMLElement;
  }

  it('should render the page title and subtitle', async () => {
    await setup(of(pageOf([])));

    const title = el().querySelector('#invoices-title');
    expect(title?.textContent?.trim()).toBe('Notas fiscais');
    expect(el().querySelector('.invoices-list-page__subtitle')?.textContent).toContain(
      'Emissão e impressão de notas',
    );
  });

  it('should render the "Nova nota fiscal" call to action as a button that does not navigate', async () => {
    await setup(of(pageOf([])));

    const button = newInvoiceButton();
    expect(button.textContent).toContain('Nova nota fiscal');
    expect(button.getAttribute('type')).toBe('button');
    expect(el().querySelector('a[routerLink="/notas/nova"]')).toBeFalsy();
  });

  it('should show a loading indicator while fetching the invoice list', async () => {
    const pending = new Subject<PagedResponse<InvoiceSummary>>();
    await setup(pending.asObservable());

    expect(el().querySelector('[role="status"]')).toBeTruthy();
  });

  it('should render the empty state when there are no invoices', async () => {
    await setup(of(pageOf([])));

    const empty = el().querySelector('.invoices-list-page__empty');
    expect(empty?.textContent).toContain('Nenhuma nota cadastrada');
    expect(el().querySelector('table')).toBeFalsy();
  });

  it('should render a table row for each invoice with item count and status badges, defaulting to page 1/size 5/sortBy=number desc', async () => {
    await setup(of(pageOf(sampleInvoices)));

    const rows = el().querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(el().textContent).toContain('Aberta');
    expect(el().textContent).toContain('Fechada');
    expect(invoicesService.getPaged).toHaveBeenCalledWith(1, 5, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
  });

  it('should reflect the effective pageSize (not a stray default) in the page-size select', async () => {
    await setup(
      of(pageOf(sampleInvoices)),
      '/notas?page=1&pageSize=10&sortBy=number&sortDirection=desc',
    );

    expect(invoicesService.getPaged).toHaveBeenCalledWith(1, 10, 'number', 'desc');
    const select: HTMLSelectElement = el().querySelector('select')!;
    expect(select.value).toBe('10');
  });

  it('should place the page-size select and pagination before the table, never duplicating them after it', async () => {
    await setup(of(pageOf(sampleInvoices)));

    const select = el().querySelector('select');
    const pagination = el().querySelector('.pagination');
    const table = el().querySelector('table');
    expect(select).toBeTruthy();
    expect(pagination).toBeTruthy();
    expect(table).toBeTruthy();

    expect(select!.compareDocumentPosition(table!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(
      pagination!.compareDocumentPosition(table!) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(el().querySelectorAll('select').length).toBe(1);
    expect(el().querySelectorAll('.pagination').length).toBe(1);
  });

  it('should navigate to the invoice detail route (with the current listing params as router state) when a row is clicked', async () => {
    await setup(
      of(pageOf(sampleInvoices)),
      '/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc',
    );
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const row: HTMLTableRowElement = el().querySelector('tbody tr')!;
    row.click();

    expect(router.navigate).toHaveBeenCalledWith(['/notas', 1], {
      state: {
        listQueryParams: { page: '1', pageSize: '5', sortBy: 'number', sortDirection: 'desc' },
      },
    });
  });

  it('should be reachable and activatable by keyboard (tabindex + Enter)', async () => {
    await setup(of(pageOf(sampleInvoices)));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const row: HTMLTableRowElement = el().querySelector('tbody tr')!;
    expect(row.getAttribute('tabindex')).toBe('0');
    expect(row.getAttribute('role')).toBe('link');

    row.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(router.navigate).toHaveBeenCalledWith(['/notas', 1], expect.anything());
  });

  it('should not activate row navigation when a sort header button is clicked (no event bubbling into the row handler)', async () => {
    await setup(of(pageOf(sampleInvoices)));
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    sortButton('status').click();
    harness.detectChanges();

    expect(router.navigate).not.toHaveBeenCalledWith(
      ['/notas', expect.anything()],
      expect.anything(),
    );
  });

  it('should show an inline error state (with a retry action) and a single toast on the first load failure', async () => {
    await setup(
      throwError(() => new HttpErrorResponse({ status: 503 })),
      '/notas?page=2&pageSize=25&sortBy=number&sortDirection=desc',
    );

    const alert = el().querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('indisponível');
    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(
      'O serviço de estoque está temporariamente indisponível. Tente novamente em alguns instantes.',
    );

    const retryButton: HTMLButtonElement = el().querySelector('.invoices-list-page__state button')!;

    // Retrying while it keeps failing must not stack a second toast.
    retryButton.click();
    harness.detectChanges();
    expect(notification.error).toHaveBeenCalledTimes(1);

    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 2, pageSize: 25 })),
    );
    retryButton.click();
    harness.detectChanges();

    expect(invoicesService.getPaged).toHaveBeenNthCalledWith(3, 2, 25, 'number', 'desc');
    expect(el().querySelectorAll('tbody tr').length).toBe(2);
  });

  it('should load the page indicated by valid query params directly from the URL', async () => {
    await setup(
      of(
        pageOf(sampleInvoices, {
          pageNumber: 2,
          pageSize: 5,
          totalCount: 8,
          totalPages: 2,
          hasPreviousPage: true,
          hasNextPage: false,
        }),
      ),
      '/notas?page=2&pageSize=5&sortBy=status&sortDirection=asc',
    );

    expect(invoicesService.getPaged).toHaveBeenCalledWith(2, 5, 'status', 'asc');
    expect(location.path()).toBe('/notas?page=2&pageSize=5&sortBy=status&sortDirection=asc');
  });

  it('should normalize invalid query params to the defaults without looping', async () => {
    await setup(of(pageOf(sampleInvoices)), '/notas?page=0&pageSize=7');

    expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);
    expect(invoicesService.getPaged).toHaveBeenCalledWith(1, 5, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
  });

  it('should normalize invalid sortBy/sortDirection to the defaults without looping', async () => {
    await setup(
      of(pageOf(sampleInvoices)),
      '/notas?page=2&pageSize=10&sortBy=unknown&sortDirection=sideways',
    );

    expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);
    expect(invoicesService.getPaged).toHaveBeenCalledWith(1, 5, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
  });

  it('should request the next page and update the URL when "Próxima" is clicked', async () => {
    await setup(
      of(
        pageOf(sampleInvoices, {
          pageNumber: 1,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: false,
          hasNextPage: true,
        }),
      ),
      '/notas?page=1&pageSize=10&sortBy=number&sortDirection=desc',
    );

    invoicesService.getPaged.mockReturnValue(
      of(
        pageOf(sampleInvoices, {
          pageNumber: 2,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: true,
          hasNextPage: true,
        }),
      ),
    );

    const buttons = el().querySelectorAll('.pagination__button');
    const next = buttons[1] as HTMLButtonElement;
    next.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(invoicesService.getPaged).toHaveBeenCalledWith(2, 10, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=2&pageSize=10&sortBy=number&sortDirection=desc');
  });

  it('should change the page size and return to page 1, preserving the active sort', async () => {
    await setup(
      of(
        pageOf(sampleInvoices, {
          pageNumber: 2,
          pageSize: 10,
          totalCount: 25,
          totalPages: 3,
          hasPreviousPage: true,
          hasNextPage: true,
        }),
      ),
      '/notas?page=2&pageSize=10&sortBy=createdAtUtc&sortDirection=asc',
    );

    invoicesService.getPaged.mockReturnValue(
      of(
        pageOf(sampleInvoices, {
          pageNumber: 1,
          pageSize: 50,
          totalCount: 25,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        }),
      ),
    );

    const select: HTMLSelectElement = el().querySelector('select')!;
    select.value = '50';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(invoicesService.getPaged).toHaveBeenCalledWith(1, 50, 'createdAtUtc', 'asc');
    expect(location.path()).toBe('/notas?page=1&pageSize=50&sortBy=createdAtUtc&sortDirection=asc');
  });

  it('should toggle page size 5 -> 10 -> 5, each change resolving in a single request/URL update', async () => {
    await setup(
      of(pageOf(sampleInvoices)),
      '/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc',
    );

    expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);
    expect(invoicesService.getPaged).toHaveBeenNthCalledWith(1, 1, 5, 'number', 'desc');

    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 10 })),
    );
    let select: HTMLSelectElement = el().querySelector('select')!;
    select.value = '10';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(invoicesService.getPaged).toHaveBeenCalledTimes(2);
    expect(invoicesService.getPaged).toHaveBeenNthCalledWith(2, 1, 10, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=1&pageSize=10&sortBy=number&sortDirection=desc');
    select = el().querySelector('select')!;
    expect(select.value).toBe('10');

    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 5 })),
    );
    select.value = '5';
    select.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(invoicesService.getPaged).toHaveBeenCalledTimes(3);
    expect(invoicesService.getPaged).toHaveBeenNthCalledWith(3, 1, 5, 'number', 'desc');
    expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
    select = el().querySelector('select')!;
    expect(select.value).toBe('5');
  });

  it('should restore the requested page/size on refresh (fresh navigation to the same URL)', async () => {
    await setup(
      of(pageOf(sampleInvoices, { pageNumber: 2, pageSize: 25, totalCount: 30, totalPages: 2 })),
      '/notas?page=2&pageSize=25&sortBy=number&sortDirection=desc',
    );

    expect(invoicesService.getPaged).toHaveBeenCalledWith(2, 25, 'number', 'desc');
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
      of(pageOf(sampleInvoices)),
      '/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc',
    );

    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 25 })),
    );
    await harness.navigateByUrl('/notas?page=1&pageSize=25&sortBy=number&sortDirection=desc');
    harness.detectChanges();
    await harness.fixture.whenStable();
    expect(location.path()).toBe('/notas?page=1&pageSize=25&sortBy=number&sortDirection=desc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('25');

    // "Back": returning to the previously visited URL.
    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 5 })),
    );
    await harness.navigateByUrl('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('5');

    // "Forward": returning to the later-visited URL.
    invoicesService.getPaged.mockReturnValue(
      of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 25 })),
    );
    await harness.navigateByUrl('/notas?page=1&pageSize=25&sortBy=number&sortDirection=desc');
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(location.path()).toBe('/notas?page=1&pageSize=25&sortBy=number&sortDirection=desc');
    expect((el().querySelector('select') as HTMLSelectElement).value).toBe('25');
  });

  describe('sortable column headers', () => {
    it('should mark the default sort column (Número, descending) with aria-sort and a ▼ indicator', async () => {
      await setup(of(pageOf(sampleInvoices)));

      expect(columnHeader('number').getAttribute('aria-sort')).toBe('descending');
      expect(sortButton('number').textContent).toContain('▼');
      expect(columnHeader('createdAtUtc').hasAttribute('aria-sort')).toBe(false);
      expect(columnHeader('itemsCount').hasAttribute('aria-sort')).toBe(false);
      expect(columnHeader('status').hasAttribute('aria-sort')).toBe(false);
    });

    it('should select a new column ascending and reset to page 1, preserving pageSize', async () => {
      await setup(
        of(pageOf(sampleInvoices, { pageNumber: 2, pageSize: 10, totalCount: 25, totalPages: 3 })),
        '/notas?page=2&pageSize=10&sortBy=number&sortDirection=desc',
      );

      invoicesService.getPaged.mockReturnValue(
        of(pageOf(sampleInvoices, { pageNumber: 1, pageSize: 10 })),
      );
      sortButton('createdAtUtc').click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(invoicesService.getPaged).toHaveBeenLastCalledWith(1, 10, 'createdAtUtc', 'asc');
      expect(location.path()).toBe(
        '/notas?page=1&pageSize=10&sortBy=createdAtUtc&sortDirection=asc',
      );
      expect(columnHeader('createdAtUtc').getAttribute('aria-sort')).toBe('ascending');
    });

    it('should flip desc -> asc on a second click of the same (default) column', async () => {
      await setup(
        of(pageOf(sampleInvoices)),
        '/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc',
      );

      invoicesService.getPaged.mockReturnValue(of(pageOf(sampleInvoices)));
      sortButton('number').click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(invoicesService.getPaged).toHaveBeenLastCalledWith(1, 5, 'number', 'asc');
      expect(location.path()).toBe('/notas?page=1&pageSize=5&sortBy=number&sortDirection=asc');
      expect(columnHeader('number').getAttribute('aria-sort')).toBe('ascending');
      expect(sortButton('number').textContent).toContain('▲');
    });
  });

  describe('column alignment', () => {
    it('should mark the Itens header and every Itens value cell with the same numeric column class', async () => {
      await setup(of(pageOf(sampleInvoices)));

      expect(columnHeader('itemsCount').classList).toContain('data-table__col-numeric');

      const itemsCells: HTMLTableCellElement[] = Array.from(
        el().querySelectorAll('tbody td.data-table__col-numeric'),
      );
      expect(itemsCells.length).toBe(sampleInvoices.length);
      itemsCells.forEach((cell, index) =>
        expect(cell.textContent?.trim()).toBe(String(sampleInvoices[index].itemsCount)),
      );
    });

    it('should mark the Status header and its badge cell with the centered status column class', async () => {
      await setup(of(pageOf(sampleInvoices)));

      expect(columnHeader('status').classList).toContain('data-table__col-status');

      const statusCells: HTMLTableCellElement[] = Array.from(
        el().querySelectorAll('tbody td.data-table__col-status'),
      );
      expect(statusCells.length).toBe(sampleInvoices.length);
      statusCells.forEach((cell) =>
        expect(cell.querySelector('app-invoice-status-badge')).toBeTruthy(),
      );
    });

    it('should mark the Número and Emissão headers with the text column class', async () => {
      await setup(of(pageOf(sampleInvoices)));

      expect(columnHeader('number').classList).toContain('data-table__col-text');
      expect(columnHeader('createdAtUtc').classList).toContain('data-table__col-text');
    });
  });

  describe('registration dialog', () => {
    it('should open the dialog with the invoice form when "+ Nova nota fiscal" is clicked', async () => {
      await setup(of(pageOf([])));

      await openDialog();

      expect(dialogEl()).toBeTruthy();
      expect(dialogEl().querySelector('#invoice-form-dialog-title')?.textContent?.trim()).toBe(
        'Nova nota fiscal',
      );
      expect(invoicesService.create).not.toHaveBeenCalled();
    });

    it('should not create an invoice and leave the listing/pagination unchanged when cancelled', async () => {
      await setup(of(pageOf([])), '/notas?page=1&pageSize=10&sortBy=number&sortDirection=desc');
      expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);

      await openDialog();

      const cancelButton = Array.from(dialogEl().querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cancelar',
      ) as HTMLButtonElement;
      cancelButton.click();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);
      expect(location.path()).toBe('/notas?page=1&pageSize=10&sortBy=number&sortDirection=desc');
      expect(document.querySelector('.invoice-form--dialog')).toBeFalsy();
    });

    it('should return focus to the "+ Nova nota fiscal" button after cancelling', async () => {
      await setup(of(pageOf([])));

      await openDialog();

      const cancelButton = Array.from(dialogEl().querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cancelar',
      ) as HTMLButtonElement;
      cancelButton.click();
      harness.detectChanges();
      await harness.fixture.whenStable();
      harness.detectChanges();

      expect(document.activeElement).toBe(newInvoiceButton());
    });

    it('should block a duplicate product inside the dialog form', async () => {
      await setup(of(pageOf([])));

      await openDialog();

      const addItemButton = Array.from(dialogEl().querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Adicionar item'),
      ) as HTMLButtonElement;
      addItemButton.click();
      addItemButton.click();
      harness.detectChanges();

      const debugNode = getDebugNode(dialogEl())!;
      const invoiceFormComponent = debugNode.componentInstance as {
        items: {
          at: (i: number) => { patchValue: (v: object) => void };
          hasError: (e: string) => boolean;
        };
      };
      invoiceFormComponent.items.at(0).patchValue({ productId: 1, quantity: 1 });
      invoiceFormComponent.items.at(1).patchValue({ productId: 1, quantity: 2 });
      harness.detectChanges();

      expect(invoiceFormComponent.items.hasError('duplicateProducts')).toBe(true);

      const form: HTMLFormElement = dialogEl().querySelector('form')!;
      form.dispatchEvent(new Event('submit'));
      harness.detectChanges();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(dialogEl().textContent).toContain('produtos duplicados');
    });

    it('should create the invoice, close the dialog and reload page 1 preserving pageSize and the active sort on success, remaining on /notas', async () => {
      await setup(
        of(pageOf(sampleInvoices, { pageNumber: 2, pageSize: 10, totalCount: 15, totalPages: 2 })),
        '/notas?page=2&pageSize=10&sortBy=status&sortDirection=asc',
      );

      const created: Invoice = {
        id: 10,
        number: 5,
        status: 'Open',
        createdAtUtc: '2026-08-19T10:00:00Z',
        closedAtUtc: null,
        items: [
          { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 2 },
        ],
      };
      invoicesService.create.mockReturnValue(of(created));
      invoicesService.getPaged.mockReturnValue(of(pageOf([created as unknown as InvoiceSummary])));

      await openDialog();

      const addItemButton = Array.from(dialogEl().querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Adicionar item'),
      ) as HTMLButtonElement;
      addItemButton.click();
      harness.detectChanges();

      // The dialog content is a separate root Angular view (attached via the
      // CDK overlay, not a logical descendant of `InvoicesListPage`), so
      // `getDebugNode` is used to reach the `InvoiceFormComponent` instance
      // directly and drive its `FormArray`, same as the mat-select-avoiding
      // approach used in `invoice-form.spec.ts`.
      const invoiceFormNativeEl = dialogEl();
      const debugNode = getDebugNode(invoiceFormNativeEl)!;
      const invoiceFormComponent = debugNode.componentInstance as {
        items: { at: (i: number) => { patchValue: (v: object) => void } };
      };
      invoiceFormComponent.items.at(0).patchValue({ productId: 1, quantity: 2 });
      harness.detectChanges();

      const form: HTMLFormElement = dialogEl().querySelector('form')!;
      form.dispatchEvent(new Event('submit'));
      harness.detectChanges();
      await harness.fixture.whenStable();
      harness.detectChanges();
      await harness.fixture.whenStable();

      expect(invoicesService.create).toHaveBeenCalledWith({
        items: [{ productId: 1, quantity: 2 }],
      });
      expect(document.querySelector('.invoice-form--dialog')).toBeFalsy();
      expect(location.path()).toBe('/notas?page=1&pageSize=10&sortBy=status&sortDirection=asc');
      expect(invoicesService.getPaged).toHaveBeenLastCalledWith(1, 10, 'status', 'asc');
    });

    it('should keep the dialog open on a 409 conflict', async () => {
      await setup(of(pageOf([])));

      const error = new HttpErrorResponse({ status: 409, error: { detail: 'Conflict.' } });
      invoicesService.create.mockReturnValue(throwError(() => error));

      await openDialog();

      expect(dialogEl()).toBeTruthy();
      expect(invoicesService.getPaged).toHaveBeenCalledTimes(1);
    });
  });
});
