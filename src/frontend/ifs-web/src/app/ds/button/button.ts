import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  isDevMode,
  ViewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { DsIcon, IconName } from '../icon/icon';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'md' | 'sm' | 'lg';
export type ButtonType = 'button' | 'submit' | 'reset';

@Component({
  selector: 'app-ds-button',
  standalone: true,
  imports: [DsIcon, NgTemplateOutlet],
  templateUrl: './button.html',
  styleUrl: './button.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsButton implements AfterViewInit {
  readonly variant = input<ButtonVariant>('secondary');
  readonly size = input<ButtonSize>('md');
  readonly icon = input<IconName | undefined>();
  readonly iconPosition = input<'start' | 'end'>('start');
  readonly fullWidth = input(false);
  readonly href = input<string | undefined>();
  readonly type = input<ButtonType>('button');
  readonly disabled = input(false);
  readonly ariaLabel = input<string | undefined>();

  private readonly host = inject(ElementRef);
  protected readonly accessibleLabel = computed(
    () =>
      this.ariaLabel() ||
      (this.host.nativeElement as HTMLElement).getAttribute('aria-label') ||
      undefined,
  );
  @ViewChild('nativeControl', { read: ElementRef })
  private nativeControl?: ElementRef<HTMLElement>;

  ngAfterViewInit(): void {
    const controlText = this.nativeControl?.nativeElement.textContent;
    const hasVisibleLabel = typeof controlText === 'string' && controlText.trim().length > 0;
    if (isDevMode() && this.icon() && !this.accessibleLabel()?.trim() && !hasVisibleLabel) {
      throw new Error('An icon-only Strata button requires an aria-label.');
    }
  }
}
