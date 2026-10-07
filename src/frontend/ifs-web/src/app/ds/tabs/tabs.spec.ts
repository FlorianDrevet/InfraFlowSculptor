import { inputBinding } from '@angular/core';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsTabs } from './tabs';

describe('app-ds-tabs', () => {
  it('selects a tab with the arrow keys and renders its count', async () => {
    await render(DsTabs, {
      bindings: [
        inputBinding('ariaLabel', () => 'Sections'),
        inputBinding('items', () => [
          { id: 'resources', label: 'Ressources', count: 10 },
          { id: 'diagnostics', label: 'Diagnostics', count: 3, countTone: 'ember' },
        ]),
        inputBinding('active', () => 'resources'),
      ],
    });
    const tabs = screen.getByRole('tablist', { name: 'Sections' });
    const first = within(tabs).getByRole('tab', { name: 'Ressources 10' });
    fireEvent.keyDown(first, { key: 'ArrowRight' });
    await waitFor(() => {
      expect(
        within(tabs).getByRole('tab', { name: 'Diagnostics 3' }).getAttribute('aria-selected'),
      ).toBe('true');
    });
    expect(within(tabs).getByText('3').classList.contains('st-tab-count-ember')).toBe(true);
  });
});
