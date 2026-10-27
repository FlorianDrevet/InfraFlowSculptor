import { inputBinding, signal } from '@angular/core';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { AuditEventPageResponse } from '../../core/api/generated';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { OrganizationAudit } from './organization-audit';

const organizationId = '33333333-3333-4333-8333-333333333333';

const auditPage: AuditEventPageResponse = {
  items: [
    {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      occurredAt: '2026-10-27T12:00:00Z',
      actorDisplayName: 'Alice Martin',
      via: 'Screen',
      action: 'organization.member.roles_changed',
      objectType: 'OrganizationMember',
      objectId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      projectId: null,
      diff: {
        changes: [
          {
            name: 'roleValues',
            before: 'Member',
            after: 'Member, ConnectionManager',
            isPersonalData: false,
          },
        ],
      },
    },
  ],
  page: 1,
  pageSize: 50,
  totalCount: 1,
};

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      audit: {
        title: 'Journal d’audit',
        description: 'Toutes les actions sensibles.',
        period: 'Période',
        periods: {
          today: 'Aujourd’hui',
          sevenDays: '7 derniers jours',
          thirtyDays: '30 derniers jours',
          ninetyDays: '90 derniers jours',
        },
        columns: {
          date: 'Date (UTC)',
          actor: 'Auteur',
          action: 'Action',
          objectAndChanges: 'Objet et différence',
          context: 'Contexte',
        },
        loading: 'Chargement',
        loadError: 'Erreur de chargement',
        retry: 'Réessayer',
        permissionDenied: 'Permission requise : audit.lire.',
        empty: 'Aucun événement.',
        pagination: 'Pages du journal',
        previous: 'Précédent',
        next: 'Suivant',
        pageStatus: 'Page {{ page }} sur {{ pageCount }}',
        personalDataErased: 'Donnée personnelle effacée',
        actions: {
          memberRolesChanged: 'Rôles du membre modifiés',
        },
        fields: {
          roles: 'Rôles',
        },
        objects: {
          member: 'Membre',
        },
        via: {
          screen: 'Écran',
        },
      },
    });
  }
}

function renderPage(listAuditEvents: ReturnType<typeof vi.fn>) {
  return render(OrganizationAudit, {
    bindings: [inputBinding('id', () => organizationId)],
    providers: [
      {
        provide: OrganizationsService,
        useValue: {
          listAuditEvents,
          organizations: signal([{ id: organizationId, name: 'Contoso Labs' }]),
        },
      },
      { provide: LanguageService, useValue: { activeLanguage: signal('fr') } },
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
}

describe('OrganizationAudit', () => {
  it('shows the actor, action, and role changes returned by the audit API', async () => {
    const listAuditEvents = vi.fn().mockReturnValue(of(auditPage));
    await renderPage(listAuditEvents);

    expect(await screen.findByRole('heading', { name: 'Journal d’audit' })).toBeDefined();
    expect(await screen.findByText('Alice Martin')).toBeDefined();
    expect(screen.getByText('Rôles du membre modifiés')).toBeDefined();
    expect(screen.getByText('Member → Member, ConnectionManager')).toBeDefined();
    expect(screen.getByLabelText('Période')).toBeDefined();
    expect(listAuditEvents).toHaveBeenCalledWith(
      organizationId,
      expect.objectContaining({ page: 1, pageSize: 50 }),
    );
  });

  it('shows the active organization as the audit context', async () => {
    const listAuditEvents = vi.fn().mockReturnValue(of(auditPage));
    await renderPage(listAuditEvents);

    expect(await screen.findByRole('columnheader', { name: 'Contexte' })).toBeDefined();
    expect(screen.getByText('Contoso Labs')).toBeDefined();
  });

  it('states the required permission when a member cannot read the audit log', async () => {
    const listAuditEvents = vi.fn().mockReturnValue(
      throwError(() => new ApiError(403, 'ORG_ROLE_REQUIRED', 'Permission required')),
    );
    await renderPage(listAuditEvents);

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Permission requise : audit.lire.');
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('loads a new page when the period changes', async () => {
    const listAuditEvents = vi.fn().mockReturnValue(of(auditPage));
    await renderPage(listAuditEvents);

    fireEvent.change(screen.getByLabelText('Période'), { target: { value: '7' } });

    await waitFor(() => expect(listAuditEvents).toHaveBeenCalledTimes(2));
    const [, parameters] = listAuditEvents.mock.calls[1] as [string, { from: string; page: number }];
    const days = (Date.now() - new Date(parameters.from).getTime()) / 86_400_000;
    expect(days).toBeGreaterThanOrEqual(6.99);
    expect(days).toBeLessThan(7.01);
    expect(parameters.page).toBe(1);
  });

  it('starts the today filter at midnight UTC', async () => {
    const listAuditEvents = vi.fn().mockReturnValue(of(auditPage));
    await renderPage(listAuditEvents);

    fireEvent.change(screen.getByLabelText('Période'), { target: { value: 'today' } });

    await waitFor(() => expect(listAuditEvents).toHaveBeenCalledTimes(2));
    const [, parameters] = listAuditEvents.mock.calls[1] as [string, { from: string; to: string }];
    const from = new Date(parameters.from);
    const through = new Date(parameters.to);
    expect(from.getUTCDate()).toBe(through.getUTCDate());
    expect(from.getUTCHours()).toBe(0);
    expect(from.getUTCMinutes()).toBe(0);
    expect(from.getUTCSeconds()).toBe(0);
  });
});
