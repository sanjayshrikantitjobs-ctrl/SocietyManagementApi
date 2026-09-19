export interface NotificationDto {
  id: number;
  eventType: string;
  title: string;
  message: string;
  payloadJson?: string | null;
  createdAt: string;
  isRead: boolean;
  readAt?: string | null;
}

/** Maps a persisted notification's eventType to where it should deep-link —
 * mirrors the exact same event-name strings NotificationService already
 * raises (see backend INotificationService), so this is the one place that
 * needs updating if a new event type is ever added, not a parallel taxonomy. */
export function resolveNotificationLink(n: NotificationDto): string[] | null {
  try {
    const payload = n.payloadJson ? JSON.parse(n.payloadJson) : null;
    switch (n.eventType) {
      case 'AnnouncementPublished':
        return payload?.AnnouncementId ? ['/announcements', payload.AnnouncementId] : ['/announcements'];
      // Raised/Reopened only ever reach Admin (who can see /complaints);
      // Assigned/InProgress/Resolved only ever reach the complaint's own
      // raiser, who has no complaints.view permission — /my-complaints is
      // the page they can actually open.
      case 'ComplaintRaised':
      case 'ComplaintReopened':
        return ['/complaints'];
      case 'ComplaintAssigned':
      case 'ComplaintInProgress':
      case 'ComplaintResolved':
        return ['/my-complaints'];
      case 'VisitorApprovalRequested':
      case 'VisitorApproved':
      case 'VisitorRejected':
      case 'VisitorRequestExpired':
        return ['/visitors'];
      case 'FestivalContributionRecorded':
      case 'FestivalExpenseApproved':
        return payload?.festivalId ? ['/festivals', payload.festivalId] : ['/festivals'];
      // These two never share a recipient: Created only ever reaches
      // SuperAdmin, Resolved only ever reaches the ticket's own creator —
      // so each can safely point at its one real destination.
      case 'SupportTicketCreated':
        return ['/admin/support-tickets'];
      case 'SupportTicketResolved':
        return ['/support'];
      default:
        return null;
    }
  } catch {
    return null;
  }
}
