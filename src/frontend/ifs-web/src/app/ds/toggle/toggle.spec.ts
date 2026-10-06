import { inputBinding } from '@angular/core';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsToggle } from './toggle';

describe('app-ds-toggle', () => {
  it('exposes switch state and toggles with its native button interaction', async () => {
    await render(DsToggle, { bindings: [inputBinding('label', () => 'Always On')] });
    const toggle = screen.getByRole('switch', { name: 'Always On' });
    expect(toggle.getAttribute('aria-checked')).toBe('false');
    fireEvent.click(toggle);
    await waitFor(() => expect(toggle.getAttribute('aria-checked')).toBe('true'));
  });
});
