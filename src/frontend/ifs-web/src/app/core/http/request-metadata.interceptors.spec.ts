import { signal } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { retry } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { APP_CONFIG, DEFAULT_APP_CONFIG } from '../config/app-config.token';
import { LanguageService } from '../i18n/language.service';
import { ActiveOrganizationStore } from './active-organization.store';
import { acceptLanguageInterceptor } from './accept-language.interceptor';
import { idempotencyInterceptor } from './idempotency.interceptor';
import { organizationInterceptor } from './organization.interceptor';

describe('request metadata interceptors', () => {
  let client: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            organizationInterceptor,
            idempotencyInterceptor,
            acceptLanguageInterceptor,
          ]),
        ),
        provideHttpClientTesting(),
        { provide: APP_CONFIG, useValue: { ...DEFAULT_APP_CONFIG, apiUrl: 'https://api.example' } },
        { provide: LanguageService, useValue: { activeLanguage: signal('en') } },
      ],
    });
    client = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds the active language and only sends an organization when one is selected', () => {
    client.get('/v1/me').subscribe();
    const unscoped = controller.expectOne('/v1/me');
    expect(unscoped.request.headers.get('Accept-Language')).toBe('en');
    expect(unscoped.request.headers.has('X-Ifs-Organization')).toBe(false);
    unscoped.flush({});

    TestBed.inject(ActiveOrganizationStore).setOrganizationId('org-42');
    client.get('/v1/me').subscribe();
    const scoped = controller.expectOne('/v1/me');
    expect(scoped.request.headers.get('X-Ifs-Organization')).toBe('org-42');
    expect(scoped.request.headers.get('Accept-Language')).toBe('en');
    scoped.flush({});
  });

  it('creates one idempotency key for a command and keeps it across a retry', () => {
    client.post('/v1/commands', { value: 'demo' }).pipe(retry(1)).subscribe();
    const firstAttempt = controller.expectOne('/v1/commands');
    const firstKey = firstAttempt.request.headers.get('Idempotency-Key');
    expect(firstKey).toBeTruthy();
    firstAttempt.flush({}, { status: 503, statusText: 'Unavailable' });

    const retryAttempt = controller.expectOne('/v1/commands');
    expect(retryAttempt.request.headers.get('Idempotency-Key')).toBe(firstKey);
    retryAttempt.flush({ accepted: true });
  });

  it('does not add idempotency keys to read requests', () => {
    client.get('/v1/me').subscribe();
    const request = controller.expectOne('/v1/me');
    expect(request.request.headers.has('Idempotency-Key')).toBe(false);
    request.flush({});
  });

  it('does not add IFS metadata to external OIDC token requests', () => {
    TestBed.inject(ActiveOrganizationStore).setOrganizationId('org-42');
    const tokenUrl = 'https://localhost:8080/realms/ifs/protocol/openid-connect/token';

    client.post(tokenUrl, { grant_type: 'authorization_code' }).subscribe();

    const request = controller.expectOne(tokenUrl);
    expect(request.request.headers.has('Idempotency-Key')).toBe(false);
    expect(request.request.headers.has('X-Ifs-Organization')).toBe(false);
    expect(request.request.headers.has('Accept-Language')).toBe(false);
    request.flush({ access_token: 'test' });
  });
});
