import { provideRouter } from '@angular/router';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { TranslocoLoader, Translation, provideTransloco } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { RevisionDetailResponse } from '../../core/api/generated';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { CodeHighlightService } from '../../shared/code-viewer/code-highlight.service';
import { RevisionDetail } from './revision-detail';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      revisions: {
        revisionNumber: 'Révision {{ number }}',
        stateGenerated: 'Générée',
        stale: 'Périmée',
        queuePosition: 'Position {{ position }} dans la file',
        queued: 'Révision {{ number }} en file, position {{ position }}.',
        incident: 'Incident IFS',
      },
      revisionDetail: {
        back: 'Toutes les révisions',
        loading: 'Chargement de la révision…',
        loadError: 'La révision n’a pas pu être chargée.',
        loadErrorDescription: 'Vérifiez le lien.',
        notAvailable: 'Non disponible',
        modelVersion: 'modèle v{{ version }}',
        author: 'Auteur',
        catalogVersion: 'catalogue {{ version }}',
        downloadArchive: 'Télécharger l’archive',
        staleTitle: 'Révision périmée',
        staleDescription: 'Le modèle a changé depuis la création de la révision.',
        failureDescription: 'Un contrôle de sortie a échoué.',
        tabsLabel: 'Sections de la révision',
        changesTab: 'Changements',
        planTab: 'Plan de déploiement',
        filesTab: 'Fichiers',
        findingsTab: 'Constats',
        changesTitle: 'Résumé des changements',
        changesDescription: 'Calculés sur le plan de déploiement.',
        noChanges: 'Aucun changement.',
        planTitle: 'Plan de déploiement figé',
        planLoading: 'Chargement du plan…',
        planError: 'Le plan n’a pas pu être chargé.',
        outputsPending: 'Le plan et les fichiers apparaîtront après la génération.',
        filesTitle: 'Fichiers',
        searchFiles: 'Rechercher dans les fichiers',
        searchingFiles: 'Recherche dans les fichiers…',
        searchError: 'La recherche a échoué.',
        noFilesMatch: 'Aucun fichier ne correspond à la recherche.',
        noFilesAvailable: 'Aucun fichier n’est disponible pour cette révision.',
        fileTreeLabel: 'Arborescence des fichiers générés',
        fileViewerLabel: 'Contenu du fichier',
        downloadFile: 'Télécharger',
        fileLoading: 'Chargement du fichier…',
        fileError: 'Le fichier n’a pas pu être chargé.',
        selectFile: 'Sélectionnez un fichier.',
        findingsTitle: 'Constats de cette révision',
        findingsDescription: 'Constats figés lors de la demande de génération.',
        noFindings: 'Aucun constat associé à cette révision.',
      },
    });
  }
}

const detail: RevisionDetailResponse = {
  revision: {
    authorObjectId: 'object-id',
    authorTenantId: 'tenant-id',
    catalogVersion: '2026.09',
    changesCount: 1,
    ciPlatform: 'AzureDevOps',
    completedAt: '2026-11-13T10:00:00Z',
    createdAt: '2026-11-13T09:59:00Z',
    failureCode: null,
    filesCount: 1,
    findingsCount: 0,
    id: 'revision-id',
    infrastructureLanguage: 'Bicep',
    inputSha256: 'sha256',
    isStale: false,
    modelVersion: 3,
    number: 1,
    queuePosition: null,
    startedAt: '2026-11-13T09:59:10Z',
    state: 'Generated',
  },
  changes: [
    {
      category: 'Added',
      descriptionEn: 'File added',
      descriptionFr: 'Fichier ajouté',
      subject: 'infra/main.bicep',
    },
  ],
  files: [
    { relativePath: 'infra/main.bicep', sha256: 'file-sha', size: 17 },
    { relativePath: 'README.md', sha256: 'readme-sha', size: 8 },
  ],
  findings: [],
};

describe('RevisionDetail', () => {
  it('shows only the J0-24 tabs and opens a selected generated file', async () => {
    const revisions = {
      get: vi.fn().mockReturnValue(of(detail)),
      plan: vi.fn().mockReturnValue(of({ project: {}, components: [], pins: {} })),
      readFile: vi.fn((_projectId: string, _revisionId: string, path: string) =>
        of(path === 'README.md' ? 'overview and setup' : 'param location string'),
      ),
      downloadFile: vi.fn().mockReturnValue(of(new Blob(['file']))),
      downloadArchive: vi.fn().mockReturnValue(of(new Blob(['zip']))),
    };
    const highlighter = {
      highlight: vi.fn().mockResolvedValue('<pre><code>param location string</code></pre>'),
    };
    await render(RevisionDetail, {
      inputs: { projectId: 'project-id', revisionId: 'revision-id' },
      providers: [
        provideRouter([]),
        { provide: RevisionsService, useValue: revisions },
        { provide: CodeHighlightService, useValue: highlighter },
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
    });

    expect(await screen.findByText('Fichier ajouté')).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Plan de déploiement' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Fichiers' })).toBeTruthy();
    expect(screen.queryByRole('tab', { name: 'Publications' })).toBeNull();

    fireEvent.click(screen.getByRole('tab', { name: 'Fichiers' }));

    expect(await screen.findByText('param location string')).toBeTruthy();
    expect(revisions.readFile).toHaveBeenCalledWith(
      'project-id',
      'revision-id',
      'infra/main.bicep',
    );
    expect(highlighter.highlight).toHaveBeenCalledWith('param location string', 'bicep');

    fireEvent.input(screen.getByRole('textbox', { name: 'Rechercher dans les fichiers' }), {
      target: { value: 'overview' },
    });
    expect(await screen.findByRole('button', { name: 'README.md' })).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'main.bicep' })).toBeNull());
    expect(revisions.readFile).toHaveBeenCalledWith('project-id', 'revision-id', 'README.md');
  });
});
