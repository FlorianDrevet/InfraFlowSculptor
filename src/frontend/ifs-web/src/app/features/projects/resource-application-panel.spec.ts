import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ResourceApplicationResponse,
  ResourceResponse,
  SaveApplicationRequest,
} from '../../core/api/generated';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceApplicationPanel } from './resource-application-panel';

class ResourceApplicationTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        applicationHeading: 'Application',
        applicationHelp: 'Configuration de build.',
        loadingApplication: 'Chargement…',
        applicationDelivery: 'Livraison',
        deliveryPipeline: 'Pipeline de livraison',
        applicationName: 'Nom de l’application',
        applicationSourcePath: 'Dépôt applicatif',
        dockerfile: 'Dockerfile',
        buildContext: 'Contexte de construction',
        imageRepository: 'Dépôt d’image',
        buildRegistry: 'Registre de build',
        pipelineSteps: 'Étapes du pipeline',
        stepEnabled: 'Étape activée',
        stepBlocking: 'Bloquante',
        stepPosition: 'Position',
        positionBeforeBuild: 'Avant le build',
        positionAfterBuild: 'Après le build',
        positionAfterDeployment: 'Après le déploiement',
        healthCheck: 'Contrôle de santé',
        healthCheckPath: 'Chemin de contrôle',
        healthCheckHelp: 'Chemin HTTP de santé.',
        deploymentStrategy: 'Stratégie de déploiement',
        directDeployment: 'Remplacement direct',
        save: 'Enregistrer',
        savingApplication: 'Enregistrement…',
        stepParameter: {
          tool: 'Outil',
          minimumSeverity: 'Seuil de gravité minimum',
          exceptions: 'Exceptions',
        },
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';

describe('ResourceApplicationPanel', () => {
  it('shows the linked registry and saves the health path with the configured pipeline', async () => {
    const response: ResourceApplicationResponse = {
      available: true,
      catalogVersion: '2026.10-pilot.3',
      buildProfile: 'Container',
      unavailableReason: null,
      application: {
        version: 0,
        name: 'api',
        codeSourcePath: 'src/api',
        dockerfilePath: 'src/api/Dockerfile',
        buildContextPath: 'src/api',
        imageRepository: 'shop/orders/api',
        healthCheckPath: null,
        deploymentStrategy: 'Direct',
        buildRegistry: {
          available: true,
          displayName: 'platform / main',
          registryName: 'crshopmainshared',
          promotionRequired: false,
          selectedEnvironmentId: null,
          hasValidSelection: true,
          options: [],
        },
        steps: [
          {
            name: 'DependencyCache',
            label: 'Cache des dépendances',
            description: 'Met en cache les dépendances du dépôt.',
            enabled: true,
            blocking: false,
            position: 'BeforeBuild',
            parameters: [],
          },
          {
            name: 'ImageScan',
            label: 'Scan de l’image',
            description: 'Analyse l’image avec Trivy.',
            enabled: true,
            blocking: true,
            position: 'AfterBuild',
            parameters: [
              {
                name: 'tool',
                kind: 'enum',
                value: 'Trivy',
                defaultValue: 'Trivy',
                values: ['Trivy'],
              },
              {
                name: 'minimumSeverity',
                kind: 'enum',
                value: 'CRITICAL',
                defaultValue: 'CRITICAL',
                values: ['CRITICAL', 'HIGH', 'MEDIUM', 'LOW'],
              },
              { name: 'exceptions', kind: 'string', value: '', defaultValue: '', values: [] },
            ],
          },
        ],
        findings: [],
      },
    };
    let savedRequest: SaveApplicationRequest | undefined;
    const resources = {
      application: vi.fn(() => of(response)),
      saveApplication: vi.fn(
        (
          _projectId: string,
          _componentId: string,
          _resourceId: string,
          body: SaveApplicationRequest,
        ) => {
          savedRequest = body;
          return of(response);
        },
      ),
    };
    const resource = {
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
    } satisfies ResourceResponse;

    TestBed.configureTestingModule({
      imports: [ResourceApplicationPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceApplicationTranslations,
        }),
        { provide: ResourcesService, useValue: resources },
      ],
    });

    const fixture = TestBed.createComponent(ResourceApplicationPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(resources.application).toHaveBeenCalledOnce());
    await fixture.whenStable();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain('platform / main');
    expect(host.textContent).toContain('crshopmainshared');
    expect(host.querySelector<HTMLInputElement>('input[name="applicationSourcePath"]')?.value).toBe(
      'src/api',
    );
    const minimumSeverity = host.querySelector<HTMLSelectElement>(
      'select[id="step-param-ImageScan-minimumSeverity"]',
    );
    expect(minimumSeverity?.value).toBe('CRITICAL');

    const healthPath = host.querySelector<HTMLInputElement>('input[name="healthCheckPath"]')!;
    healthPath.value = '/healthz';
    healthPath.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();

    await vi.waitFor(() => expect(resources.saveApplication).toHaveBeenCalledOnce());
    expect(savedRequest?.expectedVersion).toBe(0);
    expect(savedRequest?.imageRepository).toBe('shop/orders/api');
    expect(savedRequest?.healthCheckPath).toBe('/healthz');
    const imageScan = savedRequest?.steps.find((step) => step.name === 'ImageScan');
    expect(imageScan?.blocking).toBe(true);
    expect(imageScan?.position).toBe('AfterBuild');
    expect(imageScan?.parameters).toContainEqual({
      name: 'minimumSeverity',
      value: 'CRITICAL',
    });
    expect(resources.saveApplication.mock.calls[0]).toEqual([
      projectId,
      componentId,
      resourceId,
      savedRequest,
    ]);
  });
});
