export type FestivalStatus = 1 | 2 | 3; // Planning | Ongoing | Completed
export type FestivalVisibility = 1 | 2; // Public | MembersOnly
export type FestivalBudgetCategoryType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12;
export type ContributionPaymentMethod = 1 | 2 | 3; // Cash | UPI | BankTransfer
export type SponsorshipType = 1 | 2 | 3 | 4 | 5 | 6 | 7; // Title..Media
export type ExpenseApprovalStatus = 1 | 2 | 3 | 4 | 5; // Draft|Pending|Approved|Rejected|Paid
export type VendorCategory = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;
export type FlatContributionStatus = 0 | 1 | 2 | 3; // NoTarget|Pending|PartiallyPaid|Paid
export type FestivalKind = 1 | 2 | 3; // Standalone | Pool | Child

export const FESTIVAL_STATUS_LABELS: Record<FestivalStatus, string> = {
  1: 'Planning', 2: 'Ongoing', 3: 'Completed'
};

export const FESTIVAL_VISIBILITY_LABELS: Record<FestivalVisibility, string> = {
  1: 'Public', 2: 'Members Only'
};

export const BUDGET_CATEGORY_LABELS: Record<FestivalBudgetCategoryType, string> = {
  1: 'Decoration', 2: 'Lighting', 3: 'Sound', 4: 'Food', 5: 'Idol', 6: 'Pandal',
  7: 'Stage', 8: 'Security', 9: 'Cleaning', 10: 'Generator', 11: 'Photography', 12: 'Miscellaneous'
};

export const PAYMENT_METHOD_LABELS: Record<ContributionPaymentMethod, string> = {
  1: 'Cash', 2: 'UPI', 3: 'Bank Transfer'
};

export const SPONSORSHIP_TYPE_LABELS: Record<SponsorshipType, string> = {
  1: 'Title', 2: 'Platinum', 3: 'Gold', 4: 'Silver', 5: 'Bronze', 6: 'In-Kind', 7: 'Media'
};

export const EXPENSE_STATUS_LABELS: Record<ExpenseApprovalStatus, string> = {
  1: 'Draft', 2: 'Pending', 3: 'Approved', 4: 'Rejected', 5: 'Paid'
};

export const VENDOR_CATEGORY_LABELS: Record<VendorCategory, string> = {
  1: 'Decorator', 2: 'Sound', 3: 'Catering', 4: 'Electrician', 5: 'Tent House',
  6: 'Generator', 7: 'Photographer', 8: 'Other'
};

export const FLAT_CONTRIBUTION_STATUS_LABELS: Record<FlatContributionStatus, string> = {
  0: 'No Target', 1: 'Pending', 2: 'Partially Paid', 3: 'Paid'
};

export const FESTIVAL_KIND_LABELS: Record<FestivalKind, string> = {
  1: 'Standalone', 2: 'Contribution Pool', 3: 'Child Festival'
};

export interface Festival {
  id: number;
  societyId: number;
  name: string;
  year: number;
  startDate: string;
  endDate: string;
  description?: string | null;
  bannerImageUrl?: string | null;
  coverPhotoUrl?: string | null;
  theme?: string | null;
  status: FestivalStatus;
  visibility: FestivalVisibility;
  isRecurring: boolean;
  parentFestivalId?: number | null;
  kind: FestivalKind;
  contributionPoolFestivalId?: number | null;
  contributionPoolFestivalName?: string | null;
  totalBudget: number;
  collected: number;
  spent: number;
  remaining: number;
  sponsorCount: number;
  pendingExpenseCount: number;
}

export interface FestivalBudgetCategoryDto {
  id: number;
  festivalId: number;
  category: FestivalBudgetCategoryType;
  estimatedAmount: number;
  approvedAmount: number;
  actualAmount: number;
  remaining: number;
  notes?: string | null;
}

export interface FestivalBudgetRevisionDto {
  id: number;
  festivalBudgetCategoryId: number;
  previousEstimatedAmount: number;
  newEstimatedAmount: number;
  previousApprovedAmount: number;
  newApprovedAmount: number;
  reason?: string | null;
  createdAt: string;
  createdBy?: string | null;
}

