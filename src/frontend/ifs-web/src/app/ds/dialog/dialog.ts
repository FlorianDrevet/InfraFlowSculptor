import {
  ChangeDetectionStrategy,
  Component,
  TemplateRef,
  ViewChild,
  inject,
  input,
} from '@angular/core';
import { Dialog } from '@angular/cdk/dialog';
import { Overlay } from '@angular/cdk/overlay';

let nextDialogId = 0;

@Component({
  selector: 'app-ds-dialog',
  standalone: true,
  templateUrl: './dialog.html',
  styleUrl: './dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsDialog {
  readonly title = input.required<string>();
  readonly triggerLabel = input('Ouvrir le dialogue');
  readonly closeLabel = input('Fermer');
  readonly drawer = input(false);

  protected readonly titleId = `ifs-dialog-title-${++nextDialogId}`;
  private readonly dialog = inject(Dialog);
  private readonly overlay = inject(Overlay);

  @ViewChild('dialogTemplate', { static: true })
  private dialogTemplate!: TemplateRef<unknown>;

  protected openDialog(): void {
    this.dialog.open(this.dialogTemplate, {
      ariaLabelledBy: this.titleId,
      ariaModal: true,
      role: 'dialog',
      panelClass: this.drawer() ? ['ifs-dialog-pane', 'ifs-dialog-drawer'] : 'ifs-dialog-pane',
      backdropClass: 'ifs-dialog-backdrop',
      maxWidth: 'calc(100vw - 32px)',
      positionStrategy: this.drawer()
        ? this.overlay.position().global().top('0').bottom('0').right('0')
        : undefined,
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
  }
}
