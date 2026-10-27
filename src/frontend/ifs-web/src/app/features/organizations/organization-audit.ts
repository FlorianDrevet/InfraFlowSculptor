import { ChangeDetectionStrategy, Component, inject, input, OnChanges, OnInit, signal, SimpleChanges } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  AuditEventPageResponse,
  AuditEventResponse,
  AuditPropertyChangeResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { DsButton } from '../../ds/button/button';
import { DsSelect } from '../../ds/select/select';

type AuditPeriod = 'today' | 7 | 30 | 90;

const PAGE_SIZE = 50;

const actionKeys: Readonly<Record<string, string>> = {
  'organization.created': 'audit.actions.organizationCreated',
  'organization.renamed': 'audit.actions.organizationRenamed',
  'organization.member.roles_changed': 'audit.actions.memberRolesChanged',
  'organization.member.removed': 'audit.actions.memberRemoved',
  'organization.member.left': 'audit.actions.memberLeft',
  'organization.access_restrictions.updated': 'audit.actions.accessRestrictionsUpdated',
  'organization.invitation.sent': 'audit.actions.invitationSent',
  'organization.invitation.resent': 'audit.actions.invitationResent',
  'organization.invitation.revoked': 'audit.actions.invitationRevoked',
  'organization.invitation.accepted': 'audit.actions.invitationAccepted',
};

const fieldKeys: Readonly<Record<string, string>> = {
  name: 'audit.fields.name',
  roleValues: 'audit.fields.roles',
  allowedEmailDomains: 'audit.fields.allowedEmailDomains',
  allowedTenantIds: 'audit.fields.allowedTenantIds',
  status: 'audit.fields.status',
  email: 'audit.fields.email',
};

const objectTypeKeys: Readonly<Record<string, string>> = {
  Organization: 'audit.objects.organization',
  OrganizationMember: 'audit.objects.member',
  Invitation: 'audit.objects.invitation',
};

const viaKeys: Readonly<Record<string, string>> = {
  Screen: 'audit.via.screen',
  Api: 'audit.via.api',
  Mcp: 'audit.via.mcp',
  Support: 'audit.via.support',
};

@Component({
  selector: 'app-organization-audit',
  imports: [DsButton, DsSelect, TranslocoPipe],
  templateUrl: './organization-audit.html',
  styleUrl: './organization-audit.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationAudit implements OnChanges, OnInit {
  readonly id = input.required<string>();
  private readonly organizations = inject(OrganizationsService);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);
  protected readonly period = signal<AuditPeriod>(30);
  protected readonly page = signal(1);
  protected readonly audit = signal<AuditEventPageResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly permissionDenied = signal(false);
  protected readonly pageCount = signal(0);
  private requestVersion = 0;

  ngOnInit(): void {
    void this.loadPage();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['id'] && !changes['id'].firstChange) {
      this.page.set(1);
      void this.loadPage();
    }
  }

  protected async selectPeriod(value: string): Promise<void> {
    const period = value === 'today' ? 'today' : Number(value);
    if (period !== 'today' && period !== 7 && period !== 30 && period !== 90) {
      return;
    }

    this.period.set(period);
    this.page.set(1);
    await this.loadPage();
  }

  protected async previousPage(): Promise<void> {
    if (this.page() <= 1 || this.loading()) {
      return;
    }

    this.page.update(page => page - 1);
    await this.loadPage();
  }

  protected async nextPage(): Promise<void> {
    if (this.page() >= this.pageCount() || this.loading()) {
      return;
    }

    this.page.update(page => page + 1);
    await this.loadPage();
  }

  protected async retry(): Promise<void> {
    await this.loadPage();
  }

  protected actionKey(action: string): string {
    return actionKeys[action] ?? action;
  }

  protected fieldKey(field: string): string {
    return fieldKeys[field] ?? field;
  }

  protected objectTypeKey(objectType: string): string {
    return objectTypeKeys[objectType] ?? objectType;
  }

  protected organizationContext(): string {
    return this.organizations.organizations().find(organization => organization.id === this.id())?.name
      ?? this.transloco.translate('audit.objects.organization');
  }

  protected viaKey(via: string): string {
    return viaKeys[via] ?? via;
  }

  protected formatDate(value: string): string {
    const locale = this.language.activeLanguage() === 'fr' ? 'fr-FR' : 'en-GB';
    return new Intl.DateTimeFormat(locale, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      timeZone: 'UTC',
    }).format(new Date(value));
  }

  protected describeChange(change: AuditPropertyChangeResponse): string {
    const before = this.formatValue(change, change.before);
    const after = this.formatValue(change, change.after);
    return `${before} → ${after}`;
  }

  protected eventObject(event: AuditEventResponse): string {
    const objectType = this.transloco.translate(this.objectTypeKey(event.objectType));
    return `${objectType} · ${event.objectId}`;
  }

  private async loadPage(): Promise<void> {
    const requestVersion = ++this.requestVersion;
    const organizationId = this.id();
    this.loading.set(true);
    this.loadError.set(false);
    this.permissionDenied.set(false);
    const through = new Date();
    const from = new Date(through);
    const period = this.period();
    if (period === 'today') {
      from.setUTCHours(0, 0, 0, 0);
    } else {
      from.setTime(through.getTime() - period * 24 * 60 * 60 * 1000);
    }

    try {
      const result = await firstValueFrom(
        this.organizations.listAuditEvents(organizationId, {
          from: from.toISOString(),
          to: through.toISOString(),
          page: this.page(),
          pageSize: PAGE_SIZE,
        }),
      );
      if (requestVersion !== this.requestVersion) {
        return;
      }
      this.audit.set(result);
      this.pageCount.set(Math.max(1, Math.ceil(Number(result.totalCount) / Number(result.pageSize))));
    } catch (error) {
      if (requestVersion !== this.requestVersion) {
        return;
      }
      if (error instanceof ApiError && error.status === 403) {
        this.permissionDenied.set(true);
      } else {
        this.loadError.set(true);
      }
    } finally {
      if (requestVersion === this.requestVersion) {
        this.loading.set(false);
      }
    }
  }

  private formatValue(change: AuditPropertyChangeResponse, value: string | null): string {
    if (value === null) {
      return change.isPersonalData ? this.transloco.translate('audit.personalDataErased') : '—';
    }

    return value;
  }
}
