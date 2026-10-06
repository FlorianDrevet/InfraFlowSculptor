import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { isApiRequest } from './is-api-request';

const COMMAND_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const requestKeys = new WeakMap<HttpRequest<unknown>, string>();

function createOperationId(): string {
  return (
    globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
  );
}

function keyFor(request: HttpRequest<unknown>): string {
  const existing = request.headers.get('Idempotency-Key');
  if (existing) {
    return existing;
  }

  const previous = requestKeys.get(request);
  if (previous) {
    return previous;
  }

  const key = createOperationId();
  requestKeys.set(request, key);
  return key;
}

export const idempotencyInterceptor: HttpInterceptorFn = (request, next) => {
  if (
    !isApiRequest(request) ||
    !COMMAND_METHODS.has(request.method) ||
    request.headers.has('Idempotency-Key')
  ) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { 'Idempotency-Key': keyFor(request) } }));
};
