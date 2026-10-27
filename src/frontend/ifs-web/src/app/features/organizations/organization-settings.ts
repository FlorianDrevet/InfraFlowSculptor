import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { OrganizationsService } from '../../core/organizations/organizations.service';

@Component({
  selector: 'app-organization-settings',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './organization-settings.html',
  styleUrl: './organization-settings.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationSettings implements OnInit {
  readonly id = input.required<string>();
  private readonly organizations = inject(OrganizationsService);
  private readonly router = inject(Router);
  protected readonly name = signal('');
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly hasError = signal(false);

  ngOnInit(): void {
    void this.loadOrganization();
  }

  async save(): Promise<void> {
    const name = this.name().trim();
    if (name.length < 2 || name.length > 80 || this.saving()) {
      this.hasError.set(true);
      return;
    }

    this.saving.set(true);
    this.hasError.set(false);
    try {
      await firstValueFrom(this.organizations.rename(this.id(), name));
    } catch {
      this.hasError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async loadOrganization(): Promise<void> {
    try {
      const organizations = await this.organizations.ensureLoaded();
      const organization = organizations.find(item => item.id === this.id());
      if (!organization) {
        await this.router.navigateByUrl('/not-found');
        return;
      }

      this.name.set(organization.name);
    } catch {
      this.hasError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
