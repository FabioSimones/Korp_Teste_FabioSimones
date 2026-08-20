import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../../products/models/product';
import { ProductsService } from '../../products/products.service';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoiceFormComponent } from './invoice-form';
import { InvoiceFormPage } from './invoice-form-page';

interface InvoicesServiceStub {
  create: ReturnType<typeof vi.fn>;
}

interface ProductsServiceStub {
  getAll: ReturnType<typeof vi.fn>;
}

/**
 * `InvoiceFormPage` is now a thin route wrapper around the shared
 * `InvoiceFormComponent` (see `invoice-form.spec.ts` for the exhaustive
 * validation/submit/error coverage, run both in "page" and "dialog" modes).
 * This spec only asserts the page-specific chrome (title, back link) and
 * that the route keeps working end-to-end through the shared component,
 * without a `MatDialogRef` in the injector — confirming `/notas/nova`
 * remains functional as a direct route after the extraction.
 */
describe('InvoiceFormPage', () => {
  let fixture: ComponentFixture<InvoiceFormPage>;
  let invoicesService: InvoicesServiceStub;
  let productsService: ProductsServiceStub;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let router: Router;

  const sampleProducts: Product[] = [{ id: 1, code: 'A1', description: 'Produto A', balance: 10 }];

  function setup(): void {
    invoicesService = { create: vi.fn() };
    productsService = { getAll: vi.fn().mockReturnValue(of(sampleProducts)) };
    notification = { success: vi.fn(), error: vi.fn() };

    TestBed.configureTestingModule({
      imports: [InvoiceFormPage],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: InvoicesService, useValue: invoicesService },
        { provide: ProductsService, useValue: productsService },
        { provide: NotificationService, useValue: notification },
      ],
    });

    fixture = TestBed.createComponent(InvoiceFormPage);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
  }

  it('should render the page title and a link back to the listing', () => {
    setup();

    const title = fixture.nativeElement.querySelector('#invoice-form-title');
    expect(title?.textContent?.trim()).toBe('Nova nota fiscal');

    const backLink: HTMLAnchorElement =
      fixture.nativeElement.querySelector('a[routerLink="/notas"]');
    expect(backLink?.textContent).toContain('Voltar para a listagem');
  });

  it('should render the shared invoice form and load available products', () => {
    setup();

    expect(fixture.nativeElement.querySelector('app-invoice-form')).toBeTruthy();
    expect(productsService.getAll).toHaveBeenCalled();
  });

  it('should not render dialog-only chrome (title/close button) when used as a route', () => {
    setup();

    expect(fixture.nativeElement.querySelector('#invoice-form-dialog-title')).toBeFalsy();
    expect(fixture.nativeElement.querySelector('.invoice-form__dialog-close')).toBeFalsy();
  });

  it('should submit an invoice end-to-end and navigate to the list on success', () => {
    setup();

    const created: Invoice = {
      id: 1,
      number: 100,
      status: 'Open',
      createdAtUtc: '2026-08-17T10:00:00Z',
      closedAtUtc: null,
      items: [
        { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 2 },
      ],
    };
    invoicesService.create.mockReturnValue(of(created));

    const buttons: HTMLButtonElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    );
    const addItemButton = buttons.find((b) => b.textContent?.includes('Adicionar item'))!;
    addItemButton.click();
    fixture.detectChanges();

    // `mat-select` isn't a native <select>; drive the underlying FormArray
    // directly instead of simulating a full Material overlay interaction.
    const invoiceForm = fixture.debugElement.query(By.directive(InvoiceFormComponent))
      .componentInstance as InvoiceFormComponent;
    (
      invoiceForm as unknown as {
        items: { at: (i: number) => { patchValue: (v: object) => void } };
      }
    ).items
      .at(0)
      .patchValue({ productId: 1, quantity: 2 });
    fixture.detectChanges();

    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(invoicesService.create).toHaveBeenCalledWith({ items: [{ productId: 1, quantity: 2 }] });
    expect(notification.success).toHaveBeenCalledWith('Nota Nº 100 criada com sucesso.');
    expect(router.navigate).toHaveBeenCalledWith(['/notas']);
  });
});
