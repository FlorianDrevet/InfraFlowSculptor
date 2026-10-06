import {
  ChangeDetectionStrategy,
  Component,
  TemplateRef,
  ViewChild,
  inject,
  input,
} from '@angular/core';
import { Dialog } from '@angular/cdk/dialog';

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

  protected readonly titleId = `ifs-dialog-title-${++nextDialogId}`;
  private readonly dialog = inject(Dialog);

  @ViewChild('dialogTemplate', { static: true })
  private dialogTemplate!: TemplateRef<unknown>;

  protected openDialog(): void {
    this.dialog.open(this.dialogTemplate, {
      ariaLabelledBy: this.titleId,
      ariaModal: true,
      role: 'dialog',
      panelClass: 'ifs-dialog-pane',
      backdropClass: 'ifs-dialog-backdrop',
      maxWidth: 'calc(100vw - 32px)',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
  }
}
