import { TestBed } from '@angular/core/testing';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { NotificationService } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;
  let snackBar: MatSnackBar;
  let openSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatSnackBarModule],
      providers: [provideNoopAnimations()],
    });
    service = TestBed.inject(NotificationService);
    snackBar = TestBed.inject(MatSnackBar);
    openSpy = vi.spyOn(snackBar, 'open');
  });

  it('should open a success toast: "notification-success" panel class, 4s duration, polite politeness, "Fechar" action', () => {
    service.success('Produto cadastrado com sucesso.');

    expect(openSpy).toHaveBeenCalledWith(
      'Produto cadastrado com sucesso.',
      'Fechar',
      expect.objectContaining({
        panelClass: 'notification-success',
        duration: 4000,
        politeness: 'polite',
        horizontalPosition: 'end',
        verticalPosition: 'top',
      }),
    );
  });

  it('should open an error toast: "notification-error" panel class, a longer (7s) duration and assertive politeness', () => {
    service.error('Não foi possível concluir a operação.');

    expect(openSpy).toHaveBeenCalledWith(
      'Não foi possível concluir a operação.',
      'Fechar',
      expect.objectContaining({
        panelClass: 'notification-error',
        duration: 7000,
        politeness: 'assertive',
      }),
    );
  });

  it('should open a warning toast: "notification-warning" panel class, 6s duration and polite politeness', () => {
    service.warning('Confira os dados antes de continuar.');

    expect(openSpy).toHaveBeenCalledWith(
      'Confira os dados antes de continuar.',
      'Fechar',
      expect.objectContaining({
        panelClass: 'notification-warning',
        duration: 6000,
        politeness: 'polite',
      }),
    );
  });

  it('should open an info toast: "notification-info" panel class, 4s duration and polite politeness', () => {
    service.info('Nenhuma ação necessária.');

    expect(openSpy).toHaveBeenCalledWith(
      'Nenhuma ação necessária.',
      'Fechar',
      expect.objectContaining({
        panelClass: 'notification-info',
        duration: 4000,
        politeness: 'polite',
      }),
    );
  });

  it('should always pass "Fechar" as the action label (an accessible, clearly-labeled close button)', () => {
    service.success('ok');
    service.error('erro');
    service.warning('atenção');
    service.info('info');

    for (const call of openSpy.mock.calls) {
      expect(call[1]).toBe('Fechar');
    }
  });

  it('should error out longer than success/info/warning so users have more time to read the reason', () => {
    service.success('a');
    service.warning('b');
    service.info('c');
    service.error('d');

    const durations = openSpy.mock.calls.map(
      (call: unknown[]) => (call[2] as { duration: number }).duration,
    );
    const [successDuration, warningDuration, infoDuration, errorDuration] = durations;

    expect(errorDuration).toBeGreaterThan(successDuration);
    expect(errorDuration).toBeGreaterThan(warningDuration);
    expect(errorDuration).toBeGreaterThan(infoDuration);
  });
});
