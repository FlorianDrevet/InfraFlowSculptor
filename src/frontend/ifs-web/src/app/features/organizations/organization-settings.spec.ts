import { inputBinding } from '@angular/core';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { OrganizationSettings } from './organization-settings';

const organizationId = '33333333-3333-4333-8333-333333333333';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      organization: {
        settingsTitle: 'Paramètres de l’organisation',
        loading: 'Chargement',
        nameLabel: 'Nom',
        accessHeading: 'Restrictions d’accès',
        allowedEmailDomains: 'Domaines autorisés',
        allowedTenantIds: 'Tenants autorisés',
        allowedTenantsHint: 'Identifiants de tenant',
        saveError: 'Impossible d’enregistrer les paramètres.',
        saving: 'Enregistrement',
        saveAction: 'Enregistrer',
      },
    });
  }
}

describe('OrganizationSettings', () => {
  it('does not submit empty restrictions after the organization failed to load', async () => {
    const rename = vi.fn().mockReturnValue(of({}));
    const updateAccessRestrictions = vi.fn().mockReturnValue(of({}));
    const service = {
      get: vi.fn().mockReturnValue(throwError(() => new Error('offline'))),
      rename,
      updateAccessRestrictions,
    };

    const { fixture } = await render(OrganizationSettings, {
      bindings: [inputBinding('id', () => organizationId)],
      providers: [
        { provide: OrganizationsService, useValue: service },
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

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Impossible d’enregistrer les paramètres.'));
    const component = fixture.componentInstance as unknown as {
      name: { set(value: string): void };
      save(): Promise<void>;
    };
    component.name.set('Contoso');

    await component.save();

    expect(rename).not.toHaveBeenCalled();
    expect(updateAccessRestrictions).not.toHaveBeenCalled();
  });
});
