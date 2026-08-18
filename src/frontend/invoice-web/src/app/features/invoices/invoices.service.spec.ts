import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { CreateInvoiceRequest, Invoice } from './models/invoice';
import { InvoicesService } from './invoices.service';

describe('InvoicesService', () => {
  let service: InvoicesService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.billingApiUrl}/api/invoices`;

  const sampleInvoice: Invoice = {
    id: 1,
    number: 1001,
    status: 'Open',
    createdAtUtc: '2026-08-17T10:00:00Z',
    closedAtUtc: null,
    items: [
      { id: 1, productId: 1, productCode: 'A1', productDescription: 'Produto A', quantity: 2 },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(InvoicesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should GET the invoice list from Billing.Api', () => {
    let result: Invoice[] | undefined;
    service.getAll().subscribe((response) => (result = response));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([sampleInvoice]);

    expect(result).toEqual([sampleInvoice]);
  });

  it('should GET a single invoice by id from Billing.Api', () => {
    let result: Invoice | undefined;
    service.getById(1).subscribe((response) => (result = response));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sampleInvoice);

    expect(result).toEqual(sampleInvoice);
  });

  it('should POST a new invoice to Billing.Api', () => {
    const request: CreateInvoiceRequest = { items: [{ productId: 1, quantity: 2 }] };

    let result: Invoice | undefined;
    service.create(request).subscribe((created) => (result = created));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(sampleInvoice, { status: 201, statusText: 'Created' });

    expect(result).toEqual(sampleInvoice);
  });

  it('should POST to the print endpoint and return the closed invoice', () => {
    const closedInvoice: Invoice = {
      ...sampleInvoice,
      status: 'Closed',
      closedAtUtc: '2026-08-18T10:00:00Z',
    };

    let result: Invoice | undefined;
    service.print(1).subscribe((response) => (result = response));

    const req = httpMock.expectOne(`${baseUrl}/1/print`);
    expect(req.request.method).toBe('POST');
    req.flush(closedInvoice);

    expect(result).toEqual(closedInvoice);
  });
});
