import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ComponentResponse } from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';

@Component({
  selector: 'app-project-components',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './project-components.html',
  styleUrl: './project-components.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectComponents implements OnInit {
  readonly projectId = input.required<string>();
  protected readonly components = signal<ComponentResponse[]>([]);
  protected readonly canManage = signal(false);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly projects = inject(ProjectsService);
  private readonly componentService = inject(ComponentsService);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [components, permissions] = await Promise.all([
        firstValueFrom(this.componentService.list(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      this.components.set(components);
      this.canManage.set(permissions.permissions.includes('components.manage'));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
