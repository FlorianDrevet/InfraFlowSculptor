import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { OrganizationsService } from '../../core/organizations/organizations.service';

@Component({
  selector: 'app-organization-create',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './organization-create.html',
  styleUrl: './organization-create.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationCreate {
  private readonly organizations = inject(OrganizationsService);
  private readonly router = inject(Router);
  protected readonly name = signal('');
  protected readonly saving = signal(false);
  protected readonly hasError = signal(false);

  async create(): Promise<void> {
    const name = this.name().trim();
    if (name.length < 2 || name.length > 80 || this.saving()) {
      this.hasError.set(true);
      return;
    }

    this.saving.set(true);
    this.hasError.set(false);
    try {
      await firstValueFrom(this.organizations.create(name));
      await this.router.navigateByUrl('/');
    } catch {
      this.hasError.set(true);
    } finally {
      this.saving.set(false);
    }
  }
}
