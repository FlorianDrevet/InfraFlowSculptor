import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Shell } from './core/layout/shell';

@Component({
  selector: 'app-root',
  imports: [Shell],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
