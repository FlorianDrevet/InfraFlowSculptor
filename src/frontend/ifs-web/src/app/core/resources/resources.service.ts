import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import {
  addResource,
  clearOverride,
  deleteResource,
  getEffectiveValues,
  getResourceDeletionImpact,
  moveResource,
  previewResourceName,
  setExistingResourceId,
  setForcedName,
  setOverride,
  setPresence,
  setResourceProperties,
  updateResourceMetadata,
} from '../api/generated';
import type {
  ClearResourceOverrideRequest,
  ExpectedVersionRequest,
  MoveResourceRequest,
  PreviewResourceNameRequest,
  SaveResourcePropertiesRequest,
  SaveResourceRequest,
  SetExistingResourceIdRequest,
  SetResourceForcedNameRequest,
  SetResourceOverrideRequest,
  SetResourcePresenceRequest,
  UpdateResourceMetadataRequest,
} from '../api/generated';

@Injectable({ providedIn: 'root' })
export class ResourcesService {
  private readonly api = inject(Api);

  add(projectId: string, componentId: string, body: SaveResourceRequest) {
    return this.api.invoke(addResource, { projectId, componentId, body });
  }

  previewName(projectId: string, componentId: string, body: PreviewResourceNameRequest) {
    return this.api.invoke(previewResourceName, { projectId, componentId, body });
  }

  updateMetadata(projectId: string, componentId: string, resourceId: string, body: UpdateResourceMetadataRequest) {
    return this.api.invoke(updateResourceMetadata, { projectId, componentId, resourceId, body });
  }

  effectiveValues(projectId: string, resourceId: string) {
    return this.api.invoke(getEffectiveValues, { projectId, resourceId });
  }

  deletionImpact(projectId: string, componentId: string, resourceId: string) {
    return this.api.invoke(getResourceDeletionImpact, { projectId, componentId, resourceId });
  }

  setProperties(projectId: string, componentId: string, resourceId: string, body: SaveResourcePropertiesRequest) {
    return this.api.invoke(setResourceProperties, { projectId, componentId, resourceId, body });
  }

  setOverride(projectId: string, componentId: string, resourceId: string, body: SetResourceOverrideRequest) {
    return this.api.invoke(setOverride, { projectId, componentId, resourceId, body });
  }

  clearOverride(projectId: string, componentId: string, resourceId: string, body: ClearResourceOverrideRequest) {
    return this.api.invoke(clearOverride, { projectId, componentId, resourceId, body });
  }

  setPresence(projectId: string, componentId: string, resourceId: string, body: SetResourcePresenceRequest) {
    return this.api.invoke(setPresence, { projectId, componentId, resourceId, body });
  }

  setExistingResourceId(projectId: string, componentId: string, resourceId: string, body: SetExistingResourceIdRequest) {
    return this.api.invoke(setExistingResourceId, { projectId, componentId, resourceId, body });
  }

  setForcedName(projectId: string, componentId: string, resourceId: string, body: SetResourceForcedNameRequest) {
    return this.api.invoke(setForcedName, { projectId, componentId, resourceId, body });
  }

  move(projectId: string, componentId: string, resourceId: string, body: MoveResourceRequest) {
    return this.api.invoke(moveResource, { projectId, componentId, resourceId, body });
  }

  delete(projectId: string, componentId: string, resourceId: string, body: ExpectedVersionRequest) {
    return this.api.invoke(deleteResource, { projectId, componentId, resourceId, body });
  }
}
