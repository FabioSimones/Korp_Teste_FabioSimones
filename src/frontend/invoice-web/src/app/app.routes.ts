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
          import('./features/invoices/invoices-page').then((m) => m.InvoicesPage),
      },
      {
        path: '**',
        loadComponent: () => import('./not-found/not-found').then((m) => m.NotFound),
      },
    ],
  },
];