export interface FestivalContributionDto {
  id: number;
  festivalId: number;
  flatId?: number | null;
  flatNumber?: string | null;
  memberName: string;
  amount: number;
  paymentMethod: ContributionPaymentMethod;
  paymentDate: string;
  transactionId?: string | null;
  receiptNumber: string;
  isAnonymous: boolean;
  createdAt: string;
}

export interface TopContributorDto {
  memberName: string;
  flatNumber?: string | null;
  totalAmount: number;
  contributionCount: number;
}

export interface PendingContributorDto {
  flatId: number;
  flatNumber: string;
}

export interface ContributableFlatDto {
  flatId: number;
  flatNumber: string;
}

export interface FlatContributionDto {
  flatId: number;
  flatNumber: string;
  targetAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: FlatContributionStatus;
}

export interface FlatContributionKpisDto {
  totalFlats: number;
  totalTargetAmount: number;
  totalPaidAmount: number;
  totalOutstandingAmount: number;
  flatsPaidCount: number;
  flatsPartiallyPaidCount: number;
  flatsPendingCount: number;
  flatsNoTargetCount: number;
}

export interface FestivalSponsorDto {
  id: number;
  festivalId: number;
  companyName: string;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  sponsorshipType: SponsorshipType;
  promisedAmount: number;
  receivedAmount: number;
  pendingAmount: number;
  logoUrl?: string | null;
  bannerUrl?: string | null;
}

export interface FestivalVendorDto {
  id: number;
  societyId: number;
  name: string;
  category: VendorCategory;
  phone?: string | null;
  email?: string | null;
  gstNumber?: string | null;
  address?: string | null;
  rating: number;
  totalPayments: number;
  outstandingAmount: number;
  previousFestivalsCount: number;
}

export interface FestivalExpenseDto {
  id: number;
  festivalId: number;
  festivalBudgetCategoryId: number;
  category: FestivalBudgetCategoryType;
  vendorId?: number | null;
  vendorName?: string | null;
  amount: number;
  expenseDate: string;
  description?: string | null;
  paymentMethod: ContributionPaymentMethod;
  billImageUrl?: string | null;
  invoiceNumber?: string | null;
  approvalStatus: ExpenseApprovalStatus;
  approvedByUserId?: number | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
}

export interface FestivalVolunteerDto {
  id: number;
  festivalId: number;
  name: string;
  phone?: string | null;
  email?: string | null;
  notes?: string | null;
}

export type FestivalTaskStatus = 1 | 2 | 3;

export const FESTIVAL_TASK_STATUS_LABELS: Record<FestivalTaskStatus, string> = {
  1: 'Pending', 2: 'In Progress', 3: 'Completed'
};

export interface FestivalTaskDto {
  id: number;
  festivalId: number;
  title: string;
  description?: string | null;
  assignedVolunteerId?: number | null;
  assignedVolunteerName?: string | null;
  status: FestivalTaskStatus;
  dueDate?: string | null;
}

export interface FestivalKpisDto {
  budget: number;
  collected: number;
  spent: number;
  remaining: number;
  sponsorsCount: number;
  pendingExpensesCount: number;
  volunteersCount: number;
  tasksPendingCount: number;
}

export interface BudgetVsActualPointDto {
  categoryName: string;
  estimated: number;
  approved: number;
  actual: number;
}

export interface ExpenseCategoryPointDto {
  categoryName: string;
  amount: number;
}

export interface SponsorContributionPointDto {
  companyName: string;
  promised: number;
  received: number;
}

export interface FestivalDashboardDto {
  kpis: FestivalKpisDto;
  budgetVsActual: BudgetVsActualPointDto[];
  expenseByCategory: ExpenseCategoryPointDto[];
  sponsorContributions: SponsorContributionPointDto[];
}

export interface ContributionPoolDto {
  id: number;
  name: string;
  year: number;
}

export interface PoolChildSummaryDto {
  festivalId: number;
  name: string;
  status: FestivalStatus;
  budget: number;
  spent: number;
}

export interface PoolSummaryDto {
  festivalId: number;
  poolCollected: number;
  childrenSpent: number;
  poolRemaining: number;
  children: PoolChildSummaryDto[];
}

export interface ChildPoolStatusDto {
  poolFestivalId: number;
  poolFestivalName: string;
  poolRemaining: number;
}
