import { ChangeDetectionStrategy, Component } from '@angular/core';
import { render, screen } from '@testing-library/angular';
import { describe, expect, it } from 'vitest';
import { DsTable } from './table';

@Component({
  standalone: true,
  imports: [DsTable],
  template: `<app-ds-table ariaLabel="Ressources du projet">
    <thead>
      <tr>
        <th scope="col">Ressource</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td>orders-api</td>
      </tr>
    </tbody>
  </app-ds-table>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class DsTableTestHost {}

describe('app-ds-table', () => {
  it('keeps the minimum table width inside its own scroll region at a 390px viewport', async () => {
    const originalWidth = window.innerWidth;
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 390 });

    try {
      await render(DsTableTestHost);
      const region = screen.getByRole('region', { name: 'Ressources du projet' });
      const table = region.querySelector('table');
      const header = region.querySelector('th');

      expect(table).not.toBeNull();
      expect(getComputedStyle(region).overflowX).toBe('auto');
      expect(table && getComputedStyle(table).minWidth).toBe('640px');
      expect(header && getComputedStyle(header).textTransform).toBe('uppercase');
      expect(document.documentElement.scrollWidth).toBeLessThanOrEqual(window.innerWidth);
    } finally {
      Object.defineProperty(window, 'innerWidth', { configurable: true, value: originalWidth });
    }
  });
});
