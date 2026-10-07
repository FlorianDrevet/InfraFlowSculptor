import { render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it } from 'vitest';
import { DsBadge } from './badge';

describe('app-ds-badge', () => {
  it('keeps its status readable as text when tone, dot, and icon are used', async () => {
    await render('<app-ds-badge tone="danger" dot icon="alert" mono>Push échoué</app-ds-badge>', {
      imports: [DsBadge],
    });
    expect(screen.getByText('Push échoué')).toBeDefined();
  });
});
