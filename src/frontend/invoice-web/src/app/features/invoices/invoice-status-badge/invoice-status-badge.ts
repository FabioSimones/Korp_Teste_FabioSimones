import { Component, Input } from '@angular/core';

import { InvoiceStatus } from '../models/invoice';

const STATUS_LABELS: Record<InvoiceStatus, string> = {
  Open: 'Aberta',
  Closed: 'Fechada',
};

/**
 * Small visual badge for an invoice status, shared by the list and detail
 * screens so the Open/Closed labeling and styling stay consistent.
 */
@Component({
  selector: 'app-invoice-status-badge',
  standalone: true,
  templateUrl: './invoice-status-badge.html',
  styleUrl: './invoice-status-badge.scss',
})
export class InvoiceStatusBadge {
  @Input({ required: true }) status!: InvoiceStatus;

  protected get label(): string {
    return STATUS_LABELS[this.status];
  }
}
