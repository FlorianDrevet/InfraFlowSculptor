import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import {
  createProjectComponent,
  createResourceGroup,
  deleteProjectComponent,
  deleteResourceGroup,
  getProjectComponent,
  listProjectComponents,
  updateProjectComponent,
  updateResourceGroup,
} from '../api/generated';
import type {
  ExpectedVersionRequest,
  SaveComponentRequest,
  SaveResourceGroupRequest,
} from '../api/generated';

@Injectable({ providedIn: 'root' })
export class ComponentsService {
  private readonly api = inject(Api);

  list(projectId: string) {
    return this.api.invoke(listProjectComponents, { projectId });
  }

  get(projectId: string, componentId: string) {
    return this.api.invoke(getProjectComponent, { projectId, componentId });
  }

  create(projectId: string, body: SaveComponentRequest) {
    return this.api.invoke(createProjectComponent, { projectId, body });
  }

  update(projectId: string, componentId: string, body: SaveComponentRequest) {
    return this.api.invoke(updateProjectComponent, { projectId, componentId, body });
  }

  delete(projectId: string, componentId: string, body: ExpectedVersionRequest) {
    return this.api.invoke(deleteProjectComponent, { projectId, componentId, body });
  }

  createGroup(projectId: string, componentId: string, body: SaveResourceGroupRequest) {
    return this.api.invoke(createResourceGroup, { projectId, componentId, body });
  }

  updateGroup(
    projectId: string,
    componentId: string,
    resourceGroupId: string,
    body: SaveResourceGroupRequest,
  ) {
    return this.api.invoke(updateResourceGroup, { projectId, componentId, resourceGroupId, body });
  }

  deleteGroup(
    projectId: string,
    componentId: string,
    resourceGroupId: string,
    body: ExpectedVersionRequest,
  ) {
    return this.api.invoke(deleteResourceGroup, { projectId, componentId, resourceGroupId, body });
  }
}
