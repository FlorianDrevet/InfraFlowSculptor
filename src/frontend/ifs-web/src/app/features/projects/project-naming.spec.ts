import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectNaming } from './project-naming';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement du projet…',
        loadError: 'Impossible de charger le projet.',
        namingTitle: 'Nommage',
        namingSave: 'Enregistrer',
        namingDescription:
          "Un seul calcul, côté serveur, pour l'écran, la validation et le code produit.",
        namingPreviewTitle: 'Aperçu en direct · {{count}} ressources',
        namingTemplatesTitle: 'Gabarits du projet',
        namingTypeTemplate: 'Gabarit pour un type',
        namingAbbreviationLabel: 'Abréviation de {{type}}',
        namingOriginProjectDefault: 'Défaut du projet',
        namingTarget: 'Cible',
        namingResource: 'Ressource',
        namingAzureName: 'Nom Azure',
        namingLength: 'Long.',
        namingOrigin: 'Origine',
        namingDefaultTemplate: 'Gabarit par défaut',
        namingResourceGroup: 'ResourceGroup',
        namingStorageAccount: 'StorageAccount',
        namingAbbreviations: 'Abréviations',
        namingCatalogDefault: 'Par défaut : celles du catalogue (Cloud Adoption Framework)',
        namingCatalogOrigin: 'Catalogue',
        namingProjectOrigin: 'Projet',
        namingPublishedTitle: 'Les gabarits de nommage sont verrouillés après publication',
        namingPublishedDescription:
          'Aucun changement de nom publié ne peut être enregistré depuis cet écran.',
      },
    });
  }
}

describe('ProjectNaming', () => {
  it('loads the server preview and locks published settings', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const projects = {
      get: vi.fn((id: string) =>
        of({ id, version: 2, code: 'shop', catalogVersion: '2026.10-pilot' }),
      ),
      namingSettings: vi.fn((id: string) =>
        of({
          projectId: id,
          version: 2,
          preset: 'Compact',
          defaultTemplate: '{abbr}-{project}-{name}-{env}',
          resourceGroupTemplate: 'rg-{project}-{component}-{name}-{env}',
          typeOverrides: [],
          isPublished: true,
        }),
      ),
      previewNaming: vi.fn().mockReturnValue(
        of({
          items: [
            {
              componentId: '88888888-8888-4888-8888-888888888888',
              componentCode: 'core',
              resourceId: '99999999-9999-4999-8999-999999999999',
              resourceType: 'KeyVault',
              logicalName: 'main',
              targetId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
              targetCode: 'dev',
              resourceName: 'kv-shop-main-dev',
              resourceGroupName: 'rg-shop-core-main-dev',
              nameLength: 16,
              maximumLength: 24,
              isExisting: false,
              explanation: {
                level: 'ProjectDefault',
                template: '{abbr}-{project}-{name}-{env}',
                rawName: 'kv-shop-main-dev',
                finalName: 'kv-shop-main-dev',
                transformations: [],
              },
              resourceGroupExplanation: null,
            },
          ],
          issues: [],
        }),
      ),
      notifyProjectUpdated: vi.fn(),
    };
    const catalog = {
      types: vi.fn().mockReturnValue(
        of([
          {
            type: 'KeyVault',
            labelFr: 'Coffre de clés',
            labelEn: 'Key Vault',
            abbreviation: 'kv',
            category: 'security',
            icon: 'key-vault',
          },
        ]),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectNaming],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId/naming', component: ProjectNaming }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: projects },
        { provide: CatalogService, useValue: catalog },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    }).compileComponents();

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/o/org/projects/${projectId}/naming`, ProjectNaming);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(catalog.types).toHaveBeenCalledWith('2026.10-pilot');
    await vi.waitFor(() => expect(projects.previewNaming).toHaveBeenCalled());
    expect(harness.routeNativeElement?.textContent).toContain('kv-shop-main-dev');
    expect(harness.routeNativeElement?.textContent).toContain('Coffre de clés');
    expect(harness.routeNativeElement?.textContent).toContain(
        'Les gabarits de nommage sont verrouillés après publication',
    );
    expect(harness.routeNativeElement?.querySelector('input')?.disabled).toBe(true);

    const nextProjectId = '77777777-7777-4777-8777-777777777778';
    await harness.navigateByUrl(`/o/org/projects/${nextProjectId}/naming`, ProjectNaming);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projects.get).toHaveBeenCalledTimes(2);
    expect(projects.get).toHaveBeenLastCalledWith(nextProjectId);
    expect(projects.namingSettings).toHaveBeenLastCalledWith(nextProjectId);
  });
});
