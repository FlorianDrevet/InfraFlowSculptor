import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import {
  confirmPublicationOwnership,
  getPublication,
  listProjectPublications,
  startPublicationWrite,
  startRevisionPublication,
} from '../api/generated';

@Injectable({ providedIn: 'root' })
export class PublicationsService {
  private readonly api = inject(Api);

  start(
    projectId: string,
    revisionId: string,
    message: string,
    acceptStaleRevision: boolean,
    destinationIds?: string[],
  ) {
    return this.api.invoke(startRevisionPublication, {
      projectId,
      revisionId,
      body: {
        message: message.trim() || null,
        acceptStaleRevision,
        ...(destinationIds ? { destinationIds } : {}),
      },
    });
  }

  list(projectId: string) {
    return this.api.invoke(listProjectPublications, { projectId, limit: 30 });
  }

  get(projectId: string, publicationId: string) {
    return this.api.invoke(getPublication, { projectId, publicationId });
  }

  confirmOwnership(projectId: string, publicationId: string, targetId: string) {
    return this.api.invoke(confirmPublicationOwnership, { projectId, publicationId, targetId });
  }

  publish(projectId: string, publicationId: string) {
    return this.api.invoke(startPublicationWrite, { projectId, publicationId });
  }
}
