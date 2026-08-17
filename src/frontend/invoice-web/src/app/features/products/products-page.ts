import { Component } from '@angular/core';

/**
 * Placeholder route for the Products feature.
 * Forms and listings will be implemented in a future task.
 */
@Component({
  selector: 'app-products-page',
  standalone: true,
  template: `
    <section aria-labelledby="products-title">
      <h1 id="products-title">Produtos</h1>
      <p>O cadastro e a listagem de produtos serão implementados em uma próxima etapa.</p>
    </section>
  `,
})
export class ProductsPage {}
