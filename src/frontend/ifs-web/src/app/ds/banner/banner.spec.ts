import { inputBinding } from '@angular/core';
import { render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsBanner } from './banner';

describe('app-ds-banner', () => {
  it('uses alert for danger', async () => {
    await render(DsBanner, {
      bindings: [inputBinding('tone', () => 'danger'), inputBinding('title', () => 'Échec')],
    });
    expect(screen.getByRole('alert').textContent).toContain('Échec');
  });

  it('uses status for success messages', async () => {
    await render(DsBanner, {
      bindings: [inputBinding('tone', () => 'success'), inputBinding('title', () => 'Prêt')],
    });
    expect(screen.getByRole('status').textContent).toContain('Prêt');
  });
});
