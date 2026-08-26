/** mat-datepicker binds to a JS Date object, but every date-only field in this
 * app (DOB, move-in date, filters, ...) is stored/sent as a plain "yyyy-MM-dd"
 * string — converting via `.toISOString()` shifts the date by a day whenever
 * the local timezone is ahead of UTC (e.g. IST), since it renders midnight
 * local time as the previous day in UTC. Building the string from the Date's
 * local getters instead avoids that shift entirely. */
export function toDateOnlyString(value: Date | string | null | undefined): string | null {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(value);
  if (isNaN(date.getTime())) return null;
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Inverse of toDateOnlyString — parses a "yyyy-MM-dd" (or any ISO datetime)
 * string from the API into a local Date for binding to mat-datepicker. */
export function parseDateOnly(value: string | null | undefined): Date | null {
  if (!value) return null;
  const datePart = value.substring(0, 10);
  const [year, month, day] = datePart.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(year, month - 1, day);
}
