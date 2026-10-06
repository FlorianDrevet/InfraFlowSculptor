import { HttpErrorResponse } from '@angular/common/http';

export type ApiFieldErrors = Readonly<Record<string, readonly string[]>>;

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    readonly fieldErrors: ApiFieldErrors = {},
    readonly traceId: string | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseFieldErrors(value: unknown): ApiFieldErrors {
  if (!isRecord(value)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value).flatMap(([field, messages]) => {
      if (!Array.isArray(messages)) {
        return [];
      }

      return [
        [field, messages.filter((message): message is string => typeof message === 'string')],
      ];
    }),
  );
}

export function normalizeHttpError(error: HttpErrorResponse): ApiError {
  const problem = isRecord(error.error) ? error.error : {};
  const code =
    (typeof problem['code'] === 'string' && problem['code']) ||
    (typeof problem['type'] === 'string' && problem['type'] !== 'about:blank' && problem['type']) ||
    `HTTP_${error.status || 'ERROR'}`;
  const message =
    (typeof problem['detail'] === 'string' && problem['detail']) ||
    (typeof problem['message'] === 'string' && problem['message']) ||
    (typeof problem['title'] === 'string' && problem['title']) ||
    error.message ||
    'The request could not be completed.';
  const traceId = typeof problem['traceId'] === 'string' ? problem['traceId'] : null;

  return new ApiError(
    error.status,
    code,
    message,
    parseFieldErrors(problem['errors'] ?? problem['fieldErrors']),
    traceId,
  );
}
