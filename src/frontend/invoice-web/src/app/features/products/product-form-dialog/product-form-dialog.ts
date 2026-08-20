import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';

import { getUserFriendlyErrorMessage } from '../../../core/utils/http-error-messages';
import { NotificationService } from '../../../core/services/notification.service';
import { Product } from '../models/product';
import { ProductsService } from '../products.service';

/**
 * Rejects non-integer numeric values (e.g. "2.5"). Empty values are
 * ignored here since `Validators.required` already covers that case.
 */
function integerValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === undefined || value === '') {
    return null;
  }
  return Number.isInteger(Number(value)) ? null : { integer: true };
}

/**
 * Modal dialog holding the "register a new product" form. Opened by
 * `ProductsPage` via `MatDialog`; the underlying registration logic
 * (validation, submit, error handling) lives here so it has a single
 * implementation.
 *
 * Closes with the newly created `Product` on success (so the opener can
 * refresh its listing), or with `undefined` on cancel/dismiss.
 */
@Component({
  selector: 'app-product-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './product-form-dialog.html',
  styleUrl: './product-form-dialog.scss',
})
export class ProductFormDialog {
  private readonly fb = inject(FormBuilder);
  private readonly productsService = inject(ProductsService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly dialogRef = inject(MatDialogRef<ProductFormDialog, Product>);

  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    balance: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(1),
      integerValidator,
    ]),
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.formError.set(null);
    this.submitting.set(true);
    // Prevent accidental dismissal (Escape/backdrop) while the request is in
    // flight, so the dialog never closes with the POST left dangling.
    this.dialogRef.disableClose = true;

    const { code, description, balance } = this.form.getRawValue();

    this.productsService
      .create({ code: code.trim(), description: description.trim(), balance: balance as number })
      .pipe(
        finalize(() => {
          this.submitting.set(false);
          this.dialogRef.disableClose = false;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (product) => this.handleCreateSuccess(product),
        error: (error: HttpErrorResponse) => this.handleCreateError(error),
      });
  }

  protected cancel(): void {
    if (this.submitting()) {
      return;
    }
    this.dialogRef.close();
  }

  private handleCreateSuccess(product: Product): void {
    this.notification.success(`Produto "${product.code}" cadastrado com sucesso.`);
    this.dialogRef.close(product);
  }

  /**
   * Resolves a single, user-facing message via `getUserFriendlyErrorMessage`
   * and both renders it inline (`formError`, plus the field-level "duplicate"
   * state on 409) and shows it as a toast. `ProductsService` skips the
   * interceptor's own notification for every request (see its
   * `SKIP_ERROR_NOTIFICATION` usage), so this is the *only* toast shown for
   * this failure — never a duplicate generic one on top of it.
   */
  private handleCreateError(error: HttpErrorResponse): void {
    const message = getUserFriendlyErrorMessage(error, 'product-create');

    if (error.status === 409) {
      this.form.controls.code.setErrors({ duplicate: true });
      this.form.controls.code.markAsTouched();
    }

    this.formError.set(message);
    this.notification.error(message);
  }
}
