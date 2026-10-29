import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ComponentResponse, ProjectSummaryResponse, ResourceResponse } from '../../core/api/generated';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceEditor } from './resource-editor';

class ResourceEditorTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({ resource: { links: 'Liaisons' } });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';

describe('ResourceEditor', () => {
  it('shows the links tab for a modeled resource', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'ContainerApp',
      logicalName: 'api',
      description: '',
      tags: {},
      version: 1,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      defaultIdentityResourceId: null,
      children: [],
    };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'API',
      code: 'api',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        { provide: ProjectsService, useValue: { get: vi.fn(() => of(project)), myPermissions: vi.fn(() => of({ permissions: [] })) } },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => of({ properties: [] })), types: vi.fn(() => of([])) } },
        { provide: ResourcesService, useValue: {
          effectiveValues: vi.fn(() => of([])),
          previewName: vi.fn(() => of({ items: [], issues: [] })),
        } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.componentRef.setInput('tab', 'links');
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('nav')).not.toBeNull());
    fixture.detectChanges();

    const linksTab = host.querySelector('a[aria-current="page"]');
    expect(linksTab?.textContent?.trim()).toBe('Liaisons');
  });
});
