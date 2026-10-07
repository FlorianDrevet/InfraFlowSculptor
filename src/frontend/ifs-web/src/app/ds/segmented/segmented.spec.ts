import { inputBinding } from '@angular/core';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsSegmented } from './segmented';

describe('app-ds-segmented', () => {
  it('exposes a named group and updates aria-pressed on selection', async () => {
    await render(DsSegmented, {
      bindings: [
        inputBinding('ariaLabel', () => 'Environnement'),
        inputBinding('options', () => [
          { value: 'dev', label: 'dev' },
          { value: 'prod', label: 'prod' },
        ]),
        inputBinding('value', () => 'dev'),
      ],
    });
    const group = screen.getByRole('group', { name: 'Environnement' });
    fireEvent.click(within(group).getByRole('button', { name: 'prod' }));
    await waitFor(() => {
      expect(within(group).getByRole('button', { name: 'prod' }).getAttribute('aria-pressed')).toBe(
        'true',
      );
    });
  });
});
