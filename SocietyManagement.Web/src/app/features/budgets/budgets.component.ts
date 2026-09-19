import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../shared/components/prompt-dialog/prompt-dialog.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { SocietyService } from '../society-setup/services/society.service';

interface BudgetLine { category: number; budgetId?: number | null; budgeted: number; actual: number; remaining: number; }
interface BudgetOverview {
  financialYear: number; periodStart: string; periodEnd: string; totalBudgeted: number; totalActual: number; lines: BudgetLine[];
}

const CATEGORY_LABELS: Record<number, string> = { 1: 'Vendor payments', 2: 'Staff salary', 3: 'Electricity', 4: 'Repairs', 5: 'Other' };

// April-to-March financial year, identified by its starting calendar year.
const currentFinancialYear = () => { const d = new Date(); return d.getMonth() >= 3 ? d.getFullYear() : d.getFullYear() - 1; };

@Component({
  selector: 'app-budgets',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatSelectModule, PageHeaderComponent,
    SkeletonLoaderComponent, StatCardComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Budget vs Actual" subtitle="Yearly budget per expense head against what has actually been spent."
        [breadcrumbs]="[{ label: 'Finance', link: '/finance' }, { label: 'Budgets' }]">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="year">
          <mat-select [(ngModel)]="year" (ngModelChange)="load()">
            @for (y of years; track y) { <mat-option [value]="y">FY {{ y }}-{{ (y + 1) % 100 | number: '2.0-0' }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </app-page-header>

      @if (loading()) {
        <app-skeleton-loader [rows]="5" />
      } @else if (overview(); as o) {
        <div class="stats">
          <app-stat-card label="Total budget" [value]="'₹' + (o.totalBudgeted | number)" icon="account_balance_wallet" />
          <app-stat-card label="Spent so far" [value]="'₹' + (o.totalActual | number)" icon="receipt_long" iconColor="#dc2626" iconBg="#fef2f2" />
          <app-stat-card label="Remaining" [value]="'₹' + ((o.totalBudgeted - o.totalActual) | number)" icon="savings" iconColor="#16a34a" iconBg="#ecfdf5" />
        </div>
        <p class="muted">{{ o.periodStart | date: 'd MMM y' }} – {{ o.periodEnd | date: 'd MMM y' }}. Actuals come from recorded general expenses (festival spending is tracked in its own budget).</p>

        <div class="app-card lines">
          @for (l of o.lines; track l.category) {
            <div class="line">
              <div class="name">
                <strong>{{ labels[l.category] }}</strong>
                <span class="muted">{{ l.actual | currency: 'INR' : 'symbol' : '1.0-0' }} of {{ l.budgeted | currency: 'INR' : 'symbol' : '1.0-0' }}</span>
              </div>
              <div class="bar" [title]="pct(l) + '% used'">
                <div class="fill" [class.over]="l.actual > l.budgeted && l.budgeted > 0" [class.none]="l.budgeted === 0"
                     [style.width.%]="min(pct(l), 100)"></div>
              </div>
              <div class="remaining" [class.neg]="l.remaining < 0">
                @if (l.budgeted === 0 && l.actual === 0) { — }
                @else if (l.budgeted === 0) { No budget set }
                @else { {{ l.remaining < 0 ? 'Over by ' : '' }}{{ (l.remaining < 0 ? -l.remaining : l.remaining) | currency: 'INR' : 'symbol' : '1.0-0' }}{{ l.remaining >= 0 ? ' left' : '' }} }
              </div>
              @if (canManage()) {
                <button mat-stroked-button (click)="setBudget(l)"><mat-icon>edit</mat-icon> {{ l.budgeted ? 'Change' : 'Set' }}</button>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .year { width: 170px; }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 16px; margin-bottom: 12px; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .lines { padding: 4px 0; margin-top: 12px; }
    .line { display: grid; grid-template-columns: 220px 1fr 160px auto; gap: 16px; align-items: center; padding: 14px 16px; border-bottom: 1px solid var(--app-border); }
    .line:last-child { border-bottom: 0; }
    .name { display: flex; flex-direction: column; }
    .bar { height: 10px; background: var(--app-surface-alt); border-radius: 999px; overflow: hidden; }
    .fill { height: 100%; background: var(--app-primary); border-radius: 999px; }
    .fill.over { background: #dc2626; }
    .fill.none { background: #94a3b8; }
    .remaining { font-size: 13px; text-align: right; }
    .remaining.neg { color: #b91c1c; font-weight: 600; }
    @media (max-width: 900px) { .line { grid-template-columns: 1fr; } .remaining { text-align: left; } }
  `]
})
export class BudgetsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly labels = CATEGORY_LABELS;
  readonly years = [currentFinancialYear() - 1, currentFinancialYear(), currentFinancialYear() + 1];
  year = currentFinancialYear();

  readonly loading = signal(true);
  readonly overview = signal<BudgetOverview | null>(null);
  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('expenses.manage'); }
  min(a: number, b: number): number { return Math.min(a, b); }
  pct(l: BudgetLine): number { return l.budgeted > 0 ? Math.round((l.actual / l.budgeted) * 100) : l.actual > 0 ? 100 : 0; }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.loading.set(false); return; }
      this.societyId = societies[0].id;
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<BudgetOverview>>(`${environment.apiUrl}/budgets`, { params: { societyId: this.societyId, financialYear: this.year } })
      .pipe(map((r) => r.data!))
      .subscribe((o) => { this.overview.set(o); this.loading.set(false); });
  }

  setBudget(line: BudgetLine): void {
    this.dialog.open(PromptDialogComponent, {
      width: '380px',
      data: {
        title: `${this.labels[line.category]} — FY ${this.year}-${(this.year + 1) % 100}`, submitLabel: 'Save',
        fields: [{ key: 'amount', label: 'Yearly budget (₹)', type: 'number', defaultValue: line.budgeted }]
      }
    }).afterClosed().subscribe((r) => {
      if (!r) return;
      this.http.put<ApiResponse<void>>(`${environment.apiUrl}/budgets`, {
        societyId: this.societyId, financialYear: this.year, category: line.category, amount: Number(r.amount)
      }).subscribe(() => { this.toast.success('Budget saved.'); this.load(); });
    });
  }
}
