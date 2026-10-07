import { inputBinding } from '@angular/core';
import { render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsButton } from './button';

describe('app-ds-button', () => {
  it('renders a real link when href is set', async () => {
    const { container } = await render(DsButton, {
      bindings: [
        inputBinding('href', () => '/projects'),
        inputBinding('variant', () => 'secondary'),
      ],
    });
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe('/projects');
    expect(container.querySelector('button')).toBeNull();
  });

  it('uses a native button and preserves its requested type', async () => {
    const { container } = await render(DsButton, {
      bindings: [inputBinding('type', () => 'submit')],
    });
    expect(container.querySelector('button')?.type).toBe('submit');
  });

  it('stretches the control to the available width when requested', async () => {
    await render(DsButton, {
      bindings: [inputBinding('fullWidth', () => true)],
    });

    expect(screen.getByRole('button').classList.contains('st-btn-full-width')).toBe(true);
  });

  it('requires an accessible name for an icon-only action', async () => {
    await expect(
      render(DsButton, { bindings: [inputBinding('icon', () => 'plus')] }),
    ).rejects.toThrow(/aria-label/i);
  });

  it('accepts an aria label for an icon-only action', async () => {
    await render('<app-ds-button icon="plus" aria-label="Ajouter"></app-ds-button>', {
      imports: [DsButton],
    });
    expect(screen.getByRole('button', { name: 'Ajouter' })).toBeDefined();
  });
});
