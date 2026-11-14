import { provideRouter } from '@angular/router';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { TranslocoLoader, Translation, provideTransloco } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { PublicationResponse, RevisionDetailResponse } from '../../core/api/generated';
import { PublicationsService } from '../../core/publications/publications.service';
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
        publicationsTab: 'Publications',
        publicationsTitle: 'Publications',
        publicationsLoading: 'Chargement des publications…',
        publicationsLoadError: 'Les publications n’ont pas pu être chargées.',
        publicationsEmpty: 'Aucune publication pour cette révision.',
        publish: 'Publier',
        publishMessage: 'Message de publication',
        preparePublication: 'Préparer la publication',
        publicationPreparing: 'Préparation en cours…',
        publicationStartError: 'La publication n’a pas pu être préparée.',
        publicationsDescription: 'Préparez une pull request par dépôt.',
        publishTitle: 'Publier la révision {{ number }}',
        publishDescription: 'Rien n’est écrit avant votre confirmation.',
        cancelPublication: 'Annuler',
        publicationTargetPreparing: 'Préparation',
        publicationTargetNeedsConfirmation: 'Confirmation requise',
        publicationTargetPrepared: 'Prêt',
        publicationTargetPublishing: 'Publication en cours',
        publicationTargetCompleted: 'Pull request prête',
        publicationTargetFailed: 'Non publié',
        publicationOwnershipDescription: 'Des fichiers ont changé. Rien n’a été écrit.',
        publicationStaleConfirmation: 'Je confirme publier cette révision périmée.',
        publicationTargets: '{{ count }} dépôt(s) préparé(s) et vérifié(s).',
        publicationReview: 'Revoir la publication',
        publicationConfirmationRequired: 'Confirmation requise',
        publicationConfirmOwnership: 'Confirmer la prise en charge',
        publicationOwnershipReplace:
          'Remplacer la modification manuelle par le contenu de la révision',
        publicationOwnershipConfirmed: 'Prise en charge confirmée',
        publicationWrite: 'Publier dans {{ count }} dépôt(s)',
        publicationWriting: 'Écriture en cours…',
        publicationWriteError: 'La publication n’a pas pu être écrite.',
        publicationNoFiles: 'Aucun fichier à publier.',
        publicationPullRequest: 'Pull request',
        publicationCommit: 'Commit',
        publicationFailure: 'Non publié : {{ code }}',
        publicationResult: 'Révision publiée dans {{ completed }} dépôt(s) sur {{ total }}',
        publicationActionAdd: 'Ajout',
        publicationActionUpdate: 'Modifié',
        publicationActionDelete: 'Supprimé',
        publicationActionUnchanged: 'Inchangé',
        publicationRetryTarget: 'Republier {{ repository }}',
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

const publication: PublicationResponse = {
  completedAt: null,
  id: 'publication-id',
  message: '',
  preparedAt: null,
  projectId: 'project-id',
  requestedAt: '2026-11-14T09:00:00Z',
  revisionId: 'revision-id',
  revisionNumber: 1,
  state: 'Preparing',
  targets: [
    {
      commitSha: null,
      destinations: [{ basePath: '', id: 'destination-id' }],
      failureCode: null,
      files: [],
      gitConnectionId: 'connection-id',
      id: 'target-id',
      mergeCommitSha: null,
      ownershipConfirmed: false,
      providerRepositoryId: 'repository-id',
      pullRequestId: null,
      pullRequestState: 'None',
      pullRequestUrl: null,
      repositoryName: 'shop',
      requiresConfirmation: false,
      sourceBranch: 'ifs/shop/revision-1',
      state: 'Preparing',
      targetBranch: 'main',
    },
  ],
};

const publicationWithTarget = (
  state: string,
  targetState: string,
  targetOverrides: Partial<PublicationResponse['targets'][number]> = {},
): PublicationResponse => ({
  ...publication,
  state,
  targets: [
    {
      ...publication.targets[0],
      state: targetState,
      ...targetOverrides,
    },
  ],
});

