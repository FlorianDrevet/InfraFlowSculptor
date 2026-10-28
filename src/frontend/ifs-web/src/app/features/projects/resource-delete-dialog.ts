import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  input,
  output,
  ViewChild,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  RemovedResourceBehavior,
  ResourceDeletionImpactResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { DsButton } from '../../ds/button/button';

@Component({
  selector: 'app-resource-delete-dialog',
  imports: [CdkTrapFocus, DsButton, TranslocoPipe],
  templateUrl: './resource-delete-dialog.html',
  styleUrl: './resource-delete-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceDeleteDialog {
  @ViewChild('confirmationInput') private confirmationInput?: ElementRef<HTMLInputElement>;

  readonly resource = input.required<ResourceResponse>();
  readonly removedBehavior = input.required<RemovedResourceBehavior>();
  readonly impact = input<ResourceDeletionImpactResponse | null>(null);
  readonly loading = input(false);
  readonly saving = input(false);
  readonly error = input(false);
  readonly conflict = input(false);
  readonly confirmation = input('');
  readonly confirmationChange = output<string>();
  readonly dismiss = output<void>();
  readonly confirm = output<void>();

  protected readonly canConfirm = computed(
    () => this.confirmation() === this.resource().logicalName,
  );
  protected readonly removalExplanation = computed(() =>
    this.removedBehavior() === 'Delete'
      ? 'resource.deleteRemovesAzureResource'
      : 'resource.deleteDetachesAzureResource',
  );

  protected dismissOnEscape(): void {
    if (!this.saving()) this.dismiss.emit();
  }

  protected confirmDeletion(): void {
    if (!this.canConfirm() || this.loading() || this.saving() || !this.impact()) return;
    this.confirmationInput?.nativeElement.focus();
    this.confirm.emit();
  }

  protected areaLabel(area: string): string {
    switch (area) {
      case 'IncomingLinks':
        return 'resource.pendingIncomingLinks';
      case 'ApplicationParameters':
        return 'resource.pendingApplicationParameters';
      case 'ImplicitEffects':
        return 'resource.pendingImplicitEffects';
      default:
        return 'resource.pendingAnalysis';
    }
  }
}
