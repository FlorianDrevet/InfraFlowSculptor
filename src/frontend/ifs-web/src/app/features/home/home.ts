import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

interface CurrentUser {
  displayName: string;
}

@Component({
  selector: 'app-home',
  imports: [TranslocoPipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  protected readonly currentUser = httpResource<CurrentUser>(() => '/v1/me');
}
