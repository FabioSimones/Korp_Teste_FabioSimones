import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Invoice } from '../models/invoice';
import { InvoicePrintView } from './invoice-print-view';

describe('InvoicePrintView', () => {
  let fixture: ComponentFixture<InvoicePrintView>;

  const invoice: Invoice = {
    id: 1,
    number: 42,
    status: 'Closed',
    createdAtUtc: '2026-08-17T10:00:00Z',
    closedAtUtc: '2026-08-18T09:00:00Z',
    items: [
      { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 3 },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [InvoicePrintView] });
    fixture = TestBed.createComponent(InvoicePrintView);
    fixture.componentInstance.invoice = invoice;
    fixture.detectChanges();
  });

  it('should render the invoice number, status and closed date', () => {
    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('42');
    expect(content).toContain('Fechada');
  });

  it('should render each item row', () => {
    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('A1');
    expect(content).toContain('Produto A');
    expect(content).toContain('3');
  });
});
