import { Component } from '@angular/core';

/**
 * Placeholder route for the Invoices feature.
 * Forms, listings and printing will be implemented in future tasks.
 */
@Component({
  selector: 'app-invoices-page',
  standalone: true,
  template: `
    <section aria-labelledby="invoices-title">
      <h1 id="invoices-title">Notas</h1>
      <p>O cadastro, a listagem e a impressão de notas serão implementados em uma próxima etapa.</p>
    </section>
  `,
})
export class InvoicesPage {}
