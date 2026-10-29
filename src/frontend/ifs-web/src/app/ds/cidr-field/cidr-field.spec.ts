import { inputBinding } from '@angular/core';
import { fireEvent, render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it, vi } from 'vitest';
import { DsCidrField } from './cidr-field';

describe('app-ds-cidr-field', () => {
  const bindings = [
    inputBinding('label', () => 'Plages IP autorisées'),
    inputBinding('inputLabel', () => 'Nouvelle plage CIDR'),
    inputBinding('addLabel', () => 'Ajouter une plage'),
    inputBinding('removeLabel', () => 'Supprimer la plage'),
    inputBinding('invalidMessage', () => 'Saisis un CIDR IPv4 valide.'),
  ];

  it('adds a valid IPv4 CIDR through ControlValueAccessor', async () => {
    const { fixture } = await render(DsCidrField, { bindings });
    const changed = vi.fn();
    fixture.componentInstance.registerOnChange(changed);

    fireEvent.input(screen.getByLabelText('Nouvelle plage CIDR'), { target: { value: '10.1.0.0/16' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une plage' }));

    expect(changed).toHaveBeenCalledWith(['10.1.0.0/16']);
  });

  it('keeps malformed CIDRs out of the value and explains the input error', async () => {
    const { fixture } = await render(DsCidrField, { bindings });
    const changed = vi.fn();
    fixture.componentInstance.registerOnChange(changed);

    fireEvent.input(screen.getByLabelText('Nouvelle plage CIDR'), { target: { value: '256.1.0.0/16' } });
    fixture.detectChanges();

    expect(screen.getByText('Saisis un CIDR IPv4 valide.')).toBeTruthy();
    expect(screen.getByLabelText('Nouvelle plage CIDR').getAttribute('aria-invalid')).toBe('true');
    expect(screen.getByRole('button', { name: 'Ajouter une plage' }).hasAttribute('disabled')).toBe(true);
    expect(changed).not.toHaveBeenCalled();
  });

  it('removes a range with an ordinal accessible name', async () => {
    const { fixture } = await render(DsCidrField, { bindings });
    const changed = vi.fn();
    fixture.componentInstance.registerOnChange(changed);
    fixture.componentInstance.writeValue(['10.0.0.0/8', '192.168.0.0/16']);
    fixture.detectChanges();

    fireEvent.click(screen.getByRole('button', { name: 'Supprimer la plage 1' }));

    expect(changed).toHaveBeenCalledWith(['192.168.0.0/16']);
  });

  it('stops accepting entries at the configured limit', async () => {
    const { fixture } = await render(DsCidrField, {
      bindings: [...bindings, inputBinding('maxItems', () => 1)],
    });
    fireEvent.input(screen.getByLabelText('Nouvelle plage CIDR'), { target: { value: '10.0.0.0/8' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une plage' }));

    expect(screen.getByRole('button', { name: 'Ajouter une plage' }).hasAttribute('disabled')).toBe(true);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
