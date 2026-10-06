import { fireEvent, render, screen, waitFor } from '@testing-library/angular';
import { describe, expect, it } from 'vitest';
import { DsDialog } from './dialog';

describe('app-ds-dialog', () => {
  it('focuses dialog content, closes on Escape, and restores focus to its trigger', async () => {
    await render(DsDialog, {
      inputs: { title: 'Détails de la ressource', triggerLabel: 'Voir les détails' },
    });

    const trigger = screen.getByRole('button', { name: 'Voir les détails' });
    trigger.focus();
    fireEvent.click(trigger);

    const dialog = await screen.findByRole('dialog', { name: 'Détails de la ressource' });
    screen.getByRole('button', { name: 'Fermer' });
    await waitFor(() => expect(dialog.contains(document.activeElement)).toBe(true));
    expect(dialog.querySelector('h2')?.textContent).toBe('Détails de la ressource');

    fireEvent.keyDown(dialog, { key: 'Escape', code: 'Escape', keyCode: 27 });
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(document.activeElement).toBe(trigger);
  });
});
