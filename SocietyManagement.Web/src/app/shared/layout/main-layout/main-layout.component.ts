import { CommonModule } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { interval } from 'rxjs';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../../core/services/auth.service';
import { CurrentSocietyService } from '../../../core/services/current-society.service';
import { LoadingService } from '../../../core/services/loading.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { ThemeService } from '../../../core/services/theme.service';
import { AssetUrlPipe } from '../../pipes/asset-url.pipe';
import { SocietyServiceService } from '../../../features/services/services/society-service.service';

const EXPIRING_SERVICES_POLL_MS = 5 * 60 * 1000;

interface NavItem {
  label: string;
  icon: string;
  link: string;
  adminOnly?: boolean;
  superAdminOnly?: boolean;
  hideForWatchman?: boolean;
  group?: string;
}

type NavNode = { type: 'item'; item: NavItem } | { type: 'group'; name: string; icon: string; items: NavItem[] };

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', link: '/dashboard' },
  { label: 'Societies', icon: 'domain', link: '/societies', superAdminOnly: true },
  { label: 'Festivals & Events', icon: 'celebration', link: '/festivals', hideForWatchman: true },
  { label: 'Visitors', icon: 'badge', link: '/visitors' },
  { label: 'Maintenance', icon: 'receipt_long', link: '/maintenance', adminOnly: true },
  { label: 'Residents', icon: 'people', link: '/residents', adminOnly: true },
  { label: 'Staff', icon: 'engineering', link: '/staff', adminOnly: true },
  { label: 'Services', icon: 'build', link: '/services', adminOnly: true },
  { label: 'Finance', icon: 'account_balance', link: '/finance', adminOnly: true },
  { label: 'Complaints', icon: 'report_problem', link: '/complaints', adminOnly: true },
  { label: 'Committee', icon: 'groups', link: '/committee', hideForWatchman: true },
  { label: 'My Bills', icon: 'payments', link: '/my-bills', group: 'My Society', hideForWatchman: true },
  { label: 'My Water Tanker', icon: 'water_drop', link: '/my-water-tanker', group: 'My Society', hideForWatchman: true },
  { label: 'My Complaints', icon: 'report_problem', link: '/my-complaints', group: 'My Society', hideForWatchman: true },
  { label: 'My Family', icon: 'family_restroom', link: '/my-family', group: 'My Society', hideForWatchman: true },
  { label: 'Society Setup', icon: 'apartment', link: '/society-setup', adminOnly: true },
  { label: 'Users', icon: 'group', link: '/users', adminOnly: true },
  { label: 'Roles & Permissions', icon: 'admin_panel_settings', link: '/roles', adminOnly: true }
];

const GROUP_ICONS: Record<string, string> = {
  'My Society': 'apartment'
};

