import { Injectable, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { DashboardWidgetDef } from '../models/dashboard-widget.model';

/** Filters a widget catalog down to what the signed-in user may see —
 * reuses AuthService.hasPermission()/roleName() exactly as every route
 * guard already does, rather than a parallel authorization path. Step 6
 * is the first real consumer; kept here now so the contract and its
 * filtering rule land as one unit. */
@Injectable({ providedIn: 'root' })
export class DashboardWidgetRegistryService {
  private readonly auth = inject(AuthService);

  filterVisible(defs: DashboardWidgetDef[]): DashboardWidgetDef[] {
    return defs.filter((d) => {
      if (d.requiredPermission && !this.auth.hasPermission(d.requiredPermission)) return false;
      if (d.roles && d.roles.length > 0 && !d.roles.includes(this.auth.roleName() ?? '')) return false;
      return true;
    });
  }
}
