import { inputBinding } from '@angular/core';
import { render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsPanel } from './panel';

describe('app-ds-panel', () => {
  it('uses the requested heading level for its title', async () => {
    await render(DsPanel, {
      bindings: [inputBinding('title', () => 'Identité & accès'), inputBinding('level', () => 3)],
    });
    expect(screen.getByRole('heading', { level: 3, name: 'Identité & accès' })).toBeDefined();
  });
});
