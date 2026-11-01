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
        clearOverride: 'Effacer la surcharge',
        provenance: {
          absent: 'Valeur absente',
          catalogDefault: 'Défaut du catalogue',
          override: 'Surcharge de l’environnement',
          resource: 'Valeur de la ressource',
        },
      },
    });
  }
}

describe('PropertyGrid', () => {
  it('allows descriptor list properties to be edited as arrays', () => {
    expect(
      isPropertyEditable({
        name: 'workloadProfiles',
        kind: RESOURCE_PROPERTY_KIND.Array,
        overridable: true,
      }),
    ).toBe(true);
    expect(
      isPropertyEditable({
        name: 'configuration',
        kind: RESOURCE_PROPERTY_KIND.Object,
        overridable: true,
      }),
    ).toBe(false);
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

  it('keeps enum selections synchronized and represents missing values', async () => {
    const row: PropertyGridRow = {
      definition: {
        name: 'memoryGi',
        kind: 'enum',
        overridable: true,
        labels: { fr: 'Mémoire (Gi)', en: 'Memory (Gi)' },
        values: ['0.5Gi', '1Gi', '2Gi'],
      },
      resource: { value: '1Gi', provenance: 'resource' },
      targets: [
        {
          targetId: '33333333-3333-4333-8333-333333333333',
          code: 'dev',
          applicable: true,
          value: '2Gi',
          provenance: 'override',
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
    const selects = Array.from(element.querySelectorAll('select'));
    expect(selects.map((select) => select.selectedOptions[0]?.textContent?.trim())).toEqual([
      '1Gi',
      '2Gi',
    ]);

    const updates: PropertyGridUpdate[] = [];
    fixture.componentInstance.updateValue.subscribe((update) => updates.push(update));
    selects[0].value = `value:${JSON.stringify('0.5Gi')}`;
    selects[0].dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(updates).toEqual([{ property: 'memoryGi', targetId: null, value: '0.5Gi' }]);
    expect(selects[0].selectedOptions[0]?.textContent?.trim()).toBe('0.5Gi');

    fixture.componentRef.setInput('syncRevision', 1);
    fixture.detectChanges();
    expect(selects[0].selectedOptions[0]?.textContent?.trim()).toBe('1Gi');

    fixture.componentRef.setInput('rows', [
      {
        ...row,
        resource: { value: null, provenance: 'absent' },
        targets: [{ ...row.targets[0], value: null, provenance: 'absent' }],
      },
    ]);
    fixture.detectChanges();
    expect(selects.every((select) => select.selectedOptions[0]?.disabled)).toBe(true);

    fixture.componentRef.setInput('rows', [
      {
        ...row,
        resource: { value: '4Gi', provenance: 'resource' },
      },
    ]);
    fixture.detectChanges();
    expect(selects[0].selectedOptions[0]?.textContent?.trim()).toBe('4Gi');
    expect(selects[0].selectedOptions[0]?.disabled).toBe(true);

    fixture.componentRef.setInput('rows', [
      {
        definition: {
          name: 'mixedEnum',
          kind: 'enum',
          overridable: true,
          values: [1, '1'],
        },
        resource: { value: '1', provenance: 'resource' },
        targets: [
          {
            targetId: '33333333-3333-4333-8333-333333333333',
            code: 'dev',
            applicable: true,
            value: 1,
            provenance: 'override',
          },
        ],
      },
    ]);
    fixture.detectChanges();
    const mixedSelects = Array.from(element.querySelectorAll('select'));
    expect(mixedSelects.map((select) => select.value)).toEqual([
      `value:${JSON.stringify('1')}`,
      `value:${JSON.stringify(1)}`,
    ]);

    fixture.componentRef.setInput('rows', [
      {
        definition: {
          name: 'nullableEnum',
          kind: 'enum',
          overridable: true,
          values: [null, 'enabled'],
        },
        resource: { value: null, provenance: 'absent' },
        targets: [],
      },
    ]);
    fixture.detectChanges();
    const absentNullSelect = element.querySelector('select') as HTMLSelectElement;
    expect(absentNullSelect.selectedOptions[0]?.textContent?.trim()).toBe('Valeur absente');
    expect(absentNullSelect.selectedOptions[0]?.disabled).toBe(true);

    fixture.componentRef.setInput('rows', [
      {
        definition: {
          name: 'nullableEnum',
          kind: 'enum',
          overridable: true,
          values: [null, 'enabled'],
        },
        resource: { value: null, provenance: 'resource' },
        targets: [],
      },
    ]);
    fixture.detectChanges();
    const explicitNullSelect = element.querySelector('select') as HTMLSelectElement;
    expect(explicitNullSelect.selectedOptions[0]?.textContent?.trim()).toBe('null');
    expect(explicitNullSelect.selectedOptions[0]?.disabled).toBe(false);

    fixture.componentRef.setInput('rows', [
      {
        definition: {
          name: 'nullableEnum',
          kind: 'enum',
          overridable: true,
          values: ['enabled'],
        },
        resource: { value: null, provenance: 'resource' },
        targets: [],
      },
    ]);
    fixture.detectChanges();
    const unknownNullSelect = element.querySelector('select') as HTMLSelectElement;
    expect(unknownNullSelect.selectedOptions[0]?.textContent?.trim()).toBe('null');
    expect(unknownNullSelect.selectedOptions[0]?.disabled).toBe(true);
    fixture.destroy();
  });

  it('restores boolean, text and array controls when the server state is resynchronized', async () => {
    const rows: PropertyGridRow[] = [
      {
        definition: { name: 'purgeProtection', kind: 'boolean', overridable: true },
        resource: { value: true, provenance: 'resource' },
        targets: [],
      },
      {
        definition: { name: 'workspaceName', kind: 'string', overridable: true },
        resource: { value: 'saved-name', provenance: 'resource' },
        targets: [],
      },
      {
        definition: { name: 'allowedIpRanges', kind: 'array', overridable: true },
        resource: { value: ['10.0.0.0/8'], provenance: 'resource' },
        targets: [],
      },
    ];

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
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const checkbox = element.querySelector('input[type="checkbox"]') as HTMLInputElement;
    const textInput = element.querySelector('input[type="text"]') as HTMLInputElement;
    const textarea = element.querySelector('textarea') as HTMLTextAreaElement;

    checkbox.checked = false;
    checkbox.dispatchEvent(new Event('change'));
    textInput.value = 'unsaved-name';
    textInput.dispatchEvent(new Event('change'));
    textarea.value = '["192.168.0.0/16"]';
    textarea.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(checkbox.checked).toBe(false);
    expect(textInput.value).toBe('unsaved-name');
    expect(textarea.value).toBe('["192.168.0.0/16"]');

    fixture.componentRef.setInput('syncRevision', 1);
    fixture.detectChanges();
    expect(checkbox.checked).toBe(true);
    expect(textInput.value).toBe('saved-name');
    expect(textarea.value).toBe('["10.0.0.0/8"]');
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
    const textarea = element.querySelector(
      'textarea[aria-label="Régions autorisées — Valeur de la ressource"]',
    ) as HTMLTextAreaElement;
    textarea.value = '["westeurope", "francecentral"]';
    textarea.dispatchEvent(new Event('change'));

    expect(updates).toEqual([
      { property: 'allowedRegions', targetId: null, value: ['westeurope', 'francecentral'] },
    ]);
    fixture.destroy();
  });
});
