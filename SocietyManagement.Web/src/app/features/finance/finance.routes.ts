import { Routes } from '@angular/router';
import { FinanceShellComponent } from './finance-shell.component';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    component: FinanceShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
      {
        path: 'overview',
        loadComponent: () => import('./overview/finance-overview.component').then((m) => m.FinanceOverviewComponent)
      },
      {
        path: 'income',
        loadComponent: () => import('./income/income-list.component').then((m) => m.IncomeListComponent)
      },
      {
        path: 'expenses',
        loadComponent: () => import('./expenses/expenses-list.component').then((m) => m.ExpensesListComponent)
      },
      {
        path: 'outstanding',
        loadComponent: () => import('./outstanding/outstanding-list.component').then((m) => m.OutstandingListComponent)
      },
      {
        path: 'ledger',
        loadComponent: () => import('./ledger/ledger.component').then((m) => m.LedgerComponent)
      },
      {
        path: 'receipts',
        loadComponent: () => import('./receipts/receipts-list.component').then((m) => m.ReceiptsListComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./reports/financial-reports.component').then((m) => m.FinancialReportsComponent)
      }
    ]
  }
];
