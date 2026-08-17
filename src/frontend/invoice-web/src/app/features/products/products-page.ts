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
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { finalize } from 'rxjs';

import { NotificationService } from '../../core/services/notification.service';
import { Product } from './models/product';
import { ProductsService } from './products.service';

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
 * Registers new products and lists the ones already registered, consuming
 * Inventory.Api directly. Editing, deletion, invoices and printing are out
 * of scope for this feature.
 */
@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  templateUrl: './products-page.html',
  styleUrl: './products-page.scss',
})
export class ProductsPage {
  private readonly fb = inject(FormBuilder);
  private readonly productsService = inject(ProductsService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly displayedColumns = ['code', 'description', 'balance'] as const;

  protected readonly products = signal<Product[]>([]);
  protected readonly loading = signal(true);
  protected readonly listError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    balance: this.fb.nonNullable.control(0, [
      Validators.required,
      Validators.min(0),
      integerValidator,
    ]),
  });

  constructor() {
    this.loadProducts();
  }

  protected reload(): void {
    this.loadProducts();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.formError.set(null);
    this.submitting.set(true);

    const { code, description, balance } = this.form.getRawValue();

    this.productsService
      .create({ code: code.trim(), description: description.trim(), balance })
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (product) => this.handleCreateSuccess(product),
        error: (error: HttpErrorResponse) => this.handleCreateError(error),
      });
  }

  private loadProducts(): void {
    this.loading.set(true);
    this.listError.set(null);

    this.productsService
      .getAll()
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (products) => this.products.set(products),
        error: () =>
          this.listError.set('Não foi possível carregar a lista de produtos. Tente novamente.'),
      });
  }

  private handleCreateSuccess(product: Product): void {
    this.products.update((current) =>
      [...current, product].sort((a, b) => a.code.localeCompare(b.code)),
    );
    this.form.reset({ code: '', description: '', balance: 0 });
    this.notification.success(`Produto "${product.code}" cadastrado com sucesso.`);
  }

  private handleCreateError(error: HttpErrorResponse): void {
    if (error.status === 409) {
      this.form.controls.code.setErrors({ duplicate: true });
      this.form.controls.code.markAsTouched();
      this.formError.set(error.error?.detail ?? 'Este código de produto já está cadastrado.');
      return;
    }

    if (error.status === 400) {
      const errors: string[] = error.error?.errors?.product ?? [];
      this.formError.set(
        errors.length > 0
          ? errors.join(' ')
          : 'Dados inválidos. Verifique os campos e tente novamente.',
      );
      return;
    }

    if (error.status === 0 || error.status === 503) {
      this.formError.set(
        'Serviço de estoque indisponível no momento. Tente novamente em instantes.',
      );
      return;
    }

    this.formError.set('Não foi possível cadastrar o produto. Tente novamente.');
  }
}
