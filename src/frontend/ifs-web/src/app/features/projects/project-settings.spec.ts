import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectSettings } from './project-settings';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement…',
        loadError: 'Impossible de charger le projet.',
        settingsTitle: 'Paramètres du projet',
        settingsDescription: 'Identité, tags et coordinateur du projet.',
        projectName: 'Nom du projet',
        projectCode: 'Code du projet',
        tenantId: 'Tenant Entra',
        projectCodeLocked: 'Le code est verrouillé après la première publication.',
        permissionRequired: 'Vous n’avez pas le droit de modifier ce projet.',
        projectDescription: 'Description',
        projectTags: 'Tags du projet',
        tagsHint: 'Une paire clé=valeur par ligne.',
        systemTagsEnabled: 'Activer les tags système',
        coordinator: 'Coordinateur',
        noCoordinator: 'Aucun coordinateur',
        settingsSave: 'Enregistrer les paramètres',
        settingsSaving: 'Enregistrement…',
        settingsSaveError: 'Les paramètres du projet n’ont pas pu être enregistrés.',
        settingsImpactTitle: 'Confirmer les changements',
        settingsImpactDescription: 'Le serveur a calculé les données et ressources concernées.',
        impactResources: 'Ressources concernées',
        noAffectedResources: 'Aucune ressource concernée.',
        affectedData: 'Données concernées',
        cancel: 'Annuler',
        confirmImpact: 'Confirmer',
        deleteProject: 'Supprimer le projet',
        deleteProjectTitle: 'Supprimer le projet',
        deleteProjectImpact: 'Le projet sera masqué pendant 30 jours.',
        environmentCount: 'Environnements',
        nameConfirmation: 'Saisissez le nom exact du projet',
        confirmDeleteProject: 'Supprimer le projet',
        projectDeleteError: 'Le projet n’a pas pu être supprimé.',
        exportTitle: 'Export',
        exportDescription:
          "Le modèle complet au format ifs-project/v1, sans aucun secret. L'export est journalisé.",
        exportProject: 'Exporter le projet',
        exporting: 'Préparation de l’export…',
        exportError: "L'export du projet n'a pas pu être préparé.",
      },
    });
  }
}

describe('ProjectSettings', () => {
  it('renders the export panel beside project settings and downloads its temporary link', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const downloadUrl = 'https://api.example.test/v1/project-exports/0123456789abcdef';
    const projects = {
      get: vi.fn().mockReturnValue(
        of({
          id: projectId,
          version: 1,
          name: 'Boutique en ligne',
          code: 'shop',
          description: '',
          systemTagsEnabled: true,
          coordinatorUserProfileId: null,
          tags: {},
        }),
      ),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['project.administer'] })),
      exportProject: vi.fn().mockReturnValue(of({ downloadUrl, expiresAt: '2026-11-14T10:15:00Z' })),
    };
    const organizations = { listMembers: vi.fn().mockReturnValue(of([])) };
    let clickedUrl = '';
    const anchorClick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clickedUrl = this.href;
    });

    await TestBed.configureTestingModule({
      imports: [ProjectSettings],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId/settings', component: ProjectSettings }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: projects },
        { provide: OrganizationsService, useValue: organizations },
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
    await harness.navigateByUrl(`/o/contoso/projects/${projectId}/settings`, ProjectSettings);
    await vi.waitFor(() => {
      harness.fixture.detectChanges();
      expect(harness.routeNativeElement?.textContent).toContain('Exporter le projet');
    });

    const layout = harness.routeNativeElement?.querySelector('.project-settings__layout');
    const sideColumn = layout?.querySelector('.project-settings__side-column');
    const exportPanel = sideColumn?.querySelector('app-ds-panel');
    expect(layout).not.toBeNull();
    expect(exportPanel).not.toBeNull();
    expect(sideColumn?.contains(exportPanel ?? null)).toBe(true);
    expect(exportPanel?.textContent).toContain('Exporter le projet');

    const button = [...(harness.routeNativeElement?.querySelectorAll('button') ?? [])]
      .find(candidate => candidate.textContent?.includes('Exporter le projet'));
    expect(button).toBeDefined();
    button?.click();

    await vi.waitFor(() => expect(projects.exportProject).toHaveBeenCalledWith(projectId));
    expect(anchorClick).toHaveBeenCalledOnce();
    expect(clickedUrl).toBe(downloadUrl);
  });
});
