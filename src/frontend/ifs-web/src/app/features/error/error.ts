import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-error-page',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './error.html',
  styleUrl: './error.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorPage {
  protected readonly reference =
    inject(ActivatedRoute).snapshot.queryParamMap.get('reference') ?? '—';
}
