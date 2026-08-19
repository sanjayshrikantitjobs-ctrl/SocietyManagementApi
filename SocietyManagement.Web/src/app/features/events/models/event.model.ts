export type EventStatus = 1 | 2 | 3 | 4 | 5; // Draft | Open | Closed | Completed | Cancelled
export type EventRsvpStatus = 1 | 2 | 3; // Registered | CheckedIn | Cancelled

export const EVENT_STATUS_LABELS: Record<EventStatus, string> = {
  1: 'Draft', 2: 'Open', 3: 'Closed', 4: 'Completed', 5: 'Cancelled'
};

export const EVENT_RSVP_STATUS_LABELS: Record<EventRsvpStatus, string> = {
  1: 'Registered', 2: 'Checked In', 3: 'Cancelled'
};

export interface EventDto {
  id: number;
  societyId: number;
  festivalId?: number | null;
  festivalName?: string | null;
  name: string;
  description?: string | null;
  eventDateTime: string;
  venue?: string | null;
  capacityLimit?: number | null;
  rsvpDeadline?: string | null;
  status: EventStatus;
}

export interface EventCapacitySummaryDto {
  eventId: number;
  capacityLimit?: number | null;
  totalRegistered: number;
  totalCheckedIn: number;
  remainingSeats?: number | null;
}

export interface EventRsvpDto {
  id: number;
  eventId: number;
  flatId: number;
  flatNumber: string;
  memberId: number;
  memberName: string;
  memberPhone: string;
  headCount: number;
  qrToken: string;
  status: EventRsvpStatus;
  checkedInCount?: number | null;
  checkedInAt?: string | null;
}
