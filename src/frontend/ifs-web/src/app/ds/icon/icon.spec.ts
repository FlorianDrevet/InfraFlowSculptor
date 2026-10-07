import { inputBinding } from '@angular/core';
import { render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsIcon } from './icon';

describe('app-ds-icon', () => {
  it('is decorative without a label', async () => {
    const decorative = await render(DsIcon, { bindings: [inputBinding('name', () => 'home')] });
    expect(decorative.container.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('is announced as an image when a label is provided', async () => {
    await render(DsIcon, {
      bindings: [inputBinding('name', () => 'home'), inputBinding('label', () => 'Accueil')],
    });
    expect(screen.getByRole('img', { name: 'Accueil' })).toBeDefined();
  });
});
