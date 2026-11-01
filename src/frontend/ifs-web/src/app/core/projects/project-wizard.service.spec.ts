import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { Api, getCatalogRegions, getCatalogVersions, getMe } from '../api/generated';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ProjectWizardService } from './project-wizard.service';

describe('ProjectWizardService', () => {
  const invoke = vi.fn();

  beforeEach(() => {
    invoke.mockReset();
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: { invoke } }] });
  });

  it('selects the highest published patch when catalog versions share a publication date', async () => {
    invoke
      .mockReturnValueOnce(of({ tenantId: 'tenant-1' }))
      .mockReturnValueOnce(
        of([
          { version: '2026.10-pilot', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.1', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.3', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.4', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.5', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.11', status: 'draft', publishedAt: '2026-10-30' },
        ]),
      )
      .mockReturnValueOnce(of([{ name: 'francecentral', code: 'fc' }]));

    const service = TestBed.inject(ProjectWizardService);
    const metadata = await firstValueFrom(service.loadMetadata());

    expect(metadata.catalogVersion).toBe('2026.10-pilot.5');
    expect(invoke).toHaveBeenNthCalledWith(3, getCatalogRegions, { version: '2026.10-pilot.5' });
    expect(invoke).toHaveBeenNthCalledWith(2, getCatalogVersions, {});
    expect(invoke).toHaveBeenNthCalledWith(1, getMe, {});
  });

  it('prefers publication date before patch number', async () => {
    invoke
      .mockReturnValueOnce(of({ tenantId: 'tenant-1' }))
      .mockReturnValueOnce(
        of([
          { version: '2026.10-pilot.3', status: 'published', publishedAt: '2026-10-29' },
          { version: '2026.10-pilot.1', status: 'published', publishedAt: '2026-10-30' },
        ]),
      )
      .mockReturnValueOnce(of([]));

    const service = TestBed.inject(ProjectWizardService);
    const metadata = await firstValueFrom(service.loadMetadata());

    expect(metadata.catalogVersion).toBe('2026.10-pilot.1');
  });
});
