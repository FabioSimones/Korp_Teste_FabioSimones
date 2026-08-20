import { HttpErrorResponse } from '@angular/common/http';

/**
 * Identifies the screen/operation an HTTP error came from, so the same
 * `errorCode`/status combination can be worded differently depending on
 * context (see `CONTEXT_CONFLICT_FALLBACK` below).
 */
export type ErrorContext =
  | 'product-create'
  | 'product-list'
  | 'invoice-create'
  | 'invoice-list'
  | 'invoice-detail'
  | 'invoice-print';

/**
 * `errorCode` values Inventory.Api and Billing.Api add to their
 * `ProblemDetails` responses. Confirmed against the implemented backend:
 * - Inventory.Api: `PRODUCT_NOT_FOUND`, `DUPLICATE_PRODUCT_CODE`,
 *   `INVALID_PRODUCT`, `INSUFFICIENT_STOCK`, `INVALID_STOCK_DEBIT`,
 *   `INVALID_PAGINATION`.
 * - Billing.Api: `INVALID_INVOICE`, `INVOICE_NOT_FOUND`, `PRODUCT_NOT_FOUND`,
 *   `DUPLICATE_INVOICE_NUMBER`, `INVOICE_ALREADY_CLOSED`,
 *   `INSUFFICIENT_STOCK`, `INVENTORY_UNAVAILABLE`, `INVENTORY_TIMEOUT`,
 *   `INVALID_PAGINATION`.
 * (There is no separate `INVOICE_PRINT_CONFLICT` code: a failed print is
 * always one of `INVOICE_ALREADY_CLOSED` or `INSUFFICIENT_STOCK`, or a
 * plain 409/503 with no code — see `CONTEXT_CONFLICT_FALLBACK` below.)
 *
 * Kept as a plain string union (not an enum) so an unrecognized/future code
 * simply falls through to the status-based fallback instead of failing.
 */
type KnownErrorCode =
  | 'PRODUCT_NOT_FOUND'
  | 'DUPLICATE_PRODUCT_CODE'
  | 'INVALID_PRODUCT'
  | 'INSUFFICIENT_STOCK'
  | 'INVALID_STOCK_DEBIT'
  | 'INVOICE_NOT_FOUND'
  | 'INVOICE_ALREADY_CLOSED'
  | 'INVALID_INVOICE'
  | 'INVENTORY_UNAVAILABLE'
  | 'INVENTORY_TIMEOUT'
  | 'DUPLICATE_INVOICE_NUMBER'
  | 'INVALID_PAGINATION';

/**
 * `detail` is only trusted for `INSUFFICIENT_STOCK`: the contract documents
 * it as an already-translated, user-safe sentence naming the product and
 * the available/requested quantities (e.g. `O produto "ABC-001" não possui
 * saldo suficiente. Disponível: 3; solicitado: 4.`). For every other case we
 * never surface `detail` directly, since it may be untranslated, may be
 * `undefined`, or may leak implementation details.
 */
function safeDetail(error: HttpErrorResponse): string | null {
  const detail = (error.error as { detail?: unknown } | null)?.detail;
  return typeof detail === 'string' && detail.trim().length > 0 ? detail : null;
}

const ERROR_CODE_MESSAGES: Record<KnownErrorCode, (error: HttpErrorResponse) => string> = {
  PRODUCT_NOT_FOUND: () => 'Produto não encontrado no estoque.',
  DUPLICATE_PRODUCT_CODE: () => 'Este código de produto já está cadastrado.',
  INVALID_PRODUCT: () => 'Dados do produto inválidos. Confira os campos e tente novamente.',
  INSUFFICIENT_STOCK: (error) =>
    safeDetail(error) ?? 'O produto selecionado não possui saldo suficiente.',
  INVALID_STOCK_DEBIT: () => 'Quantidade inválida para a baixa de estoque.',
  INVOICE_NOT_FOUND: () => 'Nota não encontrada.',
  INVOICE_ALREADY_CLOSED: () => 'Esta nota já foi fechada anteriormente.',
  INVALID_INVOICE: () => 'Dados da nota inválidos. Confira os itens e tente novamente.',
  INVENTORY_UNAVAILABLE: () =>
    'O serviço de estoque está temporariamente indisponível. Tente novamente em alguns instantes.',
  INVENTORY_TIMEOUT: () => 'O serviço demorou mais que o esperado para responder. Tente novamente.',
  DUPLICATE_INVOICE_NUMBER: () => 'Não foi possível gerar o número da nota. Tente novamente.',
  INVALID_PAGINATION: () =>
    'Parâmetros de paginação inválidos. Recarregue a página e tente novamente.',
};

/**
 * Generic 409 wording used when the backend has not sent a recognized
 * `errorCode` for that context. Kept per-context because "conflict" reads
 * very differently on a product form versus an invoice print action.
 */
const CONTEXT_CONFLICT_FALLBACK: Partial<Record<ErrorContext, string>> = {
  'product-create': 'Este código de produto já está cadastrado.',
  'invoice-create': 'Não foi possível registrar a nota devido a um conflito. Tente novamente.',
  'invoice-print':
    'Não foi possível concluir a impressão devido a um conflito. Atualize a página e tente novamente.',
};

/**
 * Status-only fallback, used whenever `errorCode` is missing or not
 * recognized. `status === 0` is treated the same as `503` (service
 * unreachable) rather than as a timeout: in this app it is what the HTTP
 * client reports for a connection failure (backend down / CORS / no
 * network), which reads better to users as "indisponível" than as "demorou
 * para responder". True request timeouts are covered by the
 * `INVENTORY_TIMEOUT` errorCode and by status `504`.
 */
function statusFallback(error: HttpErrorResponse, context?: ErrorContext): string {
  switch (error.status) {
    case 400:
      return 'Confira os dados informados e tente novamente.';
    case 404:
      return 'O registro solicitado não foi encontrado.';
    case 409:
      return (
        (context && CONTEXT_CONFLICT_FALLBACK[context]) ??
        'Não foi possível concluir a operação devido a um conflito com o estado atual. Tente novamente.'
      );
    case 0:
    case 503:
      return 'O serviço de estoque está temporariamente indisponível. Tente novamente em alguns instantes.';
    case 504:
      return 'O serviço demorou mais que o esperado para responder. Tente novamente.';
    default:
      return 'Não foi possível concluir a operação. Tente novamente.';
  }
}

/**
 * Resolves a `HttpErrorResponse` from Inventory.Api/Billing.Api into a
 * single, user-facing Portuguese sentence, safe to render directly in a
 * toast or inline alert.
 *
 * Resolution order:
 * 1. `error.error.errorCode`, when present and recognized (context may
 *    still refine the specific wording, see `INSUFFICIENT_STOCK`/context
 *    combinations above and in each screen's own error-message building).
 * 2. `error.status`, refined by `context` for 409 conflicts.
 * 3. A final generic fallback.
 *
 * `error.error.detail` is only ever surfaced for `INSUFFICIENT_STOCK` (see
 * `safeDetail`); every other path uses a canned Portuguese sentence so raw
 * backend text (which may be in English, or a framework-generated string
 * like "Http failure response for ...") never reaches the UI.
 */
export function getUserFriendlyErrorMessage(
  error: HttpErrorResponse,
  context?: ErrorContext,
): string {
  const errorCode = (error.error as { errorCode?: unknown } | null)?.errorCode;

  if (typeof errorCode === 'string' && errorCode in ERROR_CODE_MESSAGES) {
    return ERROR_CODE_MESSAGES[errorCode as KnownErrorCode](error);
  }

  return statusFallback(error, context);
}
