import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InvoiceStatusBadge } from './invoice-status-badge';

describe('InvoiceStatusBadge', () => {
  let fixture: ComponentFixture<InvoiceStatusBadge>;

  function setup(status: 'Open' | 'Closed'): void {
    TestBed.configureTestingModule({ imports: [InvoiceStatusBadge] });
    fixture = TestBed.createComponent(InvoiceStatusBadge);
    fixture.componentInstance.status = status;
    fixture.detectChanges();
  }

  it('should render "Aberta" for Open invoices', () => {
    setup('Open');

    const badge = fixture.nativeElement.querySelector('.invoice-status-badge');
    expect(badge.textContent.trim()).toBe('Aberta');
    expect(badge.classList).not.toContain('invoice-status-badge--closed');
  });

  it('should render "Fechada" for Closed invoices', () => {
    setup('Closed');

    const badge = fixture.nativeElement.querySelector('.invoice-status-badge');
    expect(badge.textContent.trim()).toBe('Fechada');
    expect(badge.classList).toContain('invoice-status-badge--closed');
  });
});
