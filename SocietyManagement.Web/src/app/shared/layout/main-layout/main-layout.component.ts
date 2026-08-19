import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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
import { LoadingService } from '../../../core/services/loading.service';
import { SignalrService } from '../../../core/services/signalr.service';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  label: string;
  icon: string;
  link: string;
  adminOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', link: '/dashboard' },
  { label: 'Festivals & Events', icon: 'celebration', link: '/festivals' },
  { label: 'Events', icon: 'event', link: '/events' },
  { label: 'Visitors', icon: 'badge', link: '/visitors' },
  { label: 'Maintenance', icon: 'receipt_long', link: '/maintenance', adminOnly: true },
  { label: 'Residents', icon: 'people', link: '/residents', adminOnly: true },
  { label: 'My Bills', icon: 'payments', link: '/my-bills' },
  { label: 'Society Setup', icon: 'apartment', link: '/society-setup', adminOnly: true },
  { label: 'Users', icon: 'group', link: '/users', adminOnly: true },
  { label: 'Roles & Permissions', icon: 'admin_panel_settings', link: '/roles', adminOnly: true }
];

/** App shell — collapsible sidebar nav + topbar (search/theme-toggle/user
 * menu) + router-outlet content area. Mirrors the "modern admin dashboard"
 * look requested in the spec. */
@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule,
    MatListModule, MatIconModule, MatButtonModule, MatMenuModule, MatDividerModule,
    MatProgressBarModule, MatTooltipModule
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  readonly loading = inject(LoadingService);
  // Injected (not otherwise referenced) so the SignalR connection lifecycle —
  // driven by its internal `effect()` on auth.isAuthenticated() — starts as
  // soon as the authenticated app shell loads.
  private readonly signalr = inject(SignalrService);

  readonly sidenavOpen = signal(true);
  readonly navItems = NAV_ITEMS;

  toggleSidenav(): void {
    this.sidenavOpen.update((v) => !v);
  }

  visibleNavItems(): NavItem[] {
    return this.navItems.filter((item) => !item.adminOnly || this.auth.isAdmin());
  }

  logout(): void {
    this.auth.logout();
  }
}
