import { describe, expect, it } from 'vitest';
import { PendingNavigationStore } from './pending-navigation.store';

describe('PendingNavigationStore', () => {
  it('restores the requested local route once after authentication', () => {
    const store = new PendingNavigationStore();

    store.save('/invitations/a-token?source=email');

    expect(store.consume()).toBe('/invitations/a-token?source=email');
    expect(store.consume()).toBeNull();
  });

  it('does not store an external redirect', () => {
    const store = new PendingNavigationStore();

    store.save('//example.com/phishing');

    expect(store.consume()).toBeNull();
  });
});
