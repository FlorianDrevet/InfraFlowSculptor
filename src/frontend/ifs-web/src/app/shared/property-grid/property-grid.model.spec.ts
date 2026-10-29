import { describe, expect, it } from 'vitest';
import type { EffectiveValueTargetResponse } from '../../core/api/generated/models/effective-value-target-response';
import {
  canClearOverride,
  createClearOverrideRequest,
  createPropertyGridRows,
  isPropertyEditable,
  parseResourceTypeProperties,
  type ResourcePropertyDefinition,
} from './property-grid.model';

describe('property grid model', () => {
  const definitions: ResourcePropertyDefinition[] = [
    { name: 'retention', kind: 'integer', overridable: true, default: 30 },
    { name: 'sku', kind: 'enum', overridable: true, default: 'basic', values: ['basic', 'premium'] },
    { name: 'sampling', kind: 'number', overridable: true, default: 100 },
    { name: 'dailyQuotaGb', kind: 'number', overridable: true },
    { name: 'workloadProfiles', kind: 'array', overridable: true, default: [] },
  ];

  const targets: EffectiveValueTargetResponse[] = [
    {
      id: 'dev-id',
      code: 'dev',
      isPresent: true,
      region: 'francecentral',
      tags: {},
      properties: [
        { name: 'retention', value: 30, provenance: 'resource' },
        { name: 'sku', value: 'premium', provenance: 'resource' },
        { name: 'sampling', value: 100, provenance: 'catalogDefault' },
        { name: 'dailyQuotaGb', value: null, provenance: 'absent' },
        { name: 'workloadProfiles', value: [], provenance: 'catalogDefault' },
      ],
    },
    {
      id: 'prd-id',
      code: 'prd',
      isPresent: true,
      region: 'francecentral',
      tags: {},
      properties: [
        { name: 'retention', value: 90, provenance: 'override' },
        { name: 'sku', value: 'premium', provenance: 'resource' },
        { name: 'sampling', value: 100, provenance: 'catalogDefault' },
        { name: 'dailyQuotaGb', value: null, provenance: 'absent' },
        { name: 'workloadProfiles', value: [], provenance: 'catalogDefault' },
      ],
    },
  ];

  it('preserves override, resource, catalog-default and absent values from the server', () => {
    const rows = createPropertyGridRows(
      definitions,
      { retention: 30, sku: 'premium' },
      targets,
    );

    const retention = rows.find((row) => row.definition.name === 'retention');
    expect(retention?.resource).toEqual({ value: 30, provenance: 'resource' });
    expect(retention?.targets.map((cell) => [cell.value, cell.provenance])).toEqual([
      [30, 'resource'],
      [90, 'override'],
    ]);

    const sampling = rows.find((row) => row.definition.name === 'sampling');
    expect(sampling?.resource).toEqual({ value: 100, provenance: 'catalogDefault' });
    expect(sampling?.targets.map((cell) => cell.provenance)).toEqual([
      'catalogDefault',
      'catalogDefault',
    ]);

    const dailyQuota = rows.find((row) => row.definition.name === 'dailyQuotaGb');
    expect(dailyQuota?.resource).toEqual({ value: null, provenance: 'absent' });
    expect(dailyQuota?.targets.map((cell) => [cell.value, cell.provenance])).toEqual([
      [null, 'absent'],
      [null, 'absent'],
    ]);
  });

  it('clears an override with an explicit target and property command', () => {
    const rows = createPropertyGridRows(definitions, { retention: 30 }, targets);
    const retention = rows.find((row) => row.definition.name === 'retention');
    expect(retention).toBeDefined();

    const inheritedCell = retention!.targets[0];
    const overrideCell = retention!.targets[1];
    expect(canClearOverride(retention!.definition, inheritedCell)).toBe(false);
    expect(canClearOverride(retention!.definition, overrideCell)).toBe(true);
    expect(createClearOverrideRequest(retention!.definition, overrideCell)).toEqual({
      targetId: 'prd-id',
      property: 'retention',
    });
    expect(createClearOverrideRequest(retention!.definition, overrideCell)).not.toHaveProperty('value');
  });

  it('keeps arrays visible and editable without changing their declared item values', () => {
    const rows = createPropertyGridRows(
      definitions,
      { workloadProfiles: [{ name: 'D4', limits: { cpu: 1.5 } }] },
      targets,
    );
    const profiles = rows.find((row) => row.definition.name === 'workloadProfiles');

    expect(profiles?.resource).toEqual({
      value: [{ name: 'D4', limits: { cpu: 1.5 } }],
      provenance: 'resource',
    });
    expect(isPropertyEditable(profiles!.definition)).toBe(true);
    expect(isPropertyEditable(definitions[0])).toBe(true);
    expect(isPropertyEditable({ name: 'unknown', kind: 'object', overridable: true })).toBe(false);
  });

  it('does not invent item shapes that the catalogue descriptor does not declare', () => {
    const parsed = parseResourceTypeProperties({
      properties: [
        {
          name: 'workloadProfiles',
          labels: { fr: 'Profils de charge dédiés', en: 'Dedicated workload profiles' },
          kind: 'array',
          overridable: true,
          default: [],
        },
      ],
    });

    expect(parsed).toEqual([
      expect.objectContaining({
        name: 'workloadProfiles',
        labels: { fr: 'Profils de charge dédiés', en: 'Dedicated workload profiles' },
        kind: 'array',
        default: [],
      }),
    ]);
    expect(isPropertyEditable(parsed[0])).toBe(true);
  });
});
