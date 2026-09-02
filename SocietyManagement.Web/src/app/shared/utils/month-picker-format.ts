import { MatDateFormats } from '@angular/material/core';

/** For the month-only `<mat-datepicker startView="year" (monthSelected)="...">`
 * pattern used across Maintenance (Bills, Dashboard, Water Tanker) — shows
 * "September 2026" in the input instead of the app's default full-date
 * format, which would otherwise render a misleading day-of-month that was
 * never actually chosen. Provide via `{ provide: MAT_DATE_FORMATS, useValue:
 * MONTH_YEAR_FORMATS }` on any component using that pattern. */
// The app uses provideNativeDateAdapter() (see app.config.ts), whose format
// values are Intl.DateTimeFormatOptions objects, not Moment-style tokens.
export const MONTH_YEAR_FORMATS: MatDateFormats = {
  parse: { dateInput: { year: 'numeric', month: 'short' } },
  display: {
    dateInput: { year: 'numeric', month: 'long' },
    monthYearLabel: { year: 'numeric', month: 'short' },
    dateA11yLabel: { year: 'numeric', month: 'long' },
    monthYearA11yLabel: { year: 'numeric', month: 'long' }
  }
};
