export type ComplaintCategory = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10;

export const COMPLAINT_CATEGORY_LABELS: Record<ComplaintCategory, string> = {
  1: 'Plumbing', 2: 'Electrical', 3: 'Housekeeping', 4: 'Security', 5: 'Parking',
  6: 'Structural', 7: 'Noise', 8: 'Lift / Elevator', 9: 'Water Supply', 10: 'Other'
};

export type ComplaintPriority = 1 | 2 | 3;

export const COMPLAINT_PRIORITY_LABELS: Record<ComplaintPriority, string> = {
  1: 'Low', 2: 'Medium', 3: 'High'
};

export type ComplaintStatus = 1 | 2 | 3 | 4 | 5;

export const COMPLAINT_STATUS_LABELS: Record<ComplaintStatus, string> = {
  1: 'Open', 2: 'Assigned', 3: 'In Progress', 4: 'Resolved', 5: 'Closed'
};

export interface ComplaintDto {
  id: number;
  societyId: number;
  flatId: number;
  flatNumber: string;
  raisedByUserId: number;
  raisedByName: string;
  category: ComplaintCategory;
  title: string;
  description: string;
  priority: ComplaintPriority;
  status: ComplaintStatus;
  photoUrl?: string | null;
  assignedStaffId?: number | null;
  assignedStaffName?: string | null;
  assignedAt?: string | null;
  inProgressAt?: string | null;
  resolvedAt?: string | null;
  resolutionNotes?: string | null;
  closedAt?: string | null;
  reopenReason?: string | null;
  createdAt: string;
}

export interface ComplaintKpisDto {
  open: number;
  assigned: number;
  inProgress: number;
  resolved: number;
  closed: number;
}
