import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Signal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';

import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../../products/models/product';
import { ProductsService } from '../../products/products.service';
import { CreateInvoiceRequest, Invoice } from '../models/invoice';
import { InvoicesService } from '../invoices.service';

/** Reactive Forms group for a single invoice line item. */
type InvoiceItemGroup = FormGroup<{
  productId: FormControl<number | null>;
  quantity: FormControl<number>;
}>;

/**
 * Rejects non-integer numeric values. Empty values are ignored here since
 * `Validators.required` already covers that case.
 */
function integerValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === undefined || value === '') {
    return null;
  }
  return Number.isInteger(Number(value)) ? null : { integer: true };
}

/**
 * Flags the items `FormArray` as invalid when the same product is selected
 * in more than one line. This is a frontend-only UX guard: Billing.Api
 * intentionally allows repeated products in the same invoice (see task 07
 * notes), so this validator never talks to the backend and only prevents
 * accidental duplicate rows while filling the form.
 */
const duplicateProductsValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const array = control as FormArray<InvoiceItemGroup>;
  const productIds = array.controls
    .map((group) => group.controls.productId.value)
    .filter((id): id is number => id !== null && id !== undefined);

  return new Set(productIds).size !== productIds.length ? { duplicateProducts: true } : null;
};

/** Requires the items `FormArray` to contain at least one row. */
const atLeastOneItemValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const array = control as FormArray<InvoiceItemGroup>;
  return array.length === 0 ? { required: true } : null;
};

/**
 * Registers a new invoice with one or more product lines, consuming
 * Billing.Api for the write and Inventory.Api (via `ProductsService`) to
 * populate the product picker and show each product's available balance.
 */
@Component({
  selector: 'app-invoice-form-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './invoice-form-page.html',
  styleUrl: './invoice-form-page.scss',
})
export class InvoiceFormPage {
  private readonly fb = inject(FormBuilder);
  private readonly invoicesService = inject(InvoicesService);
  private readonly productsService = inject(ProductsService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly products = signal<Product[]>([]);
  protected readonly productsLoading = signal(true);
  protected readonly productsError = signal<string | null>(null);

  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly items = this.fb.array<InvoiceItemGroup>(
    [],
    [atLeastOneItemValidator, duplicateProductsValidator],
  );

  protected readonly form = this.fb.group({
    items: this.items,
  });

  protected readonly itemsValue: Signal<readonly { productId?: number | null }[]> = toSignal(
    this.items.valueChanges,
    { initialValue: [] },
  );

  protected readonly duplicateProductIds: Signal<ReadonlySet<number>> = computed(() => {
    const counts = new Map<number, number>();
    for (const item of this.itemsValue()) {
      if (item.productId === null || item.productId === undefined) {
        continue;
      }
      counts.set(item.productId, (counts.get(item.productId) ?? 0) + 1);
    }
    return new Set([...counts.entries()].filter(([, count]) => count > 1).map(([id]) => id));
  });

  constructor() {
    this.loadProducts();
  }

  protected balanceFor(productId: number | null): number | null {
    if (productId === null || productId === undefined) {
      return null;
    }
    return this.products().find((product) => product.id === productId)?.balance ?? null;
  }

  protected isDuplicate(index: number): boolean {
    const productId = this.itemsValue()[index]?.productId;
    return (
      productId !== null && productId !== undefined && this.duplicateProductIds().has(productId)
    );
  }

  protected reloadProducts(): void {
    this.loadProducts();
  }

  protected addItem(): void {
    this.items.push(
      this.fb.nonNullable.group({
        productId: this.fb.control<number | null>(null, Validators.required),
        quantity: this.fb.nonNullable.control(1, [
          Validators.required,
          Validators.min(1),
          integerValidator,
        ]),
      }),
    );
  }

  protected removeItem(index: number): void {
    this.items.removeAt(index);
  }

  protected submit(): void {
    this.formError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formError.set(this.buildClientValidationMessage());
      return;
    }

    this.submitting.set(true);

    const request: CreateInvoiceRequest = {
      items: this.items.controls.map((group) => {
        const value = group.getRawValue();
        return { productId: value.productId as number, quantity: value.quantity };
      }),
    };

    this.invoicesService
      .create(request)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (invoice) => this.handleCreateSuccess(invoice),
        error: (error: HttpErrorResponse) => this.handleCreateError(error),
      });
  }

  private buildClientValidationMessage(): string {
    if (this.items.hasError('required')) {
      return 'Adicione ao menos um item à nota.';
    }
    if (this.items.hasError('duplicateProducts')) {
      return 'Existem produtos duplicados na nota. Remova as duplicidades antes de continuar.';
    }
    return 'Verifique os campos destacados e tente novamente.';
  }

  private loadProducts(): void {
    this.productsLoading.set(true);
    this.productsError.set(null);

    this.productsService
      .getAll()
      .pipe(
        finalize(() => this.productsLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (products) =>
          this.products.set([...products].sort((a, b) => a.code.localeCompare(b.code))),
        error: () =>
          this.productsError.set(
            'Não foi possível carregar os produtos disponíveis. Tente novamente.',
          ),
      });
  }

  private handleCreateSuccess(invoice: Invoice): void {
    this.notification.success(`Nota Nº ${invoice.number} criada com sucesso.`);
    this.router.navigate(['/notas']);
  }

  private handleCreateError(error: HttpErrorResponse): void {
    if (error.status === 400) {
      const errors: string[] = error.error?.errors?.invoice ?? [];
      this.formError.set(
        errors.length > 0
          ? errors.join(' ')
          : 'Dados inválidos. Verifique os itens e tente novamente.',
      );
      return;
    }

    if (error.status === 404) {
      this.formError.set(
        error.error?.detail ?? 'Um dos produtos selecionados não foi encontrado no estoque.',
      );
      return;
    }

    if (error.status === 409) {
      this.formError.set(error.error?.detail ?? 'Conflito ao registrar a nota. Tente novamente.');
      return;
    }

    if (error.status === 0 || error.status === 503) {
      this.formError.set(
        'Serviço de faturamento indisponível no momento. Tente novamente em instantes.',
      );
      return;
    }

    this.formError.set('Não foi possível cadastrar a nota. Tente novamente.');
  }
}
