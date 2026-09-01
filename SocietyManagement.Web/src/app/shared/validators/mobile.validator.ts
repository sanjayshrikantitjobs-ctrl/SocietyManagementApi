import { ValidatorFn, Validators } from '@angular/forms';

/** Indian mobile number: exactly 10 digits, first digit 6-9. Mirrors the
 * backend's StringExtensions.IsValidIndianMobile(). */
export const MOBILE_PATTERN = /^[6-9]\d{9}$/;

export const MOBILE_PATTERN_ERROR = 'Enter a valid 10-digit mobile number.';

/** Reactive-forms validator for a required mobile number field. */
export function mobileValidator(): ValidatorFn[] {
  return [Validators.required, Validators.pattern(MOBILE_PATTERN)];
}

/** Reactive-forms validator for an optional mobile number field (e.g. WhatsApp
 * number) — only enforces the pattern when a value is present. */
export function optionalMobileValidator(): ValidatorFn[] {
  return [Validators.pattern(MOBILE_PATTERN)];
}
