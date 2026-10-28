import type { JsonElement } from '../../core/api/generated/models/json-element';
import type { EffectiveValueTargetResponse } from '../../core/api/generated/models/effective-value-target-response';

export const RESOURCE_PROPERTY_KIND = {
  Boolean: 'boolean',
  Integer: 'integer',
  Number: 'number',
  Enum: 'enum',
  String: 'string',
  Array: 'array',
  Object: 'object',
  Reference: 'reference',
} as const;

export const EFFECTIVE_VALUE_PROVENANCE = {
  Override: 'override',
  Resource: 'resource',
  CatalogDefault: 'catalogDefault',
  Absent: 'absent',
} as const;

export interface ResourcePropertyDefinition {
  name: string;
  kind: string;
  overridable: boolean;
  labels?: { fr: string; en: string };
  default?: JsonElement;
  values?: JsonElement[];
  minimum?: number;
  maximum?: number;
  minLength?: number;
  maxLength?: number;
  pattern?: string;
  lockedAfterPublish?: boolean;
  condition?: string;
  description?: string;
}

export interface PropertyGridCell {
  targetId: string;
  code: string;
  applicable: boolean;
  value: JsonElement | null;
  provenance: string | null;
}

export interface PropertyGridRow {
  definition: ResourcePropertyDefinition;
  resource: { value: JsonElement | null; provenance: string };
  targets: PropertyGridCell[];
}

export function createPropertyGridRows(
  definitions: ResourcePropertyDefinition[],
  resourceProperties: Record<string, JsonElement>,
  targets: EffectiveValueTargetResponse[],
): PropertyGridRow[] {
  return definitions
    .map((definition) => {
      const hasResourceValue = Object.prototype.hasOwnProperty.call(resourceProperties, definition.name)
        && resourceProperties[definition.name] !== null;
      const resourceValue = hasResourceValue
        ? { value: resourceProperties[definition.name], provenance: EFFECTIVE_VALUE_PROVENANCE.Resource }
        : definition.default !== undefined && definition.default !== null
          ? { value: definition.default, provenance: EFFECTIVE_VALUE_PROVENANCE.CatalogDefault }
          : { value: null, provenance: EFFECTIVE_VALUE_PROVENANCE.Absent };
      const cells = targets.map((target): PropertyGridCell => {
        const effectiveValue = target.properties.find((property) => property.name === definition.name);
        return effectiveValue
          ? {
              targetId: target.id,
              code: target.code,
              applicable: true,
              value: effectiveValue.value,
              provenance: effectiveValue.provenance,
            }
          : {
              targetId: target.id,
              code: target.code,
              applicable: false,
              value: null,
              provenance: null,
            };
      });

      return { definition, resource: resourceValue, targets: cells };
    })
    .filter((row) => row.targets.some((cell) => cell.applicable));
}

export function canClearOverride(
  definition: ResourcePropertyDefinition,
  cell: PropertyGridCell,
): boolean {
  return definition.overridable
    && cell.applicable
    && cell.provenance === EFFECTIVE_VALUE_PROVENANCE.Override;
}

export function createClearOverrideRequest(
  definition: ResourcePropertyDefinition,
  cell: PropertyGridCell,
): { targetId: string; property: string } | undefined {
  return canClearOverride(definition, cell)
    ? { targetId: cell.targetId, property: definition.name }
    : undefined;
}

export function isPropertyEditable(definition: ResourcePropertyDefinition): boolean {
  const editableKinds: ReadonlySet<string> = new Set([
    RESOURCE_PROPERTY_KIND.Boolean,
    RESOURCE_PROPERTY_KIND.Integer,
    RESOURCE_PROPERTY_KIND.Number,
    RESOURCE_PROPERTY_KIND.Enum,
    RESOURCE_PROPERTY_KIND.String,
    RESOURCE_PROPERTY_KIND.Array,
  ]);
  return editableKinds.has(definition.kind);
}

export function parseResourceTypeProperties(descriptor: JsonElement): ResourcePropertyDefinition[] {
  if (!isJsonObject(descriptor)) {
    return [];
  }

  const properties = descriptor['properties'];
  if (!Array.isArray(properties)) {
    return [];
  }

  return properties.flatMap((property): ResourcePropertyDefinition[] => {
    if (!isJsonObject(property)) {
      return [];
    }

    const name = property['name'];
    const kind = property['kind'];
    const overridable = property['overridable'];
    if (typeof name !== 'string' || typeof kind !== 'string' || typeof overridable !== 'boolean') {
      return [];
    }

    const definition: ResourcePropertyDefinition = { name, kind, overridable };
    const labels = property['labels'];
    if (isJsonObject(labels)) {
      const fr = labels['fr'];
      const en = labels['en'];
      if (typeof fr === 'string' && typeof en === 'string') {
        definition.labels = { fr, en };
      }
    }

    if (Object.prototype.hasOwnProperty.call(property, 'default')) {
      definition.default = property['default'];
    }
    if (Array.isArray(property['values'])) {
      definition.values = property['values'];
    }

    const minimum = property['minimum'];
    if (typeof minimum === 'number') {
      definition.minimum = minimum;
    }
    const maximum = property['maximum'];
    if (typeof maximum === 'number') {
      definition.maximum = maximum;
    }
    const minLength = property['minLength'];
    if (typeof minLength === 'number') {
      definition.minLength = minLength;
    }
    const maxLength = property['maxLength'];
    if (typeof maxLength === 'number') {
      definition.maxLength = maxLength;
    }
    const pattern = property['pattern'];
    if (typeof pattern === 'string') {
      definition.pattern = pattern;
    }
    const lockedAfterPublish = property['lockedAfterPublish'];
    if (typeof lockedAfterPublish === 'boolean') {
      definition.lockedAfterPublish = lockedAfterPublish;
    }
    const condition = property['condition'];
    if (typeof condition === 'string') {
      definition.condition = condition;
    }
    const description = property['description'];
    if (typeof description === 'string') {
      definition.description = description;
    }

    return [definition];
  });
}

function isJsonObject(value: JsonElement): value is { [key: string]: JsonElement } {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
