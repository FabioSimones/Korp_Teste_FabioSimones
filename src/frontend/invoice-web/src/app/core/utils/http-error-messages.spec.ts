import { HttpErrorResponse } from '@angular/common/http';

import { getUserFriendlyErrorMessage } from './http-error-messages';

function errorResponse(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('getUserFriendlyErrorMessage', () => {
  it('should resolve the real backend INSUFFICIENT_STOCK payload by surfacing its (already Portuguese) detail', () => {
    // Exact shape returned by Billing.Api when printing fails for lack of stock.
    const error = errorResponse(409, {
      title: 'Saldo de estoque insuficiente.',
      status: 409,
      detail: 'O produto "SKU-PRINT-3" não possui saldo suficiente. Disponível: 2; solicitado: 5.',
      errorCode: 'INSUFFICIENT_STOCK',
      traceId: 'abc-123',
    });

    expect(getUserFriendlyErrorMessage(error, 'invoice-print')).toBe(
      'O produto "SKU-PRINT-3" não possui saldo suficiente. Disponível: 2; solicitado: 5.',
    );
  });

  it('should fall back to a canned message for INSUFFICIENT_STOCK when detail is missing', () => {
    const error = errorResponse(409, { errorCode: 'INSUFFICIENT_STOCK' });

    expect(getUserFriendlyErrorMessage(error)).toBe(
      'O produto selecionado não possui saldo suficiente.',
    );
  });

  it.each([
    ['PRODUCT_NOT_FOUND', 'Produto não encontrado no estoque.'],
    ['DUPLICATE_PRODUCT_CODE', 'Este código de produto já está cadastrado.'],
    ['INVALID_PRODUCT', 'Dados do produto inválidos. Confira os campos e tente novamente.'],
    ['INVALID_STOCK_DEBIT', 'Quantidade inválida para a baixa de estoque.'],
    ['INVOICE_NOT_FOUND', 'Nota não encontrada.'],
    ['INVOICE_ALREADY_CLOSED', 'Esta nota já foi fechada anteriormente.'],
    ['INVALID_INVOICE', 'Dados da nota inválidos. Confira os itens e tente novamente.'],
    [
      'INVENTORY_UNAVAILABLE',
      'O serviço de estoque está temporariamente indisponível. Tente novamente em alguns instantes.',
    ],
    ['INVENTORY_TIMEOUT', 'O serviço demorou mais que o esperado para responder. Tente novamente.'],
    ['DUPLICATE_INVOICE_NUMBER', 'Não foi possível gerar o número da nota. Tente novamente.'],
    [
      'INVALID_PAGINATION',
      'Parâmetros de paginação inválidos. Recarregue a página e tente novamente.',
    ],
  ])(
    'should resolve errorCode %s to its Portuguese message, ignoring any raw detail',
    (code, expected) => {
      const error = errorResponse(409, {
        errorCode: code,
        detail: 'raw untranslated backend text',
      });

      expect(getUserFriendlyErrorMessage(error)).toBe(expected);
    },
  );

  it('should never surface raw/English detail text when errorCode is unknown', () => {
    const error = errorResponse(409, {
      errorCode: 'SOME_FUTURE_CODE',
      detail: 'Http failure response for /api/x: 409 Conflict',
    });

    expect(getUserFriendlyErrorMessage(error)).not.toContain('Http failure');
  });

  it('should fall back on status when errorCode is absent', () => {
    expect(getUserFriendlyErrorMessage(errorResponse(400))).toBe(
      'Confira os dados informados e tente novamente.',
    );
    expect(getUserFriendlyErrorMessage(errorResponse(404))).toBe(
      'O registro solicitado não foi encontrado.',
    );
    expect(getUserFriendlyErrorMessage(errorResponse(500))).toBe(
      'Não foi possível concluir a operação. Tente novamente.',
    );
  });

  it('should treat status 0 and 503 the same way (service unreachable/unavailable)', () => {
    expect(getUserFriendlyErrorMessage(errorResponse(0))).toContain('indisponível');
    expect(getUserFriendlyErrorMessage(errorResponse(503))).toContain('indisponível');
  });

  it('should use a distinct "demorou" wording for status 504', () => {
    expect(getUserFriendlyErrorMessage(errorResponse(504))).toContain('demorou');
  });

  it('should use a context-specific 409 fallback when no errorCode is present', () => {
    expect(getUserFriendlyErrorMessage(errorResponse(409), 'product-create')).toBe(
      'Este código de produto já está cadastrado.',
    );
    expect(getUserFriendlyErrorMessage(errorResponse(409), 'invoice-create')).toContain(
      'registrar a nota',
    );
    expect(getUserFriendlyErrorMessage(errorResponse(409), 'invoice-print')).toContain('impressão');
    // No context at all still gets a safe, generic Portuguese sentence.
    expect(getUserFriendlyErrorMessage(errorResponse(409))).toContain('conflito');
  });
});
