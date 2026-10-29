import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import type { PropertyGridRow } from './property-grid.model';
import type { PropertyGridUpdate } from './property-grid-update';
import { isPropertyEditable, RESOURCE_PROPERTY_KIND } from './property-grid.model';
import { PropertyGrid } from './property-grid';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        properties: 'Propriétés',
        property: 'Propriété',
        resourceValue: 'Valeur de la ressource',
        valueAbsent: 'Valeur absente',
        arrayInvalid: 'Saisissez une liste JSON valide.',
        provenance: { catalogDefault: 'Défaut du catalogue' },
      },
    });
  }
}

describe('PropertyGrid', () => {
  it('allows descriptor list properties to be edited as arrays', () => {
    expect(isPropertyEditable({ name: 'workloadProfiles', kind: RESOURCE_PROPERTY_KIND.Array, overridable: true })).toBe(true);
    expect(isPropertyEditable({ name: 'configuration', kind: RESOURCE_PROPERTY_KIND.Object, overridable: true })).toBe(false);
  });

  it('exposes table semantics and names editable controls with property and target', async () => {
    const row: PropertyGridRow = {
      definition: {
        name: 'retentionInDays',
        kind: 'integer',
        overridable: true,
        labels: { fr: 'Rétention (jours)', en: 'Retention (days)' },
      },
      resource: { value: 30, provenance: 'resource' },
      targets: [
        {
          targetId: '33333333-3333-4333-8333-333333333333',
          code: 'prd',
          applicable: true,
          value: 30,
          provenance: 'catalogDefault',
        },
      ],
    };

    await TestBed.configureTestingModule({
      imports: [PropertyGrid],
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

    const fixture = TestBed.createComponent(PropertyGrid);
    fixture.componentRef.setInput('rows', [row]);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const table = element.querySelector('[role="table"]') as HTMLElement;
    expect(table.getAttribute('aria-label')).toBe('Propriétés');
    expect(table.querySelectorAll('[role="row"]')).toHaveLength(2);
    expect(table.querySelectorAll('[role="cell"]')).toHaveLength(2);
    expect(
      Array.from(table.querySelectorAll('input')).map((input) => input.getAttribute('aria-label')),
    ).toEqual(['Rétention (jours) — Valeur de la ressource', 'Rétention (jours) — prd']);
    fixture.destroy();
  });

  it('emits parsed array values from the resource cell', async () => {
    const row: PropertyGridRow = {
      definition: {
        name: 'allowedRegions',
        kind: 'array',
        overridable: true,
        labels: { fr: 'Régions autorisées', en: 'Allowed regions' },
      },
      resource: { value: ['westeurope'], provenance: 'resource' },
      targets: [
        {
          targetId: '33333333-3333-4333-8333-333333333333',
          code: 'dev',
          applicable: true,
          value: ['westeurope'],
          provenance: 'resource',
        },
      ],
    };

    await TestBed.configureTestingModule({
      imports: [PropertyGrid],
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

    const fixture = TestBed.createComponent(PropertyGrid);
    fixture.componentRef.setInput('rows', [row]);
    fixture.detectChanges();
    const updates: PropertyGridUpdate[] = [];
    fixture.componentInstance.updateValue.subscribe((update) => updates.push(update));
    const element = fixture.nativeElement as HTMLElement;
    const textarea = element.querySelector('textarea[aria-label="Régions autorisées — Valeur de la ressource"]') as HTMLTextAreaElement;
    textarea.value = '["westeurope", "francecentral"]';
    textarea.dispatchEvent(new Event('change'));

    expect(updates).toEqual([
      { property: 'allowedRegions', targetId: null, value: ['westeurope', 'francecentral'] },
    ]);
    fixture.destroy();
  });
});
