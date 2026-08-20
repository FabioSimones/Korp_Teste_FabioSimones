import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

type NotificationKind = 'success' | 'error' | 'warning' | 'info';

interface NotificationSpec {
  readonly panelClass: string;
  readonly duration: number;
  readonly politeness: 'polite' | 'assertive';
}

// Duration and politeness follow the "Folha de Trabalho" feedback pattern
// (see docs/technical-details.md): errors stay longer and are announced
// assertively (they interrupt), everything else is announced politely.
// Colors/shape for each `panelClass` are defined in `styles.scss` using
// Angular Material's official snack-bar token overrides
// (`--mat-snack-bar-*` custom properties on `.mat-mdc-snack-bar-container`),
// not `::ng-deep`, since the snack bar is rendered in a CDK overlay outside
// this app's component tree and is reachable from the global stylesheet.
const SPECS: Record<NotificationKind, NotificationSpec> = {
  success: { panelClass: 'notification-success', duration: 4000, politeness: 'polite' },
  error: { panelClass: 'notification-error', duration: 7000, politeness: 'assertive' },
  warning: { panelClass: 'notification-warning', duration: 6000, politeness: 'polite' },
  info: { panelClass: 'notification-info', duration: 4000, politeness: 'polite' },
};

/**
 * Thin wrapper around `MatSnackBar` so feature code never depends on
 * Angular Material's snack bar APIs directly. Every screen that surfaces a
 * result to the user (product/invoice creation, printing, load failures,
 * etc.) goes through one of the four methods below instead of calling
 * `MatSnackBar` itself, keeping notification look/duration/accessibility
 * consistent across the app.
 *
 * Messages are expected to already be complete, user-facing Portuguese
 * sentences (see `getUserFriendlyErrorMessage` for errors) — this service
 * only owns *how* a message is presented, not its wording.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  /** Green toast for an operation that has already been confirmed by the backend (HTTP success). */
  success(message: string): void {
    this.show('success', message);
  }

  /** Red toast for a failed operation. Duration is longer so users have time to read the reason. */
  error(message: string): void {
    this.show('error', message);
  }

  /** Amber toast for a non-blocking caution (no destructive action, nothing failed). */
  warning(message: string): void {
    this.show('warning', message);
  }

  /** Steel-blue toast for neutral, non-actionable information. */
  info(message: string): void {
    this.show('info', message);
  }

  private show(kind: NotificationKind, message: string): void {
    const spec = SPECS[kind];
    this.snackBar.open(message, 'Fechar', {
      duration: spec.duration,
      panelClass: spec.panelClass,
      politeness: spec.politeness,
      // Desktop: top-right corner, out of the way of page titles/actions.
      // On narrow viewports Angular Material automatically switches the
      // container to `mat-mdc-snack-bar-handset` (full width), which
      // `styles.scss` keeps anchored to the top via the same
      // `verticalPosition`.
      horizontalPosition: 'end',
      verticalPosition: 'top',
    });
  }
}
