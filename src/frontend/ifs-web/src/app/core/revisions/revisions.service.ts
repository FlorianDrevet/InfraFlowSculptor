import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import {
  generateRevision,
  getRevision,
  getRevisionPlan,
  listRevisionFiles,
  listRevisions,
} from '../api/generated';

const REVISION_FILE_ROUTE = '/v1/projects/{projectId}/revisions/{revisionId}/files/{path}';
const REVISION_ARCHIVE_ROUTE = '/v1/projects/{projectId}/revisions/{revisionId}/archive';
const DOWNLOAD_QUERY = '?download=true';

@Injectable({ providedIn: 'root' })
export class RevisionsService {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);

  list(projectId: string) {
    return this.api.invoke(listRevisions, { projectId });
  }

  generate(projectId: string) {
    return this.api.invoke(generateRevision, { projectId });
  }

  get(projectId: string, revisionId: string) {
    return this.api.invoke(getRevision, { projectId, revisionId });
  }

  plan(projectId: string, revisionId: string) {
    return this.api.invoke(getRevisionPlan, { projectId, revisionId });
  }

  files(projectId: string, revisionId: string) {
    return this.api.invoke(listRevisionFiles, { projectId, revisionId });
  }

  readFile(projectId: string, revisionId: string, path: string) {
    return this.http.get(this.fileUrl(projectId, revisionId, path), { responseType: 'text' });
  }

  downloadFile(projectId: string, revisionId: string, path: string) {
    return this.http.get(this.fileUrl(projectId, revisionId, path, true), {
      responseType: 'blob',
    });
  }

  downloadArchive(projectId: string, revisionId: string) {
    const route = REVISION_ARCHIVE_ROUTE.replace(
      '{projectId}',
      encodeURIComponent(projectId),
    ).replace('{revisionId}', encodeURIComponent(revisionId));
    return this.http.get(route, { responseType: 'blob' });
  }

  private fileUrl(projectId: string, revisionId: string, path: string, download = false): string {
    const encodedPath = path
      .split('/')
      .map((segment) => encodeURIComponent(segment))
      .join('/');
    const route = REVISION_FILE_ROUTE.replace('{projectId}', encodeURIComponent(projectId))
      .replace('{revisionId}', encodeURIComponent(revisionId))
      .replace('{path}', encodedPath);
    return `${route}${download ? DOWNLOAD_QUERY : ''}`;
  }
}
