export type AnnouncementType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12;
export type AnnouncementPriority = 1 | 2 | 3 | 4;
export type AnnouncementStatus = 1 | 2 | 3 | 4;

export const ANNOUNCEMENT_TYPE_LABELS: Record<AnnouncementType, string> = {
  1: 'General', 2: 'Society Meeting', 3: 'Festival', 4: 'Maintenance', 5: 'Water/Electricity Notice',
  6: 'Lost & Found', 7: 'Emergency', 8: 'Event', 9: 'Parking', 10: 'Security', 11: 'Important Notice', 12: 'Other'
};

export const ANNOUNCEMENT_PRIORITY_LABELS: Record<AnnouncementPriority, string> = {
  1: 'Low', 2: 'Normal', 3: 'High', 4: 'Urgent'
};

export const ANNOUNCEMENT_STATUS_LABELS: Record<AnnouncementStatus, string> = {
  1: 'Draft', 2: 'Scheduled', 3: 'Published', 4: 'Expired'
};

export interface AnnouncementDto {
  id: number;
  societyId: number;
  title: string;
  description: string;
  type: AnnouncementType;
  priority: AnnouncementPriority;
  attachmentUrl?: string | null;
  publishAt?: string | null;
  expiryAt?: string | null;
  status: AnnouncementStatus;
  createdAt: string;
  isRead: boolean;
}
