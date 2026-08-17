import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { CreateProductRequest, Product } from './models/product';
import { ProductsService } from './products.service';

describe('ProductsService', () => {
  let service: ProductsService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.inventoryApiUrl}/api/products`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(ProductsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should GET the product list from Inventory.Api', () => {
    const products: Product[] = [{ id: 1, code: 'A1', description: 'Produto A', balance: 10 }];

    let result: Product[] | undefined;
    service.getAll().subscribe((response) => (result = response));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush(products);

    expect(result).toEqual(products);
  });

  it('should POST a new product to Inventory.Api', () => {
    const request: CreateProductRequest = { code: 'A1', description: 'Produto A', balance: 10 };
    const response: Product = { id: 1, ...request };

    let result: Product | undefined;
    service.create(request).subscribe((created) => (result = created));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(response, { status: 201, statusText: 'Created' });

    expect(result).toEqual(response);
  });
});
