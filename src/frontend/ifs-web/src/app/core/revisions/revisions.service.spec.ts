import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { listRevisions } from '../api/generated';
import { Api } from '../api/generated/api';
import { RevisionsService } from './revisions.service';

describe('RevisionsService', () => {
  it('calls the generated list operation with the project ID', () => {
    const api = { invoke: vi.fn().mockReturnValue(of({ items: [], nextNumber: 1 })) };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), { provide: Api, useValue: api }],
    });

    TestBed.inject(RevisionsService).list('project-id').subscribe();

    expect(api.invoke).toHaveBeenCalledWith(listRevisions, { projectId: 'project-id' });
  });

  it('reads revision files as text and encodes each path segment', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: Api, useValue: {} }],
    });
    const service = TestBed.inject(RevisionsService);
    const http = TestBed.inject(HttpTestingController);

    service.readFile('project-id', 'revision-id', 'orders/main file.bicep').subscribe();

    const request = http.expectOne(
      '/v1/projects/project-id/revisions/revision-id/files/orders/main%20file.bicep',
    );
    expect(request.request.responseType).toBe('text');
    request.flush('param location string');
    http.verify();
  });

  it('downloads revision archives as blobs', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: Api, useValue: {} }],
    });
    const service = TestBed.inject(RevisionsService);
    const http = TestBed.inject(HttpTestingController);

    service.downloadArchive('project-id', 'revision-id').subscribe();

    const request = http.expectOne('/v1/projects/project-id/revisions/revision-id/archive');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['archive']));
    http.verify();
  });
});
