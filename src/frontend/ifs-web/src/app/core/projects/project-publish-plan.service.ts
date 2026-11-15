import { Injectable, inject } from '@angular/core';
import { Api, getProjectPublishPlan, setProjectPublishPlan } from '../api/generated';
import type { SetProjectPublishPlanRequest } from '../api/generated';

@Injectable({ providedIn: 'root' })
export class ProjectPublishPlanService {
  private readonly api = inject(Api);

  get(projectId: string) {
    return this.api.invoke(getProjectPublishPlan, { id: projectId });
  }

  save(projectId: string, body: SetProjectPublishPlanRequest) {
    return this.api.invoke(setProjectPublishPlan, { id: projectId, body });
  }
}
