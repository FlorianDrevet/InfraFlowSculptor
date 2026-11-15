import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import {
  createGitConnection,
  deleteGitConnection,
  listGitConnections,
  listGitProviders,
  setGitConnectionRepositoryAccess,
  testGitConnection,
  updateGitConnection,
} from '../api/generated';
import type {
  CreateGitConnectionRequest,
  GitConnectionResponse,
  GitProviderAvailabilityResponse,
  SetGitConnectionRepositoryAccessRequest,
  UpdateGitConnectionRequest,
} from '../api/generated';

@Injectable({ providedIn: 'root' })
export class GitConnectionsService {
  private readonly api = inject(Api);

  providers(organizationId: string) {
    return this.api.invoke(listGitProviders, { organizationId });
  }

  list(organizationId: string) {
    return this.api.invoke(listGitConnections, { organizationId });
  }

  create(organizationId: string, body: CreateGitConnectionRequest) {
    return this.api.invoke(createGitConnection, { organizationId, body });
  }

  test(organizationId: string, connectionId: string) {
    return this.api.invoke(testGitConnection, { organizationId, connectionId });
  }

  update(organizationId: string, connectionId: string, body: UpdateGitConnectionRequest) {
    return this.api.invoke(updateGitConnection, { organizationId, connectionId, body });
  }

  setRepositoryAccess(
    organizationId: string,
    connectionId: string,
    body: SetGitConnectionRepositoryAccessRequest,
  ) {
    return this.api.invoke(setGitConnectionRepositoryAccess, {
      organizationId,
      connectionId,
      body,
    });
  }

  delete(organizationId: string, connectionId: string) {
    return this.api.invoke(deleteGitConnection, { organizationId, connectionId });
  }
}

export type GitConnection = GitConnectionResponse;
export type GitProvider = GitProviderAvailabilityResponse;
