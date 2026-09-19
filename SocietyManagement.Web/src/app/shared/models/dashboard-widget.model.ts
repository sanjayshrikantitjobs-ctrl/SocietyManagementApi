/** Registry-entry contract for a dashboard widget (§6/§12 of the strategy
 * doc) — a plain, JSON-serializable shape so Web and Mobile can describe
 * the same widget catalog without sharing UI code. Nothing renders from
 * this yet; Step 6 wires an actual widget host into the Dashboard pages.
 * Mirrored on Mobile by DashboardWidgetDefinition. */
export interface DashboardWidgetDef {
  id: string;
  title: string;
  icon: string;
  /** Checked via AuthService.hasPermission() — omit for a widget every
   * signed-in role should see (e.g. "My Complaints"). */
  requiredPermission?: string;
  /** Checked against AuthService.roleName() — omit to allow every role
   * that also passes requiredPermission. */
  roles?: string[];
  size?: 'sm' | 'md' | 'lg';
}
