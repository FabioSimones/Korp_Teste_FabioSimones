import { Routes } from '@angular/router';

import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'produtos' },
      {
        path: 'produtos',
        loadComponent: () =>
          import('./features/products/products-page').then((m) => m.ProductsPage),
      },
      {
        path: 'notas',
        loadComponent: () =>
          import('./features/invoices/invoices-list/invoices-list-page').then(
            (m) => m.InvoicesListPage,
          ),
      },
      {
        path: 'notas/nova',
        loadComponent: () =>
          import('./features/invoices/invoice-form/invoice-form-page').then(
            (m) => m.InvoiceFormPage,
          ),
      },
      {
        path: 'notas/:id',
        loadComponent: () =>
          import('./features/invoices/invoice-detail/invoice-detail-page').then(
            (m) => m.InvoiceDetailPage,
          ),
      },
      {
        path: '**',
        loadComponent: () => import('./not-found/not-found').then((m) => m.NotFound),
      },
    ],
  },
];
