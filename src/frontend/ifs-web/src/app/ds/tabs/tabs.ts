import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

export interface TabItem {
  id: string;
  label: string;
  count?: number | string;
  countTone?: 'neutral' | 'ember';
}

@Component({
  selector: 'app-ds-tabs',
  standalone: true,
  templateUrl: './tabs.html',
  styleUrl: './tabs.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsTabs {
  readonly items = input<TabItem[]>([]);
  readonly active = model<string | undefined>(undefined);
  readonly ariaLabel = input<string | undefined>();

  protected selectedId(): string | undefined {
    return this.active() ?? this.items()[0]?.id;
  }

  protected select(id: string): void {
    this.active.set(id);
  }

  protected onKeydown(event: KeyboardEvent, index: number): void {
    const items = this.items();
    if (!items.length || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) return;
    event.preventDefault();
    const direction = event.key === 'ArrowRight' ? 1 : -1;
    const nextIndex = (index + direction + items.length) % items.length;
    const next = items[nextIndex];
    this.select(next.id);
    (event.currentTarget as HTMLElement).parentElement
      ?.querySelectorAll<HTMLButtonElement>('[role="tab"]')
      .item(nextIndex)
      ?.focus();
  }
}
