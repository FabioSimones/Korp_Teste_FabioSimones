import { NgTemplateOutlet } from '@angular/common';
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
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';

import { ErrorContext, getUserFriendlyErrorMessage } from '../../../core/utils/http-error-messages';
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
 * Builds a `ValidatorFn` for a single row's quantity control that flags the
 * quantity as invalid when it exceeds `getBalance()` (the *currently*
 * selected product's balance for that same row — each row is validated
 * against its own product, never a shared/global balance).
 *
 * This is a client-side UX guard only, not a transactional guarantee: the
 * balance snapshot comes from the product list loaded when the form opened,
 * so it can go stale between then and submission (or between submission and
 * printing). Billing.Api remains the source of truth and can still reject a
 * valid-looking form with `409 INSUFFICIENT_STOCK` if the real balance
 * changed meanwhile — that response is handled separately in
 * `handleCreateError`.
 *
 * `getBalance` is a callback (not a fixed number) so the same `ValidatorFn`
 * instance keeps re-reading the row's live selection/balance every time
 * Angular re-runs validation (see `addItem`, where it is wired to always
 * read the row's current `productId` control value against the latest
 * `products()` signal).
 */
function quantityWithinBalanceValidator(getBalance: () => number | null): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const quantity = Number(value);
    if (!Number.isInteger(quantity) || quantity <= 0) {
      // required/min/integer validators already report these cases.
      return null;
    }

    const balance = getBalance();
    if (balance === null) {
      // No product selected yet (or product not found in the loaded list):
      // nothing to validate against.
      return null;
    }

    if (balance <= 0) {
      return { noBalance: true };
    }

    return quantity > balance ? { exceedsBalance: { balance } } : null;
  };
}

/**
 * Registers a new invoice with one or more product lines, consuming
 * Billing.Api for the write and Inventory.Api (via `ProductsService`) to
 * populate the product picker and show each product's available balance.
 *
 * Single implementation reused in two contexts:
 * - As a route (`/notas/nova`, via `InvoiceFormPage`), where `MatDialogRef`
 *   is not available (not opened through `MatDialog`): on success it
 *   notifies and navigates back to `/notas`.
 * - As a `MatDialog` opened from `InvoicesListPage`: `MatDialogRef` is
 *   injected automatically by Angular Material, which this component
 *   detects (optional injection) to render its own title/close button and
 *   a "Cancelar" action, and to close with the created invoice on success
 *   instead of navigating.
 */
@Component({
  selector: 'app-invoice-form',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './invoice-form.html',
  styleUrl: './invoice-form.scss',
})
export class InvoiceFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly invoicesService = inject(InvoicesService);
  private readonly productsService = inject(ProductsService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Present only when opened through `MatDialog.open(InvoiceFormComponent)`.
   * Its (absence) is what distinguishes the dialog and the standalone-route
   * rendering modes described in the class docs above.
   */
  protected readonly dialogRef = inject(MatDialogRef<InvoiceFormComponent, Invoice>, {
    optional: true,
  });

  protected readonly isDialog = this.dialogRef !== null;

  private readonly errorContext: ErrorContext = 'invoice-create';

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
    const productId = this.fb.control<number | null>(null, Validators.required);
    const quantity = this.fb.nonNullable.control(1, [
      Validators.required,
      Validators.min(1),
      integerValidator,
      quantityWithinBalanceValidator(() => this.balanceFor(productId.value)),
    ]);

    // Rule 1: revalidate this row's quantity whenever this row's own
    // product selection changes (each row's quantity validator always
    // reads `productId.value` above, but the control itself only
    // re-runs validation when told to).
    productId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => quantity.updateValueAndValidity());

    this.items.push(this.fb.group({ productId, quantity }) as InvoiceItemGroup);
  }

  protected removeItem(index: number): void {
    this.items.removeAt(index);
  }

  protected cancel(): void {
    if (this.submitting() || !this.dialogRef) {
      return;
    }
    this.dialogRef.close();
  }

  protected submit(): void {
    this.formError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formError.set(this.buildClientValidationMessage());
      return;
    }

    this.submitting.set(true);
    if (this.dialogRef) {
      // Prevent accidental dismissal (Escape/backdrop) while the request is
      // in flight, so the dialog never closes with the POST left dangling.
      this.dialogRef.disableClose = true;
    }

    const request: CreateInvoiceRequest = {
      items: this.items.controls.map((group) => {
        const value = group.getRawValue();
        return { productId: value.productId as number, quantity: value.quantity };
      }),
    };

    this.invoicesService
      .create(request)
      .pipe(
        finalize(() => {
          this.submitting.set(false);
          if (this.dialogRef) {
            this.dialogRef.disableClose = false;
          }
        }),
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
        next: (products) => {
          this.products.set([...products].sort((a, b) => a.code.localeCompare(b.code)));
          // Rule 2: products arrive after the form/rows may already exist
          // (e.g. a reload triggered by "Tentar novamente"), so every
          // existing row's quantity must be re-checked against the
          // (possibly new) balance of its selected product.
          this.items.controls.forEach((group) => group.controls.quantity.updateValueAndValidity());
        },
        error: () =>
          this.productsError.set(
            'Não foi possível carregar os produtos disponíveis. Tente novamente.',
          ),
      });
  }

  private handleCreateSuccess(invoice: Invoice): void {
    this.notification.success(`Nota Nº ${invoice.number} criada com sucesso.`);

    if (this.dialogRef) {
      this.dialogRef.close(invoice);
      return;
    }

    this.router.navigate(['/notas']);
  }

  /**
   * Resolves a single, user-facing message via `getUserFriendlyErrorMessage`
   * and both renders it inline (`formError`) and shows it as a toast.
   * `InvoicesService` skips the interceptor's own notification for every
   * request (see its `SKIP_ERROR_NOTIFICATION` usage), so this is the
   * *only* toast shown for this failure — never a duplicate generic one on
   * top of it.
   */
  private handleCreateError(error: HttpErrorResponse): void {
    const message = getUserFriendlyErrorMessage(error, this.errorContext);
    this.formError.set(message);
    this.notification.error(message);
  }
}
