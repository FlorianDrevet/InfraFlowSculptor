import { Injectable, inject } from '@angular/core';
import { Subject, tap } from 'rxjs';
import { Api } from '../api/generated/api';
import {
  acknowledgeFinding,
  addProjectEnvironment,
  applyFindingFix,
  assignProjectRole,
  deleteProject as deleteProjectRequest,
  deleteProjectEnvironment,
  getMyProjectPermissions,
  getProject,
  getProjectMemberPermissions,
  getProjectNaming,
  getProjectValidation,
  listProjectEnvironments,
  listProjects,
  moveProjectEnvironment,
  removeProjectRole,
  previewProjectNaming,
  restoreProject,
  removeProjectFavorite,
  setProjectFavorite,
  updateProjectEnvironment,
  updateProjectSettings,
  setProjectNaming,
} from '../api/generated';
import type {
  AcknowledgeFindingRequest,
  ApplyFindingFixRequest,
  AddProjectEnvironmentRequest,
  AssignProjectRoleRequest,
  DeleteProjectEnvironmentRequest,
  DeleteProjectRequest,
  MoveProjectEnvironmentRequest,
  ProjectEnvironmentResponse,
  ProjectNamingPreviewRequest,
  ProjectNamingSettingsRequest,
  ProjectRole,
  ProjectRoleScope,
  ProjectSummaryResponse,
  RestoreProjectRequest,
  UpdateProjectEnvironmentRequest,
  UpdateProjectSettingsRequest,
} from '../api/generated';

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly api = inject(Api);
  private readonly projectUpdates = new Subject<ProjectSummaryResponse>();
  readonly projectUpdates$ = this.projectUpdates.asObservable();

  list(includeDeleted = false) {
    return this.api.invoke(listProjects, { includeDeleted });
  }

  get(projectId: string) {
    return this.api.invoke(getProject, { id: projectId });
  }

  environments(projectId: string) {
    return this.api.invoke(listProjectEnvironments, { id: projectId });
  }

  validation(projectId: string) {
    return this.api.invoke(getProjectValidation, { id: projectId });
  }

  acknowledgeFinding(projectId: string, code: string, body: AcknowledgeFindingRequest) {
    return this.api.invoke(acknowledgeFinding, { id: projectId, code, body });
  }

  applyFindingFix(projectId: string, code: string, body: ApplyFindingFixRequest) {
    return this.api.invoke(applyFindingFix, { id: projectId, code, body });
  }

  namingSettings(projectId: string) {
    return this.api.invoke(getProjectNaming, { projectId });
  }

  saveNamingSettings(projectId: string, body: ProjectNamingSettingsRequest) {
    return this.api.invoke(setProjectNaming, { projectId, body });
  }

  previewNaming(projectId: string, body: ProjectNamingPreviewRequest) {
    return this.api.invoke(previewProjectNaming, { projectId, body });
  }

  updateSettings(projectId: string, body: UpdateProjectSettingsRequest) {
    return this.api
      .invoke(updateProjectSettings, { id: projectId, body })
      .pipe(tap((project) => this.notifyProjectUpdated(project)));
  }

  notifyProjectUpdated(project: ProjectSummaryResponse): void {
    this.projectUpdates.next(project);
  }

  delete(projectId: string, body: DeleteProjectRequest) {
    return this.api.invoke(deleteProjectRequest, { id: projectId, body });
  }

  restore(projectId: string, body: RestoreProjectRequest) {
    return this.api.invoke(restoreProject, { id: projectId, body });
  }

  setFavorite(projectId: string) {
    return this.api.invoke(setProjectFavorite, { id: projectId });
  }

  removeFavorite(projectId: string) {
    return this.api.invoke(removeProjectFavorite, { id: projectId });
  }

  addEnvironment(projectId: string, body: AddProjectEnvironmentRequest) {
    return this.api.invoke(addProjectEnvironment, { id: projectId, body });
  }

  updateEnvironment(
    projectId: string,
    environmentId: string,
    body: UpdateProjectEnvironmentRequest,
  ) {
    return this.api.invoke(updateProjectEnvironment, { id: projectId, environmentId, body });
  }

  deleteEnvironment(
    projectId: string,
    environmentId: string,
    body: DeleteProjectEnvironmentRequest,
  ) {
    return this.api.invoke(deleteProjectEnvironment, { id: projectId, environmentId, body });
  }

  moveEnvironment(projectId: string, environmentId: string, body: MoveProjectEnvironmentRequest) {
    return this.api.invoke(moveProjectEnvironment, { id: projectId, environmentId, body });
  }

  myPermissions(projectId: string) {
    return this.api.invoke(getMyProjectPermissions, { id: projectId });
  }

  memberPermissions(projectId: string, memberId: string) {
    return this.api.invoke(getProjectMemberPermissions, { id: projectId, memberId });
  }

  assignRole(projectId: string, memberId: string, body: AssignProjectRoleRequest) {
    return this.api.invoke(assignProjectRole, { id: projectId, memberId, body });
  }

  removeRole(
    projectId: string,
    memberId: string,
    role: ProjectRole,
    scope: ProjectRoleScope,
    expectedVersion: number,
  ) {
    return this.api.invoke(removeProjectRole, {
      id: projectId,
      memberId,
      role,
      scope,
      expectedVersion,
    });
  }
}

export type ProjectSummary = ProjectSummaryResponse;
export type ProjectEnvironment = ProjectEnvironmentResponse;
