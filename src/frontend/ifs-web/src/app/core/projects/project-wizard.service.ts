import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import {
  Api,
  createProjectFromWizard,
  discardWizardDraft,
  getCatalogRegions,
  getCatalogVersions,
  getMe,
  getWizardDraft,
  previewNaming,
  saveWizardDraft,
} from '../api/generated';
import type {
  CreateProjectFromWizardRequest,
  CreateProjectFromWizardResponse,
  JsonElement,
  NamingPreviewRequest,
  NamingPreviewResponse,
  ProjectWizardDraftResponse,
} from '../api/generated';
import type { ProjectWizardMetadata } from './project-wizard-metadata';

@Injectable({ providedIn: 'root' })
export class ProjectWizardService {
  private readonly api = inject(Api);

  loadDraft(): Observable<ProjectWizardDraftResponse | null> {
    return this.api
      .invoke$Response(getWizardDraft, {})
      .pipe(map((response) => response.body ?? null));
  }

  loadMetadata(): Observable<ProjectWizardMetadata> {
    return this.api.invoke(getMe, {}).pipe(
      switchMap((me) =>
        this.api.invoke(getCatalogVersions, {}).pipe(
          switchMap((versions) => {
            const latest = versions
              .filter((version) => version.status === 'published')
              .sort(
                (first, second) => Date.parse(second.publishedAt) - Date.parse(first.publishedAt),
              )[0];
            if (!latest) {
              throw new Error('No published catalog version is available.');
            }

            return this.api.invoke(getCatalogRegions, { version: latest.version }).pipe(
              map((regions) => ({
                tenantId: me.tenantId,
                catalogVersion: latest.version,
                regions,
              })),
            );
          }),
        ),
      ),
    );
  }

  saveDraft(content: JsonElement): Observable<ProjectWizardDraftResponse> {
    return this.api.invoke(saveWizardDraft, { body: { content } });
  }

  discardDraft(): Observable<void> {
    return this.api.invoke(discardWizardDraft, {});
  }

  previewNaming(request: NamingPreviewRequest): Observable<NamingPreviewResponse> {
    return this.api.invoke(previewNaming, { body: request });
  }

  createProject(
    request: CreateProjectFromWizardRequest,
  ): Observable<CreateProjectFromWizardResponse> {
    return this.api.invoke(createProjectFromWizard, { body: request });
  }
}
