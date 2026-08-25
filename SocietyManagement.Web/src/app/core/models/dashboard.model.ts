export interface PendingActions {
  outstandingBillsCount: number;
  openComplaintsCount: number;
  vendorPaymentsDueCount: number;
  waterTankerPendingCount: number;
  visitorRequestsWaitingCount: number;
}

export interface AdminDashboardSummary {
  totalFlats: number;
  occupiedFlats: number;
  ownersCount: number;
  tenantsCount: number;
  collectedMaintenance: number;
  outstandingAmount: number;
  openComplaintsCount: number;
  visitorsToday: number;
  pendingActions: PendingActions;
}

export interface MonthlyCollectionPoint {
  monthLabel: string;
  collected: number;
  pending: number;
}

export interface UpcomingFestival {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  budget: number;
  collected: number;
}

export interface UpcomingEvent {
  id: number;
  name: string;
  eventDateTime: string;
  venue?: string | null;
}

export interface UpcomingItems {
  festival?: UpcomingFestival | null;
  events: UpcomingEvent[];
}

export interface RecentActivityItem {
  type: string;
  title: string;
  subtitle?: string | null;
  timestamp: string;
}

export interface MemberDashboardSummary {
  myMaintenanceDue: number;
  unreadNoticesCount: number;
  upcomingEventsCount: number;
  myOpenComplaintsCount: number;
}
