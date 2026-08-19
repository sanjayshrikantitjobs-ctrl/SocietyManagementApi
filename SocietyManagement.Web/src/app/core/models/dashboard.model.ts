export interface AdminDashboardSummary {
  totalMembers: number;
  totalUsers: number;
  occupiedFlats: number;
  vacantFlats: number;
  totalFlats: number;
  totalParkingSlots: number;
  allocatedParkingSlots: number;
  pendingMaintenance: number;
  collectedMaintenance: number;
  outstandingAmount: number;
  upcomingFestivalsCount: number;
  recentComplaintsCount: number;
  visitorsToday: number;
}

export interface MemberDashboardSummary {
  myMaintenanceDue: number;
  unreadNoticesCount: number;
  upcomingEventsCount: number;
  myOpenComplaintsCount: number;
}
