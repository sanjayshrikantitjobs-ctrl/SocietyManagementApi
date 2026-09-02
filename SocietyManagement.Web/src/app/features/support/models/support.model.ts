export type SupportTicketStatus = 1 | 2 | 3; // Open | InProgress | Resolved

export const SUPPORT_TICKET_STATUS_LABELS: Record<SupportTicketStatus, string> = {
  1: 'Open', 2: 'In Progress', 3: 'Resolved'
};

export interface SupportTicketDto {
  id: number;
  societyId: number;
  societyName: string;
  createdByName: string;
  subject: string;
  description: string;
  status: SupportTicketStatus;
  createdAt: string;
  resolvedAt?: string | null;
  resolvedByName?: string | null;
  resolutionNotes?: string | null;
}
