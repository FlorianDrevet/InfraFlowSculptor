import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Api } from '../api/generated';
import { ActiveOrganizationStore } from '../http/active-organization.store';
import { FEATURES } from './feature-keys';
import { FeatureAvailabilityService } from './feature-availability.service';

describe('FeatureAvailabilityService', () => {
  const invoke = vi.fn();

  beforeEach(() => {
    invoke.mockReset();
    invoke.mockReturnValue(of([
      { key: FEATURES.organizations, status: 'Released', enabled: true },
    ]));
    TestBed.configureTestingModule({
      providers: [
        { provide: Api, useValue: { invoke } },
        {
          provide: ActiveOrganizationStore,
          useValue: { organizationId: signal('org-42') },
        },
      ],
    });
  });

  it('updates reactive feature checks after loading availability', async () => {
    const availability = TestBed.inject(FeatureAvailabilityService);
    const enabled = computed(() => availability.isEnabled(FEATURES.organizations));

    expect(enabled()).toBe(false);

    await availability.ensureLoaded();

    expect(enabled()).toBe(true);
    expect(invoke).toHaveBeenCalledOnce();
  });
});
