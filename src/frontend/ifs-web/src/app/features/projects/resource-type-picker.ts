import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  CatalogResourceTypeSummaryResponse,
  ComponentResponse,
} from '../../core/api/generated';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsResourceIcon } from '../../ds/resource-icon/resource-icon';

@Component({
  selector: 'app-resource-type-picker',
  imports: [CdkTrapFocus, DsResourceIcon, FormsModule, RouterLink, TranslocoPipe],
  templateUrl: './resource-type-picker.html',
  styleUrl: './resource-type-picker.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceTypePicker implements OnInit {
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  readonly existing = input<string | boolean>('');

  protected readonly types = signal<CatalogResourceTypeSummaryResponse[]>([]);
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly search = signal('');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly existingMode = computed(
    () => this.existing() === true || this.existing() === 'true',
  );
  protected readonly filteredTypes = computed(() => {
    const search = this.search().trim().toLocaleLowerCase();
    const types = this.types();
    return search
      ? types.filter((type) =>
          `${type.labelFr} ${type.type} ${type.category}`.toLocaleLowerCase().includes(search),
        )
      : types;
  });
  protected readonly groupedTypes = computed(() => {
    const groups = new Map<string, CatalogResourceTypeSummaryResponse[]>();
    for (const type of this.filteredTypes()) {
      const items = groups.get(type.category) ?? [];
      items.push(type);
      groups.set(type.category, items);
    }
    return [...groups.entries()].map(([category, types]) => ({ category, types }));
  });

  private readonly projects = inject(ProjectsService);
  private readonly components = inject(ComponentsService);
  private readonly catalog = inject(CatalogService);
  private readonly transloco = inject(TranslocoService);

  ngOnInit(): void {
    void this.load();
  }

  protected updateSearch(value: string): void {
    this.search.set(value);
  }

  protected categoryLabel(category: string): string {
    switch (category) {
      case 'compute':
        return this.transloco.translate('resource.categories.compute');
      case 'data':
        return this.transloco.translate('resource.categories.data');
      case 'observability':
        return this.transloco.translate('resource.categories.observability');
      case 'platform':
        return this.transloco.translate('resource.categories.platform');
      case 'security':
        return this.transloco.translate('resource.categories.security');
      default:
        return category;
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, component] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.components.get(this.projectId(), this.componentId())),
      ]);
      this.component.set(component);
      this.types.set(await firstValueFrom(this.catalog.types(project.catalogVersion)));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
