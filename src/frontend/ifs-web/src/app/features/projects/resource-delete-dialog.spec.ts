import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import type { ResourceDeletionImpactResponse } from '../../core/api/generated';
import { ResourceDeleteDialog } from './resource-delete-dialog';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        deleteTitle: 'Supprimer {{name}}',
        deleteRemovesAzureResource: 'La ressource Azure sera supprimée.',
        loadingImpact: 'Calcul de l’impact…',
        deletedWithResource: 'Éléments concernés',
        noCalculatedImpacts: 'Aucun impact calculé.',
        confirmDeleteByName: 'Saisir le nom {{name}}',
        cancel: 'Annuler',
        delete: 'Supprimer',
      },
    });
  }
}

describe('ResourceDeleteDialog', () => {
  it('blocks confirmation for a blocked impact but allows an acknowledged partial impact', async () => {
    await TestBed.configureTestingModule({
      imports: [ResourceDeleteDialog],
      providers: [
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

    const fixture = TestBed.createComponent(ResourceDeleteDialog);
    fixture.componentRef.setInput('resource', { logicalName: 'api' });
    fixture.componentRef.setInput('removedBehavior', 'Delete');
    fixture.componentRef.setInput('confirmation', 'api');
    fixture.componentRef.setInput('impact', {
      status: 'Blocked',
      severity: 'Blocker',
      pendingAreas: [],
      items: [],
      resourceVersion: 1,
    } satisfies ResourceDeletionImpactResponse);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const deleteButton = host.querySelectorAll('button').item(1);
    expect(deleteButton.disabled).toBe(true);

    fixture.componentRef.setInput('impact', {
      status: 'Partial',
      severity: 'Information',
      pendingAreas: ['ImplicitEffects'],
      items: [],
      resourceVersion: 1,
    } satisfies ResourceDeletionImpactResponse);
    fixture.detectChanges();

    expect(deleteButton.disabled).toBe(false);
    fixture.destroy();
  });
});
