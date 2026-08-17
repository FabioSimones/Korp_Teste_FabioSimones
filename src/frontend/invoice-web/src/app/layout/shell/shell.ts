import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';

interface NavLink {
  readonly label: string;
  readonly path: string;
  readonly icon: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly breakpointObserver = inject(BreakpointObserver);

  protected readonly navLinks: readonly NavLink[] = [
    { label: 'Produtos', path: '/produtos', icon: 'inventory_2' },
    { label: 'Notas', path: '/notas', icon: 'receipt_long' },
  ];

  protected readonly isHandset = signal(false);

  constructor() {
    this.breakpointObserver
      .observe(Breakpoints.Handset)
      .pipe(takeUntilDestroyed())
      .subscribe((result) => this.isHandset.set(result.matches));
  }
}
