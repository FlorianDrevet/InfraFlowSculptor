import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { normalizeHttpError } from './api-error';

describe('normalizeHttpError', () => {
  it('maps problem+json details to the ApiError contract', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: {
        type: 'https://ifs.test/problems/validation',
        detail: 'The request contains invalid fields.',
        errors: { name: ['Name is required.'] },
        traceId: '00-abc',
      },
    });

    const normalized = normalizeHttpError(error);

    expect(normalized).toMatchObject({
      status: 422,
      code: 'https://ifs.test/problems/validation',
      message: 'The request contains invalid fields.',
      fieldErrors: { name: ['Name is required.'] },
      traceId: '00-abc',
    });
  });

  it('parses problem details returned as text by endpoints without a response body', () => {
    const error = new HttpErrorResponse({
      status: 409,
      statusText: 'Conflict',
      error: JSON.stringify({
        title: 'The request conflicts with the current state.',
        code: 'RG-ORG-05',
        traceId: '00-org-conflict',
      }),
    });

    expect(normalizeHttpError(error)).toMatchObject({
      status: 409,
      code: 'RG-ORG-05',
      message: 'The request conflicts with the current state.',
      traceId: '00-org-conflict',
    });
  });

  it('provides a stable fallback code when the response has no problem document', () => {
    const normalized = normalizeHttpError(
      new HttpErrorResponse({ status: 503, statusText: 'Unavailable' }),
    );

    expect(normalized.code).toBe('HTTP_503');
    expect(normalized.status).toBe(503);
    expect(normalized.fieldErrors).toEqual({});
  });
});
