import type { JsonElement } from '../../core/api/generated/models/json-element';

export interface PropertyGridUpdate {
  property: string;
  targetId: string | null;
  value: JsonElement;
}
