import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { Api } from '../api/generated/api';
import {
  confirmPublicationOwnership,
  getPublication,
  listProjectPublications,
  startPublicationWrite,
  startRevisionPublication,
} from '../api/generated';
import { PublicationsService } from './publications.service';

describe('PublicationsService', () => {
  it('uses the generated operations for the publication lifecycle', () => {
    const api = { invoke: vi.fn().mockReturnValue(of({ id: 'publication-id', targets: [] })) };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: api }] });
    const service = TestBed.inject(PublicationsService);

    service.start('project-id', 'revision-id', 'Release test', false).subscribe();
    service.list('project-id').subscribe();
    service.get('project-id', 'publication-id').subscribe();
    service.confirmOwnership('project-id', 'publication-id', 'target-id').subscribe();
    service.publish('project-id', 'publication-id').subscribe();

    expect(api.invoke).toHaveBeenNthCalledWith(1, startRevisionPublication, {
      projectId: 'project-id',
      revisionId: 'revision-id',
      body: { message: 'Release test', acceptStaleRevision: false },
    });
    expect(api.invoke).toHaveBeenNthCalledWith(2, listProjectPublications, {
      projectId: 'project-id',
      limit: 30,
    });
    expect(api.invoke).toHaveBeenNthCalledWith(3, getPublication, {
      projectId: 'project-id',
      publicationId: 'publication-id',
    });
    expect(api.invoke).toHaveBeenNthCalledWith(4, confirmPublicationOwnership, {
      projectId: 'project-id',
      publicationId: 'publication-id',
      targetId: 'target-id',
    });
    expect(api.invoke).toHaveBeenNthCalledWith(5, startPublicationWrite, {
      projectId: 'project-id',
      publicationId: 'publication-id',
    });
  });
});
