import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../../products/models/product';
import { ProductsService } from '../../products/products.service';
import { Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';
import { InvoiceFormPage } from './invoice-form-page';

interface InvoicesServiceStub {
  create: ReturnType<typeof vi.fn>;
}

interface ProductsServiceStub {
  getAll: ReturnType<typeof vi.fn>;
}

describe('InvoiceFormPage', () => {
  let fixture: ComponentFixture<InvoiceFormPage>;
  let component: InvoiceFormPage;
  let invoicesService: InvoicesServiceStub;
  let productsService: ProductsServiceStub;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let router: Router;

  const sampleProducts: Product[] = [
    { id: 1, code: 'A1', description: 'Produto A', balance: 10 },
    { id: 2, code: 'B2', description: 'Produto B', balance: 5 },
  ];

  function setup(products = sampleProducts): void {
    invoicesService = { create: vi.fn() };
    productsService = { getAll: vi.fn().mockReturnValue(of(products)) };
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

  it('should render the page title and load available products', () => {
    setup();

    const title = fixture.nativeElement.querySelector('#invoice-form-title');
    expect(title?.textContent?.trim()).toBe('Nova nota');
    expect(productsService.getAll).toHaveBeenCalled();
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

    expect(fixture.nativeElement.textContent).toContain('A quantidade deve ser um número inteiro.');
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

  it('should show an unavailable-service message when the API cannot be reached', () => {
    setup();
    invoicesService.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));

    component['addItem']();
    component['items'].at(0).patchValue({ productId: 1, quantity: 1 });
    fixture.detectChanges();

    submitForm();

    expect(fixture.nativeElement.textContent).toContain('indisponível');
  });

  it('should surface a 404 message when a product no longer exists in inventory', () => {
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

    expect(fixture.nativeElement.textContent).toContain('Product not found.');
  });

  it('should show a products list error state with a retry action', () => {
    setup([]);
    productsService.getAll.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 503 })),
    );

    component['reloadProducts']();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Não foi possível carregar os produtos');
  });
});
