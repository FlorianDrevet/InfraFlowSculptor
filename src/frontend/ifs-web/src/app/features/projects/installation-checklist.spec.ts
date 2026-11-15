import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen } from '@testing-library/angular';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { InstallationChecklistResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { InstallationChecklist } from './installation-checklist';

const projectId = '77777777-7777-4777-8777-777777777777';
const itemKey = 'run-azure-script';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        installationTitle: "Liste de contrôle d'installation",
        installationChecklistTitle: "Liste de contrôle d'installation",
        installationCopyCommand: 'Copier la commande Azure',
        installationLoadError: 'La liste de contrôle n’a pas pu être chargée.',
        retry: 'Réessayer',
      },
    });
  }
}

const checklist: InstallationChecklistResponse = {
  revisionNumber: 1,
  sections: [
    {
      code: 'azureScript',
      title: '2. Script Azure',
      subtitle: 'Prévisualiser les opérations avant de créer les ressources',
      items: [
        {
          key: itemKey,
          text: 'Prévisualiser puis exécuter le script Azure.',
          state: 'ToDo',
          isUserActionable: true,
          command: '.\\.ifs\\install\\azure-setup.ps1 -WhatIf',
          isNewSincePrevious: false,
          completedAt: null,
        },
      ],
    },
  ],
};

describe('InstallationChecklist', () => {
  it('renders the generated checklist and saves a checked human step', async () => {
    const setInstallationChecklistItem = vi.fn().mockReturnValue(of(undefined));
    const service = {
      installationChecklist: vi.fn().mockReturnValue(of(checklist)),
      setInstallationChecklistItem,
      get: vi.fn().mockReturnValue(of({ id: projectId, code: 'shop' })),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['installation.manage'] })),
    };
    await render(InstallationChecklist, {
      inputs: { projectId },
      providers: [
        { provide: ProjectsService, useValue: service },
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

    expect(
      await screen.findByRole('heading', { name: "Liste de contrôle d'installation", level: 1 }),
    ).toBeTruthy();
    expect(screen.getByText('2. Script Azure')).toBeTruthy();
    fireEvent.click(
      screen.getByRole('checkbox', { name: 'Prévisualiser puis exécuter le script Azure.' }),
    );
    await vi.waitFor(() =>
      expect(setInstallationChecklistItem).toHaveBeenCalledWith(projectId, itemKey, {
        isCompleted: true,
      }),
    );
  });

  it('reloads the checklist after a load error when retry is selected', async () => {
    const installationChecklist = vi
      .fn()
      .mockReturnValueOnce(throwError(() => new Error('offline')))
      .mockReturnValue(of(checklist));
    const service = {
      installationChecklist,
      get: vi.fn().mockReturnValue(of({ id: projectId, code: 'shop' })),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: [] })),
    };

    await render(InstallationChecklist, {
      inputs: { projectId },
      providers: [
        { provide: ProjectsService, useValue: service },
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

    expect((await screen.findByRole('alert')).textContent).toContain(
      'La liste de contrôle n’a pas pu être chargée.',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Réessayer' }));

    expect(await screen.findByText('2. Script Azure')).toBeTruthy();
    expect(installationChecklist).toHaveBeenCalledTimes(2);
  });
});