/** App shell — collapsible sidebar nav + topbar (search/theme-toggle/user
 * menu) + router-outlet content area. Mirrors the "modern admin dashboard"
 * look requested in the spec. */
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule,
    MatListModule, MatIconModule, MatButtonModule, MatMenuModule, MatDividerModule,
    MatProgressBarModule, MatTooltipModule, MatBadgeModule, AssetUrlPipe
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  readonly loading = inject(LoadingService);
  readonly currentSociety = inject(CurrentSocietyService);
  // Injected (not otherwise referenced) so the SignalR connection lifecycle —
  // driven by its internal `effect()` on auth.isAuthenticated() — starts as
  // soon as the authenticated app shell loads.
  private readonly signalr = inject(SignalrService);
  private readonly router = inject(Router);
  private readonly servicesApi = inject(SocietyServiceService);

  // Topbar notification bell — a live count, not a persisted inbox (see
  // SocietyServiceFeature.cs's GetExpiringServicesQuery comment for why).
  readonly expiringServicesCount = signal(0);

  // Desktop: sidenav is always present, this only toggles icon-only vs full
  // width. Mobile: sidenav is an overlay drawer, closed by default, and this
  // is the only thing that controls whether it's on screen at all — two
  // different behaviors behind the same hamburger button.
  readonly desktopExpanded = signal(true);
  readonly mobileOpen = signal(false);
  readonly isHandset = signal(false);
  readonly showLabels = computed(() => this.isHandset() || this.desktopExpanded());
  readonly navItems = NAV_ITEMS;

  // mat-sidenav-content's left offset is normally auto-managed by Material
  // via a `[style.margin-inline-start.px]` binding recomputed from the
  // sidenav's rendered width — but that recalculation doesn't reliably
  // re-fire for a pure CSS width transition (toggling .sidebar--collapsed),
  // so Material keeps re-applying the stale pre-toggle value on every
  // change-detection cycle, stranding a blank gap next to the now-narrower
  // sidebar. Setting margin-left here doesn't help on its own — logical and
  // physical properties for the same edge share one cascade slot, and
  // Material's own binding (non-important) still wins whenever its CD cycle
  // runs after this effect's. Using `!important` via setProperty beats it
  // regardless of ordering. Only touched on desktop ("side" mode) — mobile's
  // overlay drawer positions itself and must be left alone.
  private readonly sidenavContent = viewChild('sidenavContent', { read: ElementRef<HTMLElement> });

  constructor() {
    inject(BreakpointObserver)
      .observe('(max-width: 768px)')
      .pipe(takeUntilDestroyed())
      .subscribe((result) => this.isHandset.set(result.matches));

    effect(() => {
      const el = this.sidenavContent()?.nativeElement;
      if (!el || this.isHandset()) return;
      const width = this.desktopExpanded() ? '280px' : '72px';
      el.style.setProperty('margin-left', width, 'important');
      el.style.setProperty('margin-inline-start', width, 'important');
    });

    effect(() => {
      if (this.currentSociety.society()?.id && this.auth.isAdmin()) this.loadExpiringServicesCount();
    });
    interval(EXPIRING_SERVICES_POLL_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.loadExpiringServicesCount());
  }

  private loadExpiringServicesCount(): void {
    const societyId = this.currentSociety.society()?.id;
    if (!societyId || !this.auth.isAdmin()) return;
    this.servicesApi.getExpiring(societyId).subscribe((services) => this.expiringServicesCount.set(services.length));
  }

  openExpiringServices(): void {
    this.router.navigate(['/services']);
  }

  toggleSidenav(): void {
    if (this.isHandset()) {
      this.mobileOpen.update((v) => !v);
    } else {
      this.desktopExpanded.update((v) => !v);
    }
  }

  // Bound to the sidenav's (closed) event, which fires both on backdrop
  // click and on Escape — keeps mobileOpen in sync so the hamburger's next
  // click reopens it correctly instead of being one toggle out of phase.
  onSidenavClosed(): void {
    this.mobileOpen.set(false);
  }

  onNavItemClick(): void {
    if (this.isHandset()) {
      this.mobileOpen.set(false);
    }
  }

  // Groups default open so nothing currently reachable becomes hidden
  // just by introducing the grouping — collapsing is opt-in per visit.
  readonly expandedGroups = signal<Set<string>>(new Set(Object.keys(GROUP_ICONS)));

  visibleNavItems(): NavItem[] {
    return this.navItems.filter((item) =>
      (!item.adminOnly || this.auth.isAdmin()) &&
      (!item.superAdminOnly || this.auth.isSuperAdmin()) &&
      (!item.hideForWatchman || !this.auth.isWatchman()));
  }

  // Buckets consecutive same-`group` items into one node so the template
  // can render a single clickable expand/collapse header per group,
  // instead of one label per boundary-crossing (the old isNewGroup approach).
  navNodes(): NavNode[] {
    const nodes: NavNode[] = [];
    for (const item of this.visibleNavItems()) {
      if (!item.group) {
        nodes.push({ type: 'item', item });
        continue;
      }
      const last = nodes[nodes.length - 1];
      if (last?.type === 'group' && last.name === item.group) {
        last.items.push(item);
      } else {
        nodes.push({ type: 'group', name: item.group, icon: GROUP_ICONS[item.group] ?? 'folder', items: [item] });
      }
    }
    return nodes;
  }

  isGroupExpanded(name: string): boolean {
    return this.expandedGroups().has(name);
  }

  toggleGroup(name: string): void {
    this.expandedGroups.update((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  logout(): void {
    this.auth.logout();
  }
}
