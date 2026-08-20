import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { PagedResponse } from '../../shared/pagination/paged-response';
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

  it('should GET a paged product list with pageNumber/pageSize/sortBy/sortDirection as query params', () => {
    const response: PagedResponse<Product> = {
      items: [{ id: 1, code: 'A1', description: 'Produto A', balance: 10 }],
      pageNumber: 2,
      pageSize: 5,
      totalCount: 12,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    };

    let result: PagedResponse<Product> | undefined;
    service.getPaged(2, 5, 'balance', 'desc').subscribe((value) => (result = value));

    const req = httpMock.expectOne(
      `${baseUrl}/paged?pageNumber=2&pageSize=5&sortBy=balance&sortDirection=desc`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('pageNumber')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('5');
    expect(req.request.params.get('sortBy')).toBe('balance');
    expect(req.request.params.get('sortDirection')).toBe('desc');
    req.flush(response);

    expect(result).toEqual(response);
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
