import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SkeletonLoaderComponent } from '../../shared/components/skeleton-loader/skeleton-loader.component';
import { STAFF_CATEGORY_LABELS } from '../staff/models/staff.model';
import { SocietyService } from '../society-setup/services/society.service';
import {
  ATTENDANCE_STATUS_OPTIONS, AttendanceService, DailyAttendanceDto, MonthlyAttendanceDto
} from './attendance.service';

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const CLOCK_STATUSES = [1, 2, 3];
const pad = (n: number) => String(n).padStart(2, '0');
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const nowTime = () => { const d = new Date(); return `${pad(d.getHours())}:${pad(d.getMinutes())}`; };
// API times come back as "HH:mm:ss"; <input type=time> wants "HH:mm".
const toInputTime = (value: string | null | undefined) => (value ? value.substring(0, 5) : '');

@Component({
  selector: 'app-attendance',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, MatTableModule,
    MatTabsModule, EmptyStateComponent, PageHeaderComponent, SkeletonLoaderComponent
  ],
  template: `
    <div class="app-page">
      <app-page-header title="Staff Attendance" subtitle="Mark daily attendance and review the monthly summary."
        [breadcrumbs]="[{ label: 'Staff', link: '/staff' }, { label: 'Attendance' }]" />

      <mat-tab-group animationDuration="0ms" (selectedTabChange)="onTabChange($event.index)">
        <mat-tab label="Daily">
          <div class="tab-body">
            <div class="bar">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Date</mat-label>
                <input matInput type="date" [ngModel]="date()" [max]="today" (ngModelChange)="onDateChange($event)" />
              </mat-form-field>
              <div class="summary">
                <span class="pill ok">{{ count(1) + count(2) }} present</span>
                <span class="pill warn">{{ count(3) }} half day</span>
                <span class="pill bad">{{ count(4) }} absent</span>
                <span class="pill">{{ count(5) }} leave</span>
                <span class="pill muted">{{ unmarked() }} not marked</span>
              </div>
            </div>

            @if (dailyLoading()) {
              <app-skeleton-loader [rows]="5" />
            } @else if (daily().length === 0) {
              <app-empty-state icon="badge" title="No active staff" message="Add staff members first, then mark their attendance here." />
            } @else {
              <div class="app-card list">
                @for (row of daily(); track row.staffId) {
                  <div class="row" [class.saving]="saving() === row.staffId">
                    <div class="who">
                      <strong>{{ row.staffName }}</strong>
                      <span class="muted">{{ categoryLabels[row.category] }}</span>
                    </div>
                    <div class="statuses">
                      @for (opt of statusOptions; track opt.value) {
                        <button type="button" class="chip" [class]="'chip s' + opt.value + (row.status === opt.value ? ' on' : '')"
                                [disabled]="!canManage()" (click)="setStatus(row, opt.value)">{{ opt.label }}</button>
                      }
                    </div>
                    <div class="times">
                      @if (showsClock(row)) {
                        <label>In <input type="time" [ngModel]="inTime(row)" (ngModelChange)="setTime(row, 'in', $event)" [disabled]="!canManage()" /></label>
                        <label>Out <input type="time" [ngModel]="outTime(row)" (ngModelChange)="setTime(row, 'out', $event)" [disabled]="!canManage()" /></label>
                        @if (canManage() && !row.checkOutTime) {
                          <button mat-stroked-button type="button" (click)="checkOutNow(row)"><mat-icon>logout</mat-icon> Check out now</button>
                        }
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </mat-tab>

        <mat-tab label="Monthly summary">
          <div class="tab-body">
            <div class="bar">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Month</mat-label>
                <mat-select [(ngModel)]="month" (ngModelChange)="loadMonthly()">
                  @for (name of monthNames; track $index) { <mat-option [value]="$index + 1">{{ name }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>Year</mat-label>
                <mat-select [(ngModel)]="year" (ngModelChange)="loadMonthly()">
                  @for (y of years; track y) { <mat-option [value]="y">{{ y }}</mat-option> }
                </mat-select>
              </mat-form-field>
              <span class="spacer"></span>
              <button mat-stroked-button (click)="exportCsv()" [disabled]="!monthly()?.rows?.length"><mat-icon>download</mat-icon> Export CSV</button>
            </div>

            @if (monthlyLoading()) {
              <app-skeleton-loader [rows]="5" />
            } @else if (monthly(); as m) {
              @if (m.rows.length === 0) {
                <app-empty-state icon="event_busy" title="No staff" message="There is no active staff to summarise." />
              } @else {
                <div class="app-card">
                  <table mat-table [dataSource]="m.rows">
                    <ng-container matColumnDef="staffName"><th mat-header-cell *matHeaderCellDef>Staff</th>
                      <td mat-cell *matCellDef="let r"><strong>{{ r.staffName }}</strong><div class="muted">{{ categoryLabels[r.category] }}</div></td></ng-container>
                    <ng-container matColumnDef="present"><th mat-header-cell *matHeaderCellDef>Present</th><td mat-cell *matCellDef="let r">{{ r.present }}</td></ng-container>
                    <ng-container matColumnDef="late"><th mat-header-cell *matHeaderCellDef>Late</th><td mat-cell *matCellDef="let r">{{ r.late }}</td></ng-container>
                    <ng-container matColumnDef="halfDay"><th mat-header-cell *matHeaderCellDef>Half day</th><td mat-cell *matCellDef="let r">{{ r.halfDay }}</td></ng-container>
                    <ng-container matColumnDef="absent"><th mat-header-cell *matHeaderCellDef>Absent</th><td mat-cell *matCellDef="let r">{{ r.absent }}</td></ng-container>
                    <ng-container matColumnDef="leave"><th mat-header-cell *matHeaderCellDef>Leave</th><td mat-cell *matCellDef="let r">{{ r.leave }}</td></ng-container>
                    <ng-container matColumnDef="unmarked"><th mat-header-cell *matHeaderCellDef>Not marked</th><td mat-cell *matCellDef="let r">{{ r.unmarked }}</td></ng-container>
                    <tr mat-header-row *matHeaderRowDef="monthlyColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: monthlyColumns;"></tr>
                  </table>
                </div>
                <p class="muted note">Counted over {{ m.daysInPeriod }} day(s) of the period so far.</p>
              }
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: [`
    .tab-body { padding: 16px 0; }
    .bar { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-bottom: 16px; }
    .spacer { flex: 1; }
    .summary { display: flex; gap: 8px; flex-wrap: wrap; }
    .pill { font-size: 12px; font-weight: 600; padding: 4px 10px; border-radius: 999px; background: var(--app-surface-alt); }
    .pill.ok { background: #dcfce7; color: #15803d; }
    .pill.warn { background: #fef3c7; color: #b45309; }
    .pill.bad { background: #fee2e2; color: #b91c1c; }
    .pill.muted { color: var(--app-text-muted); }
    .list { padding: 4px 0; }
    .row { display: grid; grid-template-columns: 200px 1fr auto; gap: 16px; align-items: center; padding: 12px 16px; border-bottom: 1px solid var(--app-border); }
    .row:last-child { border-bottom: 0; }
    .row.saving { opacity: .6; }
    .who { display: flex; flex-direction: column; }
    .muted { color: var(--app-text-muted); font-size: 12px; }
    .statuses { display: flex; gap: 6px; flex-wrap: wrap; }
    .chip { border: 1px solid var(--app-border); background: var(--app-surface); border-radius: 999px; padding: 5px 12px; font-size: 12px; cursor: pointer; color: inherit; }
    .chip:disabled { cursor: default; }
    .chip.on.s1 { background: #dcfce7; border-color: #16a34a; color: #15803d; }
    .chip.on.s2 { background: #fef3c7; border-color: #d97706; color: #b45309; }
    .chip.on.s3 { background: #e0f2fe; border-color: #0284c7; color: #0369a1; }
    .chip.on.s4 { background: #fee2e2; border-color: #dc2626; color: #b91c1c; }
    .chip.on.s5 { background: #ede9fe; border-color: #7c3aed; color: #6d28d9; }
    .times { display: flex; gap: 12px; align-items: center; font-size: 12px; }
    .times input { border: 1px solid var(--app-border); border-radius: 6px; padding: 4px 6px; background: var(--app-surface); color: inherit; }
    table { width: 100%; }
    .note { margin-top: 8px; }
    @media (max-width: 768px) { .row { grid-template-columns: 1fr; } }
  `]
})
export class AttendanceComponent implements OnInit {
  private readonly attendanceService = inject(AttendanceService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  readonly categoryLabels: Record<number, string> = STAFF_CATEGORY_LABELS;
  readonly statusOptions = ATTENDANCE_STATUS_OPTIONS;
  readonly monthNames = MONTH_NAMES;
  readonly monthlyColumns = ['staffName', 'present', 'late', 'halfDay', 'absent', 'leave', 'unmarked'];
  readonly today = isoDate(new Date());
  readonly years = [new Date().getFullYear() - 1, new Date().getFullYear(), new Date().getFullYear() + 1];

  readonly date = signal(isoDate(new Date()));
  readonly daily = signal<DailyAttendanceDto[]>([]);
  readonly dailyLoading = signal(true);
  readonly saving = signal<number | null>(null);
  readonly monthly = signal<MonthlyAttendanceDto | null>(null);
  readonly monthlyLoading = signal(false);
  month = new Date().getMonth() + 1;
  year = new Date().getFullYear();

  private societyId = 0;

  canManage(): boolean { return this.auth.hasPermission('staff.manage'); }
  showsClock(row: DailyAttendanceDto): boolean { return row.status != null && CLOCK_STATUSES.includes(row.status); }
  inTime(row: DailyAttendanceDto): string { return toInputTime(row.checkInTime); }
  outTime(row: DailyAttendanceDto): string { return toInputTime(row.checkOutTime); }
  count(status: number): number { return this.daily().filter((r) => r.status === status).length; }
  unmarked(): number { return this.daily().filter((r) => r.status == null).length; }

  ngOnInit(): void {
    this.societyService.getSocieties().subscribe((societies) => {
      if (societies.length === 0) { this.dailyLoading.set(false); return; }
      this.societyId = societies[0].id;
      this.loadDaily();
    });
  }

  onTabChange(index: number): void {
    if (index === 1) this.loadMonthly();
  }

  onDateChange(value: string): void {
    if (!value) return;
    this.date.set(value);
    this.loadDaily();
  }

  loadDaily(): void {
    this.dailyLoading.set(true);
    this.attendanceService.getDaily(this.societyId, this.date()).subscribe((rows) => {
      this.daily.set(rows);
      this.dailyLoading.set(false);
    });
  }

  loadMonthly(): void {
    if (!this.societyId) return;
    this.monthlyLoading.set(true);
    this.attendanceService.getMonthly(this.societyId, this.year, this.month).subscribe((m) => {
      this.monthly.set(m);
      this.monthlyLoading.set(false);
    });
  }

  setStatus(row: DailyAttendanceDto, status: number): void {
    if (row.status === status) return;
    const clocks = CLOCK_STATUSES.includes(status);
    // Present/Late/Half day default the check-in to "now" when marking
    // today — Absent/Leave drop the clock times server-side.
    const checkIn = clocks ? (row.checkInTime ? toInputTime(row.checkInTime) : this.date() === this.today ? nowTime() : null) : null;
    this.save(row, status, checkIn, clocks ? toInputTime(row.checkOutTime) || null : null);
  }

  setTime(row: DailyAttendanceDto, which: 'in' | 'out', value: string): void {
    if (row.status == null) return;
    this.save(row, row.status,
      which === 'in' ? value || null : toInputTime(row.checkInTime) || null,
      which === 'out' ? value || null : toInputTime(row.checkOutTime) || null);
  }

  checkOutNow(row: DailyAttendanceDto): void {
    if (row.status == null) return;
    this.save(row, row.status, toInputTime(row.checkInTime) || null, nowTime());
  }

  private save(row: DailyAttendanceDto, status: number, checkIn: string | null, checkOut: string | null): void {
    this.saving.set(row.staffId);
    this.attendanceService.mark({
      societyId: this.societyId, staffId: row.staffId, date: this.date(), status,
      checkInTime: checkIn ? `${checkIn}:00` : null, checkOutTime: checkOut ? `${checkOut}:00` : null, notes: row.notes ?? null
    }).subscribe({
      next: (id) => {
        this.daily.update((rows) => rows.map((r) => r.staffId === row.staffId
          ? { ...r, attendanceId: id, status, checkInTime: checkIn ? `${checkIn}:00` : null, checkOutTime: checkOut ? `${checkOut}:00` : null }
          : r));
        this.saving.set(null);
      },
      error: () => { this.saving.set(null); this.loadDaily(); }
    });
  }

  exportCsv(): void {
    const m = this.monthly();
    if (!m) return;
    const lines = [
      ['Staff', 'Category', 'Present', 'Late', 'Half day', 'Absent', 'Leave', 'Not marked'],
      ...m.rows.map((r) => [r.staffName, this.categoryLabels[r.category], r.present, r.late, r.halfDay, r.absent, r.leave, r.unmarked])
    ].map((cols) => cols.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(','));
    const url = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `staff-attendance-${m.year}-${pad(m.month)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
    this.toast.success('Attendance exported.');
  }
}
