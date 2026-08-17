import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

const DEFAULT_DURATION_MS = 4000;

/**
 * Thin wrapper around MatSnackBar so business features never depend on
 * Angular Material APIs directly. Business logic will be added by the
 * features that consume this service (products, invoices, printing, etc.).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.show(message, 'notification-success');
  }

  error(message: string): void {
    this.show(message, 'notification-error', 6000);
  }

  info(message: string): void {
    this.show(message, 'notification-info');
  }

  private show(message: string, panelClass: string, duration = DEFAULT_DURATION_MS): void {
    this.snackBar.open(message, 'Fechar', {
      duration,
      panelClass,
      horizontalPosition: 'end',
      verticalPosition: 'top',
    });
  }
}
