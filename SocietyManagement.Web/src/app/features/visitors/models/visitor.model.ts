export type VisitorVisitStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7;
// PendingApproval | Approved | Rejected | CheckedIn | CheckedOut | Expired | Cancelled

export const VISIT_STATUS_LABELS: Record<VisitorVisitStatus, string> = {
  1: 'Pending Approval', 2: 'Approved', 3: 'Rejected', 4: 'Checked In',
  5: 'Checked Out', 6: 'Expired', 7: 'Cancelled'
};

export interface Gate {
  id: number;
  societyId: number;
  name: string;
  code: string;
  location?: string | null;
  isActive: boolean;
}

export interface VisitorPurpose {
  id: number;
  societyId: number;
  name: string;
  requiresApproval: boolean;
  isActive: boolean;
  displayOrder: number;
}

export interface VisitorDto {
  id: number;
  societyId: number;
  name: string;
  mobileNumber: string;
  photoUrl?: string | null;
  vehicleNumber?: string | null;
  vehicleType?: string | null;
  idType?: string | null;
  idReference?: string | null;
  notes?: string | null;
}

export interface VisitorVisitDto {
  id: number;
  visitorId: number;
  visitorName: string;
  visitorMobile: string;
  visitorPhotoUrl?: string | null;
  visitorVehicleNumber?: string | null;
  flatId: number;
  flatNumber: string;
  purposeId: number;
  purposeName: string;
  gateId: number;
  gateName: string;
  numberOfVisitors: number;
  status: VisitorVisitStatus;
  requestedAt: string;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  rejectionReason?: string | null;
  checkInTime?: string | null;
  checkOutTime?: string | null;
}
