export type ChargeType = 1 | 2 | 3; // Fixed | PerSqFt | OneTime
export type ChargeFrequency = 1 | 2; // Monthly | OneTime
export type FineStatus = 1 | 2 | 3; // Pending | Billed | Waived
export type BillStatus = 1 | 2 | 3 | 4; // Pending | PartiallyPaid | Paid | Overdue
export type PaymentMode = 1 | 2 | 3 | 4; // Cash | UPI | BankTransfer | Cheque
export type BillItemType = 1 | 2 | 3; // Category | SpecialCharge | Fine

export const CHARGE_TYPE_LABELS: Record<ChargeType, string> = {
  1: 'Fixed', 2: 'Per Sq Ft', 3: 'One-time'
};

export const CHARGE_FREQUENCY_LABELS: Record<ChargeFrequency, string> = {
  1: 'Monthly', 2: 'One-time'
};

export const FINE_STATUS_LABELS: Record<FineStatus, string> = {
  1: 'Pending', 2: 'Billed', 3: 'Waived'
};

export const BILL_STATUS_LABELS: Record<BillStatus, string> = {
  1: 'Pending', 2: 'Partially Paid', 3: 'Paid', 4: 'Overdue'
};

export const PAYMENT_MODE_LABELS: Record<PaymentMode, string> = {
  1: 'Cash', 2: 'UPI', 3: 'Bank Transfer', 4: 'Cheque'
};

export const BILL_ITEM_TYPE_LABELS: Record<BillItemType, string> = {
  1: 'Category Charge', 2: 'Special Charge', 3: 'Fine'
};

export interface MaintenanceCategoryDto {
  id: number;
  societyId: number;
  chargeName: string;
  chargeType: ChargeType;
  monthlyAmount: number;
  effectiveFrom: string;
  isActive: boolean;
  displayOrder: number;
}

export interface MaintenanceSettingsDto {
  id: number;
  societyId: number;
  billGenerationDay: number;
  dueDay: number;
  gracePeriodDays: number;
  lateFeeAmount: number;
  invoiceNumberPrefix: string;
  nextInvoiceNumber: number;
  whatsAppMessageTemplate: string;
  pdfFooterMessage: string;
}

export interface SpecialChargeDto {
  id: number;
  flatId: number;
  flatNumber: string;
  chargeName: string;
  amount: number;
  frequency: ChargeFrequency;
  startDate: string;
  endDate?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface FineRecordDto {
  id: number;
  flatId: number;
  flatNumber: string;
  reason: string;
  amount: number;
  fineDate: string;
  status: FineStatus;
}

export interface MaintenanceBillDto {
  id: number;
  flatId: number;
  flatNumber: string;
  buildingName: string;
  wingName: string;
  billMonth: string;
  invoiceNumber: string;
  previousBalance: number;
  fineAmount: number;
  totalAmount: number;
  amountPaid: number;
  balance: number;
  dueDate: string;
  status: BillStatus;
  pdfUrl?: string | null;
  ownerNameSnapshot?: string | null;
}

export interface MaintenanceBillItemDto {
  id: number;
  description: string;
  amount: number;
  itemType: BillItemType;
}

export interface MaintenancePaymentDto {
  id: number;
  amount: number;
  paymentDate: string;
  paymentMode: PaymentMode;
  transactionReference?: string | null;
  notes?: string | null;
}

export interface MaintenanceBillDetailDto extends MaintenanceBillDto {
  items: MaintenanceBillItemDto[];
  payments: MaintenancePaymentDto[];
}

export interface MaintenanceKpisDto {
  totalFlats: number;
  billsGenerated: number;
  paid: number;
  pending: number;
  overdue: number;
  totalCollection: number;
  outstanding: number;
}

export interface MonthlyCollectionPointDto {
  monthLabel: string;
  amount: number;
}

export interface PaidVsPendingDto {
  paidAmount: number;
  outstandingAmount: number;
}

export interface OutstandingByWingPointDto {
  wingName: string;
  outstanding: number;
}

export interface RecentPaymentDto {
  id: number;
  flatNumber: string;
  amount: number;
  paymentDate: string;
  paymentMode: PaymentMode;
}

export interface OverdueFlatDto {
  billId: number;
  flatNumber: string;
  invoiceNumber: string;
  balance: number;
  daysOverdue: number;
}

export interface MaintenanceDashboardDto {
  kpis: MaintenanceKpisDto;
  monthlyCollectionTrend: MonthlyCollectionPointDto[];
  paidVsPending: PaidVsPendingDto;
  outstandingByWing: OutstandingByWingPointDto[];
  recentPayments: RecentPaymentDto[];
  overdueFlats: OverdueFlatDto[];
}

export interface WaterTankerCollectionDto {
  id: number;
  flatId: number;
  flatNumber: string;
  month: string;
  amount: number;
  isPaid: boolean;
  paymentDate?: string | null;
  notes?: string | null;
}

export interface WaterTankerMonthSummaryDto {
  totalFlats: number;
  flatsPaidCount: number;
  flatsPendingCount: number;
  totalCollected: number;
  totalPending: number;
}
