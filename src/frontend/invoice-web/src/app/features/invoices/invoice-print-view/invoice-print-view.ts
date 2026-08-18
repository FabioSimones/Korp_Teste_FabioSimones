import { DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';

import { Invoice } from '../models/invoice';

/**
 * Print-only representation of an invoice: hidden on screen (see
 * `invoice-print-view.scss`) and shown only inside `@media print`, so
 * `window.print()` renders a clean document without the surrounding page
 * chrome (navigation, buttons, spinners).
 */
@Component({
  selector: 'app-invoice-print-view',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './invoice-print-view.html',
  styleUrl: './invoice-print-view.scss',
})
export class InvoicePrintView {
  @Input({ required: true }) invoice!: Invoice;
}
