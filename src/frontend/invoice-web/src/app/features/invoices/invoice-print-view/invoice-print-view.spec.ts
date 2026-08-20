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

  it('should mark the Quantidade header and value with the numeric column class, and Código/Descrição with the text column class', () => {
    const headerCells: HTMLTableCellElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.invoice-print-view__table thead th'),
    );
    const quantidadeHeader = headerCells.find((th) => th.textContent?.trim() === 'Quantidade')!;
    expect(quantidadeHeader.classList).toContain('invoice-print-view__col-numeric');

    const codigoHeader = headerCells.find((th) => th.textContent?.trim() === 'Código')!;
    const descricaoHeader = headerCells.find((th) => th.textContent?.trim() === 'Descrição')!;
    expect(codigoHeader.classList).toContain('invoice-print-view__col-text');
    expect(descricaoHeader.classList).toContain('invoice-print-view__col-text');

    const quantidadeCells: HTMLTableCellElement[] = Array.from(
      fixture.nativeElement.querySelectorAll(
        '.invoice-print-view__table tbody td.invoice-print-view__col-numeric',
      ),
    );
    expect(quantidadeCells.length).toBe(invoice.items.length);
    expect(quantidadeCells[0].textContent?.trim()).toBe('3');
  });
});
