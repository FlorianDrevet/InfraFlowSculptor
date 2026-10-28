import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectMembers } from './project-members';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement…',
        loadError: 'Impossible de charger le projet.',
        membersTitle: 'Membres du projet',
        membersDescription: 'Gérez les rôles des membres.',
      },
    });
  }
}

describe('ProjectMembers', () => {
  it('shows a load error instead of hiding roles when a member permission lookup fails', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const service = {
      get: vi.fn().mockReturnValue(of({ id: projectId, version: 1, name: 'shop', code: 'shop' })),
      myPermissions: vi
        .fn()
        .mockReturnValue(
          of({ memberId: 'alice', permissions: ['members.manage'], assignments: [], version: 1 }),
        ),
      memberPermissions: vi.fn().mockReturnValue(throwError(() => new Error('Unavailable'))),
    };
    const organizations = {
      listMembers: vi.fn().mockReturnValue(
        of([
          {
            id: 'bob',
            displayName: 'Bob Bernard',
            email: 'bob@contoso.example',
            roles: [],
            isOutsideAccessRestrictions: false,
          },
        ]),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectMembers],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId/members', component: ProjectMembers }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: service },
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
    await harness.navigateByUrl(`/o/contoso/projects/${projectId}/members`, ProjectMembers);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    expect(service.memberPermissions).toHaveBeenCalled();
    await vi.waitFor(() => {
      expect(harness.routeNativeElement?.querySelector('[role="alert"]')).not.toBeNull();
    });
    expect(harness.routeNativeElement?.querySelector('[role="alert"]')?.textContent).toContain(
      'Impossible de charger le projet.',
    );
    expect(harness.routeNativeElement?.querySelector('table')).toBeNull();
  });
});
