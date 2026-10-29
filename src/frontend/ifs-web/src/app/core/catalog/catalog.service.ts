import { Injectable, inject } from '@angular/core';
import { Api } from '../api/generated/api';
import { getCatalogType, getCatalogTypes } from '../api/generated';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly api = inject(Api);

  types(version: string) {
    return this.api.invoke(getCatalogTypes, { version });
  }

  type(version: string, type: string) {
    return this.api.invoke(getCatalogType, { version, type });
  }
}
