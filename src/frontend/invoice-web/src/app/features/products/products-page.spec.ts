import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Observable, Subject, of, throwError } from 'rxjs';

import { Product } from './models/product';
import { ProductsPage } from './products-page';
import { ProductsService } from './products.service';

interface ProductsServiceStub {
  getAll: ReturnType<typeof vi.fn>;
  create: ReturnType<typeof vi.fn>;
}

describe('ProductsPage', () => {
  let fixture: ComponentFixture<ProductsPage>;
  let productsService: ProductsServiceStub;

  const sampleProducts: Product[] = [
    { id: 1, code: 'A1', description: 'Produto A', balance: 10 },
    { id: 2, code: 'B2', description: 'Produto B', balance: 5 },
  ];

  function setup(getAllResult: Observable<Product[]> = of(sampleProducts)): void {
    productsService = {
      getAll: vi.fn().mockReturnValue(getAllResult),
      create: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [ProductsPage],
      providers: [provideNoopAnimations(), { provide: ProductsService, useValue: productsService }],
    });

    fixture = TestBed.createComponent(ProductsPage);
    fixture.detectChanges();
  }

  function setInputValue(selector: string, value: string): void {
    const input: HTMLInputElement = fixture.nativeElement.querySelector(selector);
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function fillForm(code: string, description: string, balance: string): void {
    setInputValue('input[formcontrolname="code"]', code);
    setInputValue('input[formcontrolname="description"]', description);
    setInputValue('input[formcontrolname="balance"]', balance);
    fixture.detectChanges();
  }

  function submitForm(): void {
    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('should render the page title', () => {
    setup(of([]));

    const title = fixture.nativeElement.querySelector('#products-title');
    expect(title?.textContent?.trim()).toBe('Produtos');
  });

  it('should show a loading indicator while fetching the product list', () => {
    const pending = new Subject<Product[]>();
    setup(pending.asObservable());

    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeTruthy();
  });

  it('should render the empty state when there are no products', () => {
    setup(of([]));

    const empty = fixture.nativeElement.querySelector('.products-page__empty');
    expect(empty?.textContent).toContain('Nenhum produto cadastrado');
    expect(fixture.nativeElement.querySelector('table')).toBeFalsy();
  });

  it('should render a table row for each registered product', () => {
    setup(of(sampleProducts));

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('A1');
    expect(fixture.nativeElement.textContent).toContain('Produto B');
  });

  it('should show an error state with a retry action when the list request fails', () => {
    setup(throwError(() => new HttpErrorResponse({ status: 503 })));

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Não foi possível carregar a lista de produtos');

    const retryButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.products-page__state button',
    );
    productsService.getAll.mockReturnValue(of(sampleProducts));
    retryButton.click();
    fixture.detectChanges();

    expect(productsService.getAll).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
  });

  describe('form validation', () => {
    beforeEach(() => setup(of([])));

    it('should not submit and should display validation errors for empty required fields', () => {
      submitForm();

      expect(productsService.create).not.toHaveBeenCalled();
      const errors = fixture.nativeElement.querySelectorAll('mat-error');
      expect(errors.length).toBeGreaterThan(0);
      expect(fixture.nativeElement.textContent).toContain('Informe o código do produto.');
      expect(fixture.nativeElement.textContent).toContain('Informe a descrição do produto.');
    });

    it('should reject a negative balance', () => {
      fillForm('A1', 'Produto A', '-5');
      submitForm();

      expect(productsService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('O saldo não pode ser negativo.');
    });

    it('should reject a non-integer balance', () => {
      fillForm('A1', 'Produto A', '2.5');
      submitForm();

      expect(productsService.create).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('O saldo deve ser um número inteiro.');
    });
  });

  describe('product creation', () => {
    beforeEach(() => setup(of([])));

    it('should add the created product to the list and reset the form on success', () => {
      const created: Product = { id: 10, code: 'C3', description: 'Produto C', balance: 7 };
      productsService.create.mockReturnValue(of(created));

      fillForm('C3', 'Produto C', '7');
      submitForm();

      expect(productsService.create).toHaveBeenCalledWith({
        code: 'C3',
        description: 'Produto C',
        balance: 7,
      });

      const rows = fixture.nativeElement.querySelectorAll('tbody tr');
      expect(rows.length).toBe(1);
      const codeInput: HTMLInputElement = fixture.nativeElement.querySelector(
        'input[formcontrolname="code"]',
      );
      expect(codeInput.value).toBe('');
    });

    it('should flag the code field as duplicate and show the API message on 409', () => {
      const error = new HttpErrorResponse({
        status: 409,
        error: { detail: "Product code 'C3' is already registered." },
      });
      productsService.create.mockReturnValue(throwError(() => error));

      fillForm('C3', 'Produto C', '7');
      submitForm();

      const message = fixture.nativeElement.querySelector('.products-page__form-error');
      expect(message?.textContent).toContain('already registered');
      expect(fixture.nativeElement.textContent).toContain('Este código já está cadastrado.');
    });

    it('should show an unavailable-service message when the API cannot be reached', () => {
      const error = new HttpErrorResponse({ status: 0 });
      productsService.create.mockReturnValue(throwError(() => error));

      fillForm('C3', 'Produto C', '7');
      submitForm();

      const message = fixture.nativeElement.querySelector('.products-page__form-error');
      expect(message?.textContent).toContain('indisponível');
    });
  });
});
