import { HttpErrorResponse } from '@angular/common/http';
import { EnvironmentProviders, Provider } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../../products/models/product';
import { ProductsService } from '../../products/products.service';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoiceFormComponent } from './invoice-form';

interface InvoicesServiceStub {
  create: ReturnType<typeof vi.fn>;
}

interface ProductsServiceStub {
  getAll: ReturnType<typeof vi.fn>;
}

describe('InvoiceFormComponent', () => {
  let fixture: ComponentFixture<InvoiceFormComponent>;
  let component: InvoiceFormComponent;
  let invoicesService: InvoicesServiceStub;
  let productsService: ProductsServiceStub;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let router: Router;
  let dialogRef: { close: ReturnType<typeof vi.fn>; disableClose: boolean } | null;

  const sampleProducts: Product[] = [
    { id: 1, code: 'A1', description: 'Produto A', balance: 10 },
    { id: 2, code: 'B2', description: 'Produto B', balance: 5 },
  ];

  function setup(options: { asDialog?: boolean; products?: Product[] } = {}): void {
    const { asDialog = false, products = sampleProducts } = options;

    invoicesService = { create: vi.fn() };
    productsService = { getAll: vi.fn().mockReturnValue(of(products)) };
    notification = { success: vi.fn(), error: vi.fn() };
    dialogRef = asDialog ? { close: vi.fn(), disableClose: false } : null;

    const providers: (Provider | EnvironmentProviders)[] = [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: InvoicesService, useValue: invoicesService },
      { provide: ProductsService, useValue: productsService },
      { provide: NotificationService, useValue: notification },
    ];
    if (asDialog) {
      providers.push({ provide: MatDialogRef, useValue: dialogRef });
    }

    TestBed.configureTestingModule({
      imports: [InvoiceFormComponent],
      providers,
    });

    fixture = TestBed.createComponent(InvoiceFormComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
  }

  function submitForm(): void {
    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  describe('as a page (no MatDialogRef, e.g. /notas/nova)', () => {
    it('should load available products and not render dialog chrome', () => {
      setup();

      expect(productsService.getAll).toHaveBeenCalled();
      expect(fixture.nativeElement.querySelector('#invoice-form-dialog-title')).toBeFalsy();
      expect(fixture.nativeElement.querySelector('.invoice-form__dialog-close')).toBeFalsy();
    });

    it('should start with no items and block submit until one is added', () => {
      setup();

      expect(component['items'].length).toBe(0);

      submitForm();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Adicione ao menos um item');
    });

    it('should add and remove items from the FormArray', () => {
      setup();

      component['addItem']();
      component['addItem']();
      fixture.detectChanges();
      expect(component['items'].length).toBe(2);

      component['removeItem'](0);
      fixture.detectChanges();
      expect(component['items'].length).toBe(1);
    });

    it('should reject a non-positive or non-integer quantity', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 0 });
      fixture.detectChanges();

      submitForm();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('A quantidade deve ser maior que zero.');

      component['items'].at(0).patchValue({ quantity: 2.5 });
      fixture.detectChanges();
      submitForm();

      expect(fixture.nativeElement.textContent).toContain(
        'A quantidade deve ser um número inteiro.',
      );
    });

    it('should block duplicate products selected in the same form', () => {
      setup();

      component['addItem']();
      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      component['items'].at(1).patchValue({ productId: 1, quantity: 2 });
      fixture.detectChanges();

      expect(component['items'].hasError('duplicateProducts')).toBe(true);

      submitForm();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('produtos duplicados');
    });

    it('should submit the mapped payload and navigate to the list on success', () => {
      setup();

      const created: Invoice = {
        id: 1,
        number: 100,
        status: 'Open',
        createdAtUtc: '2026-08-17T10:00:00Z',
        closedAtUtc: null,
        items: [
          { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 2 },
          { id: 2, productId: 2, productCode: 'B2', productDescription: 'Produto B', quantity: 1 },
        ],
      };
      invoicesService.create.mockReturnValue(of(created));

      component['addItem']();
      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 2 });
      component['items'].at(1).patchValue({ productId: 2, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(invoicesService.create).toHaveBeenCalledWith({
        items: [
          { productId: 1, quantity: 2 },
          { productId: 2, quantity: 1 },
        ],
      });
      expect(notification.success).toHaveBeenCalledWith('Nota Nº 100 criada com sucesso.');
      expect(router.navigate).toHaveBeenCalledWith(['/notas']);
    });

    it('should show an unavailable-service message and a matching error toast when the API cannot be reached', () => {
      setup();
      invoicesService.create.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 0 })),
      );

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(fixture.nativeElement.textContent).toContain('indisponível');
      expect(notification.error).toHaveBeenCalledTimes(1);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('should surface a 404 message (no raw backend text) when a product no longer exists in inventory', () => {
      setup();
      invoicesService.create.mockReturnValue(
        throwError(
          () => new HttpErrorResponse({ status: 404, error: { detail: 'Product not found.' } }),
        ),
      );

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(fixture.nativeElement.textContent).not.toContain('Product not found.');
      expect(fixture.nativeElement.textContent).toContain('não foi encontrado');
    });

    it('should resolve PRODUCT_NOT_FOUND errorCode into a Portuguese message', () => {
      setup();
      invoicesService.create.mockReturnValue(
        throwError(
          () => new HttpErrorResponse({ status: 404, error: { errorCode: 'PRODUCT_NOT_FOUND' } }),
        ),
      );

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(fixture.nativeElement.textContent).toContain('Produto não encontrado no estoque.');
    });

    it('should show a products list error state with a retry action', () => {
      setup({ products: [] });
      productsService.getAll.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 503 })),
      );

      component['reloadProducts']();
      fixture.detectChanges();

      const alert = fixture.nativeElement.querySelector('[role="alert"]');
      expect(alert?.textContent).toContain('Não foi possível carregar os produtos');
    });
  });

  describe('as a dialog (MatDialogRef present, e.g. opened from InvoicesListPage)', () => {
    it('should render a title, close button and a Cancelar action', () => {
      setup({ asDialog: true });

      component['addItem']();
      fixture.detectChanges();

      expect(
        fixture.nativeElement.querySelector('#invoice-form-dialog-title')?.textContent?.trim(),
      ).toBe('Nova nota fiscal');
      expect(fixture.nativeElement.querySelector('.invoice-form__dialog-close')).toBeTruthy();

      const buttons: HTMLButtonElement[] = Array.from(
        fixture.nativeElement.querySelectorAll('button'),
      );
      expect(buttons.some((b) => b.textContent?.trim() === 'Cancelar')).toBe(true);
      expect(buttons.some((b) => b.textContent?.trim() === 'Criar nota')).toBe(true);
    });

    it('should close the dialog without creating an invoice when Cancelar is clicked', () => {
      setup({ asDialog: true });

      const buttons: HTMLButtonElement[] = Array.from(
        fixture.nativeElement.querySelectorAll('button'),
      );
      const cancelButton = buttons.find((b) => b.textContent?.trim() === 'Cancelar')!;
      cancelButton.click();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(dialogRef!.close).toHaveBeenCalledWith();
    });

    it('should close the dialog without creating an invoice when the header close button is clicked', () => {
      setup({ asDialog: true });

      const closeButton: HTMLButtonElement = fixture.nativeElement.querySelector(
        '.invoice-form__dialog-close',
      );
      closeButton.click();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(dialogRef!.close).toHaveBeenCalledWith();
    });

    it('should still block duplicate products and an empty item list', () => {
      setup({ asDialog: true });

      submitForm();
      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Adicione ao menos um item');

      component['addItem']();
      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      component['items'].at(1).patchValue({ productId: 1, quantity: 2 });
      fixture.detectChanges();
      submitForm();

      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('produtos duplicados');
    });

    it('should submit, notify and close the dialog with the created invoice on success, without navigating', () => {
      setup({ asDialog: true });

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

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 2 });
      fixture.detectChanges();

      submitForm();

      expect(invoicesService.create).toHaveBeenCalledWith({
        items: [{ productId: 1, quantity: 2 }],
      });
      expect(notification.success).toHaveBeenCalledWith('Nota Nº 100 criada com sucesso.');
      expect(dialogRef!.close).toHaveBeenCalledWith(created);
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('should keep the dialog open, show a Portuguese conflict message plus one error toast, and not call close on a 409 without errorCode', () => {
      setup({ asDialog: true });

      invoicesService.create.mockReturnValue(
        throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Conflict.' } })),
      );

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(fixture.nativeElement.textContent).not.toContain('Conflict.');
      expect(fixture.nativeElement.textContent).toContain('Não foi possível registrar a nota');
      expect(notification.error).toHaveBeenCalledTimes(1);
      expect(dialogRef!.close).not.toHaveBeenCalled();
    });

    it('should disable close (Escape/backdrop) and the Cancelar/close buttons while submitting', () => {
      setup({ asDialog: true });

      const pending = new Subject<Invoice>();
      invoicesService.create.mockReturnValue(pending.asObservable());

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
      fixture.detectChanges();

      submitForm();

      expect(dialogRef!.disableClose).toBe(true);
      const buttons: HTMLButtonElement[] = Array.from(
        fixture.nativeElement.querySelectorAll('button'),
      );
      const cancelButton = buttons.find((b) => b.textContent?.trim() === 'Cancelar')!;
      const closeButton: HTMLButtonElement = fixture.nativeElement.querySelector(
        '.invoice-form__dialog-close',
      );
      const submitButton = fixture.nativeElement.querySelector(
        'button[type="submit"]',
      ) as HTMLButtonElement;
      expect(cancelButton.disabled).toBe(true);
      expect(closeButton.disabled).toBe(true);
      expect(submitButton.disabled).toBe(true);

      pending.next({
        id: 1,
        number: 100,
        status: 'Open',
        createdAtUtc: '2026-08-17T10:00:00Z',
        closedAtUtc: null,
        items: [],
      });
      pending.complete();
      fixture.detectChanges();

      expect(dialogRef!.disableClose).toBe(false);
    });
  });

  describe('quantity-vs-balance validation (UX guard, backend remains authoritative)', () => {
    const productsWithZeroBalance: Product[] = [
      ...sampleProducts,
      { id: 3, code: 'C3', description: 'Produto C', balance: 0 },
    ];

    function markQuantityTouched(index: number): void {
      component['items'].at(index).controls.quantity.markAsTouched();
      fixture.detectChanges();
    }

    it('should accept a quantity equal to the available balance', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 2, quantity: 5 }); // B2 balance: 5
      markQuantityTouched(0);

      expect(component['items'].at(0).controls.quantity.valid).toBe(true);
      expect(fixture.nativeElement.textContent).not.toContain('saldo disponível (');
    });

    it('should accept a quantity below the available balance', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 2, quantity: 3 }); // B2 balance: 5
      markQuantityTouched(0);

      expect(component['items'].at(0).controls.quantity.valid).toBe(true);
    });

    it('should reject a quantity above the available balance with the exact message including the balance', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 2, quantity: 6 }); // B2 balance: 5
      markQuantityTouched(0);

      expect(component['items'].at(0).controls.quantity.hasError('exceedsBalance')).toBe(true);
      expect(fixture.nativeElement.textContent).toContain(
        'A quantidade não pode ser maior que o saldo disponível (5).',
      );
    });

    it('should reject any positive quantity for a product with zero balance, with the specific message', () => {
      setup({ products: productsWithZeroBalance });

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 3, quantity: 1 }); // C3 balance: 0
      markQuantityTouched(0);

      expect(component['items'].at(0).controls.quantity.hasError('noBalance')).toBe(true);
      expect(fixture.nativeElement.textContent).toContain('Produto sem saldo disponível.');
    });

    it('should revalidate the row quantity when its own product selection changes (rule 1)', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 2, quantity: 6 }); // B2 balance: 5
      markQuantityTouched(0);
      expect(component['items'].at(0).controls.quantity.hasError('exceedsBalance')).toBe(true);

      // Switching to A1 (balance 10) must clear the error for the same quantity.
      component['items'].at(0).controls.productId.setValue(1);
      fixture.detectChanges();

      expect(component['items'].at(0).controls.quantity.valid).toBe(true);
    });

    it('should validate each row against its own product balance, independently of other rows', () => {
      setup();

      component['addItem']();
      component['addItem']();
      component['items'].at(0).patchValue({ productId: 1, quantity: 8 }); // A1 balance: 10 -> valid
      component['items'].at(1).patchValue({ productId: 2, quantity: 8 }); // B2 balance: 5 -> invalid
      markQuantityTouched(0);
      markQuantityTouched(1);

      expect(component['items'].at(0).controls.quantity.valid).toBe(true);
      expect(component['items'].at(1).controls.quantity.hasError('exceedsBalance')).toBe(true);
    });

    it('should revalidate quantities once products finish loading after the form already has rows (rule 2)', () => {
      const pendingProducts = new Subject<Product[]>();
      invoicesService = { create: vi.fn() };
      productsService = { getAll: vi.fn().mockReturnValue(pendingProducts.asObservable()) };
      notification = { success: vi.fn(), error: vi.fn() };

      TestBed.configureTestingModule({
        imports: [InvoiceFormComponent],
        providers: [
          provideRouter([]),
          provideNoopAnimations(),
          { provide: InvoicesService, useValue: invoicesService },
          { provide: ProductsService, useValue: productsService },
          { provide: NotificationService, useValue: notification },
        ],
      });
      fixture = TestBed.createComponent(InvoiceFormComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      component['addItem']();
      // No products loaded yet: nothing to validate against, so the row is
      // not flagged even though 6 would exceed B2's eventual balance of 5.
      component['items'].at(0).patchValue({ productId: 2, quantity: 6 });
      markQuantityTouched(0);
      expect(component['items'].at(0).controls.quantity.hasError('exceedsBalance')).toBeFalsy();

      pendingProducts.next(sampleProducts);
      pendingProducts.complete();
      fixture.detectChanges();

      expect(component['items'].at(0).controls.quantity.hasError('exceedsBalance')).toBe(true);
    });

    it('should keep "Criar nota"/"Cadastrar nota" disabled while any item is invalid and never call create()', () => {
      setup();

      component['addItem']();
      component['items'].at(0).patchValue({ productId: 2, quantity: 6 }); // exceeds balance
      fixture.detectChanges();

      const submitButton: HTMLButtonElement =
        fixture.nativeElement.querySelector('button[type="submit"]');
      expect(submitButton.disabled).toBe(true);
      expect(component['form'].invalid).toBe(true);

      // Even a direct form submit (bypassing the disabled button) must not
      // reach the backend while the form is invalid: `submit()` itself
      // guards on `form.invalid` and never calls `create()`.
      submitForm();
      expect(invoicesService.create).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
