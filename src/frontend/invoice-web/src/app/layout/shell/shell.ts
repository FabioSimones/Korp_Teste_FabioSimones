import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavLink {
  readonly label: string;
  readonly path: string;
}

/**
 * Application shell: a simple top navigation bar (brand + two links, always
 * visible, no drawer/sidenav at any breakpoint) plus a centered "paper"
 * content area, following the "Folha de Trabalho" visual direction (see
 * docs/technical-details.md).
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly navLinks: readonly NavLink[] = [
    { label: 'Produtos', path: '/produtos' },
    { label: 'Notas fiscais', path: '/notas' },
  ];
}
