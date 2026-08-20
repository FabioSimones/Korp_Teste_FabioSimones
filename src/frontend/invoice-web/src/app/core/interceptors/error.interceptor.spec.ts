import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { NotificationService } from '../services/notification.service';
import { errorInterceptor } from './error.interceptor';
import { SKIP_ERROR_NOTIFICATION } from './skip-error-notification.token';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let notification: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    notification = { success: vi.fn(), error: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notification },
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should show a resolved, Portuguese error toast for a request with no SKIP_ERROR_NOTIFICATION', () => {
    httpClient.get('/api/whatever').subscribe({ error: () => undefined });

    httpMock
      .expectOne('/api/whatever')
      .flush({ detail: 'boom' }, { status: 503, statusText: 'Service Unavailable' });

    expect(notification.error).toHaveBeenCalledTimes(1);
    expect(notification.error).toHaveBeenCalledWith(expect.stringContaining('indisponível'));
  });

  it('should not show any toast for a request that opts out via SKIP_ERROR_NOTIFICATION (screen already handles it)', () => {
    const context = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
    httpClient.get('/api/whatever', { context }).subscribe({ error: () => undefined });

    httpMock
      .expectOne('/api/whatever')
      .flush({ detail: 'boom' }, { status: 409, statusText: 'Conflict' });

    expect(notification.error).not.toHaveBeenCalled();
  });

  it('should still forward the original error to the caller after notifying', () => {
    let received: unknown;
    httpClient.get('/api/whatever').subscribe({ error: (error) => (received = error) });

    httpMock
      .expectOne('/api/whatever')
      .flush({ errorCode: 'PRODUCT_NOT_FOUND' }, { status: 404, statusText: 'Not Found' });

    expect(received).toBeTruthy();
    expect(notification.error).toHaveBeenCalledWith('Produto não encontrado no estoque.');
  });
});
