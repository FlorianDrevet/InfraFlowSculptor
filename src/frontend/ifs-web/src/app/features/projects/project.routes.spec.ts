import { describe, expect, it } from 'vitest';
import { COMPONENT_ROUTES } from './components.routes';
import { PROJECT_ROUTES } from './project.routes';

describe('project feature routes', () => {
  it('keeps project pages lazy and routes components through a nested feature', () => {
    expect(PROJECT_ROUTES.map((route) => route.path)).toEqual([
      '',
      'environments',
      'environments/:environmentId',
      'components',
      'settings',
      'naming',
      'members',
    ]);
    expect(PROJECT_ROUTES.every((route) => route.loadComponent || route.loadChildren)).toBe(true);
    expect(PROJECT_ROUTES.find((route) => route.path === 'components')?.loadChildren).toBeDefined();
  });

  it('binds the new component marker and existing component IDs through settings routes', () => {
    expect(COMPONENT_ROUTES.map((route) => route.path)).toEqual([
      '',
      ':componentId/settings',
      ':componentId',
    ]);
    expect(COMPONENT_ROUTES.every((route) => route.loadComponent)).toBe(true);
  });
});
