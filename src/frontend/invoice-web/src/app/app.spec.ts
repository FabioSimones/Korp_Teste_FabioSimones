import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { App } from './app';
import { routes } from './app.routes';

describe('App routing', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
      ],
    }).compileComponents();
  });

  it('should redirect the empty path to Produtos', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');

    expect(harness.routeNativeElement?.querySelector('#products-title')).toBeTruthy();
  });

  it('should navigate to the Notas placeholder', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/notas');

    expect(harness.routeNativeElement?.querySelector('#invoices-title')).toBeTruthy();
  });

  it('should render the not-found page for an unknown route', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/rota-inexistente');

    expect(harness.routeNativeElement?.querySelector('#not-found-title')).toBeTruthy();
  });
});
