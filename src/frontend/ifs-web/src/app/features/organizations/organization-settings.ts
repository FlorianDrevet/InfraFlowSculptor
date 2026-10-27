import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
  protected readonly name = signal('');
  protected readonly allowedEmailDomains = signal('');
  protected readonly allowedTenantIds = signal('');
  protected readonly loading = signal(true);
  protected readonly loadSucceeded = signal(false);
  protected readonly saving = signal(false);
  protected readonly hasError = signal(false);

  ngOnInit(): void {
    void this.loadOrganization();
  }

  async save(): Promise<void> {
    const name = this.name().trim();
    if (!this.loadSucceeded() || name.length < 2 || name.length > 80 || this.saving()) {
      this.hasError.set(true);
      return;
    }

    this.saving.set(true);
    this.hasError.set(false);
    try {
      await firstValueFrom(this.organizations.rename(this.id(), name));
      await firstValueFrom(this.organizations.updateAccessRestrictions(
        this.id(),
        this.parseList(this.allowedEmailDomains()),
        this.parseList(this.allowedTenantIds()),
      ));
    } catch {
      this.hasError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async loadOrganization(): Promise<void> {
    try {
      const organization = await firstValueFrom(this.organizations.get(this.id()));
      this.name.set(organization.name);
      this.allowedEmailDomains.set(organization.allowedEmailDomains.join('\n'));
      this.allowedTenantIds.set(organization.allowedTenantIds.join('\n'));
      this.loadSucceeded.set(true);
    } catch {
      this.hasError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private parseList(value: string): string[] {
    return value.split(/[\r\n,]+/).map(item => item.trim()).filter(Boolean);
  }
}
