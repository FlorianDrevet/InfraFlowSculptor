import { inputBinding } from '@angular/core';
import { fireEvent, render, screen } from '@testing-library/angular/zoneless';
import { describe, expect, it, vi } from 'vitest';
import { DsTextField } from './text-field';

describe('app-ds-text-field', () => {
  it('connects the label, validation state, and message to the input', async () => {
    const { container } = await render(DsTextField, {
      bindings: [
        inputBinding('label', () => 'Jeton'),
        inputBinding('hint', () => 'Valeur locale'),
        inputBinding('error', () => 'Jeton expiré'),
        inputBinding('mono', () => true),
      ],
    });
    const input = screen.getByLabelText('Jeton');
    const message = screen.getByText('Jeton expiré');
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toBe(message.id);
    expect(message.id).toBeTruthy();
    expect(container.querySelector('.st-field-input')?.classList.contains('st-mono')).toBe(true);
  });

  it('reports native input changes through ControlValueAccessor', async () => {
    const { fixture } = await render(DsTextField, {
      bindings: [inputBinding('label', () => 'Nom')],
    });
    const changed = vi.fn();
    fixture.componentInstance.registerOnChange(changed);
    fireEvent.input(screen.getByLabelText('Nom'), { target: { value: 'orders-api' } });
    expect(changed).toHaveBeenCalledWith('orders-api');
  });

  it('uses defaultValue as the initial display value', async () => {
    await render(DsTextField, {
      bindings: [
        inputBinding('label', () => 'Nom'),
        inputBinding('defaultValue', () => 'orders-api'),
      ],
    });
    expect(screen.getByLabelText<HTMLInputElement>('Nom').value).toBe('orders-api');
  });
});
