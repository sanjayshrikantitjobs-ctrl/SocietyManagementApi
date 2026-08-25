export type FinanceSource = 1 | 2 | 3 | 4; // Maintenance | Festival | WaterTanker | GeneralExpense

export const FINANCE_SOURCE_LABELS: Record<FinanceSource, string> = {
  1: 'Maintenance', 2: 'Festival', 3: 'Water Tanker', 4: 'General'
};

export type ExpenseCategory = 1 | 2 | 3 | 4 | 5; // VendorPayment|StaffSalary|Electricity|Repairs|Other

export const EXPENSE_CATEGORY_LABELS: Record<ExpenseCategory, string> = {
  1: 'Vendor Payment', 2: 'Staff Salary', 3: 'Electricity', 4: 'Repairs', 5: 'Other'
};

export type FinancePaymentMethod = 1 | 2 | 3; // Cash|UPI|BankTransfer

export const PAYMENT_METHOD_LABELS: Record<FinancePaymentMethod, string> = {
  1: 'Cash', 2: 'UPI', 3: 'Bank Transfer'
};

export interface ExpenseDto {
  id: number;
  societyId: number;
  category: ExpenseCategory;
  title: string;
  amount: number;
  expenseDate: string;
  paymentMethod: FinancePaymentMethod;
  paidTo?: string | null;
  staffId?: number | null;
  staffName?: string | null;
  billImageUrl?: string | null;
  notes?: string | null;
}

export interface FinanceIncomeRowDto {
  id: number;
  source: FinanceSource;
  date: string;
  amount: number;
  paymentMethod?: string | null;
  receiptNumber: string;
  payerName: string;
  flatNumber?: string | null;
  description: string;
}

export interface FinanceExpenseRowDto {
  id: number;
  source: FinanceSource;
  categoryLabel: string;
  title: string;
  amount: number;
  expenseDate: string;
  paymentMethod: string;
  paidTo?: string | null;
  festivalId?: number | null;
  festivalName?: string | null;
}

export interface FinanceOutstandingRowDto {
  source: FinanceSource;
  flatNumber?: string | null;
  payerName: string;
  amount: number;
  daysOverdue?: number | null;
}

export interface FinanceCategoryAmountDto {
  label: string;
  amount: number;
}

export interface FinanceMonthPointDto {
  monthLabel: string;
  income: number;
  expense: number;
}

export interface FinanceTransactionDto {
  date: string;
  type: 'Income' | 'Expense';
  source: string;
  description: string;
  amount: number;
}

export interface FinanceOverviewDto {
  totalIncome: number;
  totalExpense: number;
  availableBalance: number;
  pendingCollection: number;
  monthlyTrend: FinanceMonthPointDto[];
  incomeBySource: FinanceCategoryAmountDto[];
  expenseByCategory: FinanceCategoryAmountDto[];
  recentTransactions: FinanceTransactionDto[];
}

export interface FinanceLedgerRowDto {
  date: string;
  type: 'Income' | 'Expense';
  source: string;
  description: string;
  amount: number;
  runningBalance: number;
}

export interface FinanceLedgerPageDto {
  items: FinanceLedgerRowDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  openingBalance: number;
}

export interface FinanceReportSummaryDto {
  totalIncome: number;
  totalExpense: number;
  netBalance: number;
  incomeBySource: FinanceCategoryAmountDto[];
  expenseByCategory: FinanceCategoryAmountDto[];
}