describe('RevisionDetail', () => {
  it('shows the revision tabs and opens a selected generated file', async () => {
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
        {
          provide: PublicationsService,
          useValue: {
            list: vi.fn().mockReturnValue(of([])),
            start: vi.fn().mockReturnValue(of(publication)),
            get: vi.fn().mockReturnValue(of(publication)),
            confirmOwnership: vi.fn().mockReturnValue(of(publication)),
            publish: vi.fn().mockReturnValue(of(publication)),
          },
        },
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
    expect(screen.getByRole('tab', { name: 'Publications' })).toBeTruthy();

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

  it('prepares a pull request publication from the revision detail', async () => {
    const revisions = {
      get: vi.fn().mockReturnValue(of(detail)),
      plan: vi.fn().mockReturnValue(of({ project: {}, components: [], pins: {} })),
      readFile: vi.fn().mockReturnValue(of('')),
      downloadFile: vi.fn().mockReturnValue(of(new Blob(['file']))),
      downloadArchive: vi.fn().mockReturnValue(of(new Blob(['zip']))),
    };
    const publications = {
      list: vi.fn().mockReturnValue(of([])),
      start: vi.fn().mockReturnValue(of(publication)),
      get: vi.fn().mockReturnValue(of(publication)),
      confirmOwnership: vi.fn().mockReturnValue(of(publication)),
      publish: vi.fn().mockReturnValue(of(publication)),
    };
    await render(RevisionDetail, {
      inputs: { projectId: 'project-id', revisionId: 'revision-id' },
      providers: [
        provideRouter([]),
        { provide: RevisionsService, useValue: revisions },
        { provide: PublicationsService, useValue: publications },
        {
          provide: CodeHighlightService,
          useValue: { highlight: vi.fn().mockResolvedValue('<pre><code></code></pre>') },
        },
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

    fireEvent.click(await screen.findByRole('tab', { name: 'Publications' }));
    expect(await screen.findByRole('heading', { name: 'Publications' })).toBeTruthy();
    expect(await screen.findByText('Aucune publication pour cette révision.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Publier' }));
    expect(await screen.findByRole('dialog')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Préparer la publication' }));

    await waitFor(() =>
      expect(publications.start).toHaveBeenCalledWith('project-id', 'revision-id', '', false),
    );
    expect((await screen.findAllByText('Préparation en cours…')).length).toBeGreaterThan(0);
  });

  it('requires ownership confirmation before writing repository changes', async () => {
    const needsConfirmation = publicationWithTarget('NeedsConfirmation', 'NeedsConfirmation', {
      requiresConfirmation: true,
      files: [
        {
          action: 'Update',
          destinationId: 'destination-id',
          existingSha256: 'old-sha',
          path: 'infra/main.bicep',
          sha256: 'new-sha',
        },
      ],
    });
    const prepared = publicationWithTarget('Prepared', 'Prepared', {
      requiresConfirmation: true,
      ownershipConfirmed: true,
    });
    const writing = publicationWithTarget('Publishing', 'Publishing');
    const publications = {
      list: vi.fn().mockReturnValue(of([needsConfirmation])),
      start: vi.fn().mockReturnValue(of(needsConfirmation)),
      get: vi.fn().mockReturnValue(of(writing)),
      confirmOwnership: vi.fn().mockReturnValue(of(prepared)),
      publish: vi.fn().mockReturnValue(of(writing)),
    };
    await render(RevisionDetail, {
      inputs: { projectId: 'project-id', revisionId: 'revision-id' },
      providers: [
        provideRouter([]),
        {
          provide: RevisionsService,
          useValue: {
            get: vi.fn().mockReturnValue(of(detail)),
            plan: vi.fn().mockReturnValue(of({ project: {}, components: [], pins: {} })),
            readFile: vi.fn().mockReturnValue(of('')),
            downloadFile: vi.fn().mockReturnValue(of(new Blob(['file']))),
            downloadArchive: vi.fn().mockReturnValue(of(new Blob(['zip']))),
          },
        },
        { provide: PublicationsService, useValue: publications },
        {
          provide: CodeHighlightService,
          useValue: { highlight: vi.fn().mockResolvedValue('<pre><code></code></pre>') },
        },
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

    fireEvent.click(await screen.findByRole('tab', { name: 'Publications' }));
    expect((await screen.findAllByText('Confirmation requise')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Publier dans 1 dépôt(s)' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Revoir la publication' }));
    expect(await screen.findByRole('dialog')).toBeTruthy();
    fireEvent.click(
      screen.getByRole('checkbox', {
        name: 'Remplacer la modification manuelle par le contenu de la révision',
      }),
    );
    expect(await screen.findByText('Prise en charge confirmée')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Publier dans 1 dépôt(s)' }));

    await waitFor(() =>
      expect(publications.publish).toHaveBeenCalledWith('project-id', 'publication-id'),
    );
    expect((await screen.findAllByText('Écriture en cours…')).length).toBeGreaterThan(0);
  });

  it('retries only the failed repository destinations', async () => {
    const partial = publicationWithTarget('PartiallyPublished', 'Failed', {
      failureCode: 'PROVIDER_FAILURE',
      pullRequestId: null,
    });
    const publications = {
      list: vi.fn().mockReturnValue(of([partial])),
      start: vi.fn().mockReturnValue(of(publication)),
      get: vi.fn().mockReturnValue(of(publication)),
      confirmOwnership: vi.fn().mockReturnValue(of(publication)),
      publish: vi.fn().mockReturnValue(of(publication)),
    };
    await render(RevisionDetail, {
      inputs: { projectId: 'project-id', revisionId: 'revision-id' },
      providers: [
        provideRouter([]),
        {
          provide: RevisionsService,
          useValue: {
            get: vi.fn().mockReturnValue(of(detail)),
            plan: vi.fn().mockReturnValue(of({ project: {}, components: [], pins: {} })),
            readFile: vi.fn().mockReturnValue(of('')),
            downloadFile: vi.fn().mockReturnValue(of(new Blob(['file']))),
            downloadArchive: vi.fn().mockReturnValue(of(new Blob(['zip']))),
          },
        },
        { provide: PublicationsService, useValue: publications },
        {
          provide: CodeHighlightService,
          useValue: { highlight: vi.fn().mockResolvedValue('<pre><code></code></pre>') },
        },
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

    fireEvent.click(await screen.findByRole('tab', { name: 'Publications' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Revoir la publication' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Republier shop' }));

    await waitFor(() =>
      expect(publications.start).toHaveBeenCalledWith('project-id', 'revision-id', '', true, [
        'destination-id',
      ]),
    );
  });
});
