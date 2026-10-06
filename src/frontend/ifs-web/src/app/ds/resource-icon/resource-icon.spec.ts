import { render, screen } from '@testing-library/angular';
import { describe, expect, it } from 'vitest';
import { DsResourceIcon } from './resource-icon';

describe('app-ds-resource-icon', () => {
  it('renders the official icon with the service label as its default alternative text', async () => {
    const { container } = await render(DsResourceIcon, { inputs: { type: 'KeyVault' } });
    const image = container.querySelector('img');

    expect(image?.getAttribute('src')).toBe('/azure-icons/key-vault.svg');
    expect(image?.getAttribute('alt')).toBe('Key Vault');
    expect(image?.getAttribute('width')).toBe('28');
  });

  it('uses an accessible category tile when the requested type has no official icon', async () => {
    await render(DsResourceIcon, { inputs: { type: 'UnknownResource' } });

    const tile = screen.getByRole('img', { name: 'UnknownResource' });
    expect(tile.textContent?.trim()).toBe('?');
    expect(tile.className).toContain('resource-icon--platform');
  });

  it('keeps the Azure image unchanged in tile mode and supports decorative alternatives', async () => {
    const { container } = await render(DsResourceIcon, {
      inputs: { type: 'KeyVault', variant: 'tile', alt: '' },
    });

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('[aria-hidden="true"]')?.textContent?.trim()).toBe('kv');
  });
});
