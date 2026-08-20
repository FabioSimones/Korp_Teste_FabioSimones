import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { InvoiceFormComponent } from './invoice-form';

/**
 * Route wrapper for `/notas/nova`. Owns only the page chrome (title, "back
 * to listing" link); the registration form/validation/submit logic lives in
 * `InvoiceFormComponent`, shared with the `MatDialog` opened from
 * `InvoicesListPage` (see that component's docs for details).
 */
@Component({
  selector: 'app-invoice-form-page',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatCardModule, InvoiceFormComponent],
  templateUrl: './invoice-form-page.html',
  styleUrl: './invoice-form-page.scss',
})
export class InvoiceFormPage {}
