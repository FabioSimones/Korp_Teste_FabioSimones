import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../models/product';
import { ProductsService } from '../products.service';
import { ProductFormDialog } from './product-form-dialog';

interface ProductsServiceStub {
  create: ReturnType<typeof vi.fn>;
}

describe('ProductFormDialog', () => {
  let fixture: ComponentFixture<ProductFormDialog>;
  let productsService: ProductsServiceStub;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let dialogRef: { close: ReturnType<typeof vi.fn>; disableClose: boolean };

  function setup(): void {
    productsService = { create: vi.fn() };
    notification = { success: vi.fn(), error: vi.fn() };
    dialogRef = { close: vi.fn(), disableClose: false };

    TestBed.configureTestingModule({
      imports: [ProductFormDialog],
      providers: [
        provideNoopAnimations(),
        { provide: ProductsService, useValue: productsService },
        { provide: NotificationService, useValue: notification },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });

    fixture = TestBed.createComponent(ProductFormDialog);
    fixture.detectChanges();
  }

  function el(): HTMLElement {
    return fixture.nativeElement;
  }

  function setInputValue(selector: string, value: string): void {
    const input: HTMLInputElement = el().querySelector(selector)!;
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
    const form: HTMLFormElement = el().querySelector('form')!;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('should render the dialog title and the three fields', () => {
    setup();

    expect(el().querySelector('#product-form-dialog-title')?.textContent?.trim()).toBe(
      'Cadastrar produto',
    );
    expect(el().querySelector('input[formcontrolname="code"]')).toBeTruthy();
    expect(el().querySelector('input[formcontrolname="description"]')).toBeTruthy();
    expect(el().querySelector('input[formcontrolname="balance"]')).toBeTruthy();
  });

  it('should close without submitting when Cancelar is clicked', () => {
    setup();

    const buttons = Array.from(el().querySelectorAll('button'));
    const cancelButton = buttons.find((b) => b.textContent?.trim() === 'Cancelar')!;
    cancelButton.click();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledWith();
  });

  it('should close without submitting when the header close button is clicked', () => {
    setup();

    const closeButton: HTMLButtonElement = el().querySelector('.product-form-dialog__close')!;
    closeButton.click();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(dialogRef.close).toHaveBeenCalledWith();
  });

  it('should not submit and should display validation errors for empty required fields', () => {
    setup();

    submitForm();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(el().textContent).toContain('Informe o código do produto.');
    expect(el().textContent).toContain('Informe a descrição do produto.');
  });

  it('should start the balance field empty rather than pre-filled with 0', () => {
    setup();

    const balanceInput: HTMLInputElement = el().querySelector('input[formcontrolname="balance"]')!;
    expect(balanceInput.value).toBe('');
  });

  it('should reject a zero balance and keep the submit button disabled', () => {
    setup();

    fillForm('A1', 'Produto A', '0');
    submitForm();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(el().textContent).toContain('Informe um saldo inicial inteiro maior que zero.');
    const submitButton: HTMLButtonElement = el().querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);
  });

  it('should reject a negative balance and keep the submit button disabled', () => {
    setup();

    fillForm('A1', 'Produto A', '-5');
    submitForm();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(el().textContent).toContain('Informe um saldo inicial inteiro maior que zero.');
    const submitButton: HTMLButtonElement = el().querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);
  });

  it('should reject a non-integer balance and keep the submit button disabled', () => {
    setup();

    fillForm('A1', 'Produto A', '2.5');
    submitForm();

    expect(productsService.create).not.toHaveBeenCalled();
    expect(el().textContent).toContain('Informe um saldo inicial inteiro maior que zero.');
    const submitButton: HTMLButtonElement = el().querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);
  });

  it('should enable the submit button once balance 1 is entered alongside valid code/description', () => {
    setup();

    const submitButton: HTMLButtonElement = el().querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);

    fillForm('A1', 'Produto A', '1');

    expect(submitButton.disabled).toBe(false);
  });

  it('should submit, notify and close the dialog with the created product on success', () => {
    setup();

    const created: Product = { id: 10, code: 'C3', description: 'Produto C', balance: 7 };
    productsService.create.mockReturnValue(of(created));

    fillForm('C3', 'Produto C', '7');
    submitForm();

    expect(productsService.create).toHaveBeenCalledWith({
      code: 'C3',
      description: 'Produto C',
      balance: 7,
    });
    expect(notification.success).toHaveBeenCalledWith('Produto "C3" cadastrado com sucesso.');
    expect(dialogRef.close).toHaveBeenCalledWith(created);
  });

  it('should flag the code field as duplicate, show one error toast and keep the dialog open on 409 (no errorCode)', () => {
    setup();

    const error = new HttpErrorResponse({
      status: 409,
      error: { detail: "Product code 'C3' is already registered." },
    });
    productsService.create.mockReturnValue(throwError(() => error));

    fillForm('C3', 'Produto C', '7');
    submitForm();

    const message = el().querySelector('.product-form-dialog__form-error');
    // Raw backend text (English/untranslated) must never reach the UI.
    expect(message?.textContent).not.toContain('already registered');
    expect(message?.textContent).toContain('Este código de produto já está cadastrado.');
    expect(el().textContent).toContain('Este código já está cadastrado.');
    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith('Este código de produto já está cadastrado.');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('should resolve DUPLICATE_PRODUCT_CODE errorCode into the same duplicate message', () => {
    setup();

    const error = new HttpErrorResponse({
      status: 409,
      error: { errorCode: 'DUPLICATE_PRODUCT_CODE', detail: 'irrelevant raw detail' },
    });
    productsService.create.mockReturnValue(throwError(() => error));

    fillForm('C3', 'Produto C', '7');
    submitForm();

    expect(el().textContent).toContain('Este código de produto já está cadastrado.');
    expect(form_error_text()).not.toContain('irrelevant raw detail');

    function form_error_text(): string {
      return el().querySelector('.product-form-dialog__form-error')?.textContent ?? '';
    }
  });

  it('should show an unavailable-service message when the API cannot be reached, keeping the dialog open', () => {
    setup();

    const error = new HttpErrorResponse({ status: 0 });
    productsService.create.mockReturnValue(throwError(() => error));

    fillForm('C3', 'Produto C', '7');
    submitForm();

    const message = el().querySelector('.product-form-dialog__form-error');
    expect(message?.textContent).toContain('indisponível');
    expect(notification.error).toHaveBeenCalledWith(message?.textContent?.trim());
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('should disable Cancelar/close while submitting and block a second submit', () => {
    setup();

    const pending = new Subject<Product>();
    productsService.create.mockReturnValue(pending.asObservable());

    fillForm('C3', 'Produto C', '7');
    submitForm();

    expect(dialogRef.disableClose).toBe(true);
    const submitButton: HTMLButtonElement = el().querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);

    const cancelButton = Array.from(el().querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Cancelar',
    ) as HTMLButtonElement;
    expect(cancelButton.disabled).toBe(true);

    pending.next({ id: 10, code: 'C3', description: 'Produto C', balance: 7 });
    pending.complete();
  });
});
