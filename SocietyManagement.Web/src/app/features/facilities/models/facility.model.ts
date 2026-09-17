export type FacilityType = 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type FacilityPricingType = 1 | 2 | 3;
export type FacilityBookingStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type FacilityPaymentStatus = 1 | 2 | 3;

export const FACILITY_TYPE_LABELS: Record<FacilityType, string> = {
  1: 'Club House', 2: 'Community Hall', 3: 'Function Room', 4: 'Refuge Room', 5: 'Party Hall', 6: 'Terrace', 7: 'Other'
};

export const FACILITY_PRICING_TYPE_LABELS: Record<FacilityPricingType, string> = {
  1: 'Per Hour', 2: 'Per Day', 3: 'Lumpsum'
};

export const FACILITY_BOOKING_STATUS_LABELS: Record<FacilityBookingStatus, string> = {
  1: 'Pending', 2: 'Approved', 3: 'Rejected', 4: 'Cancelled', 5: 'Completed', 6: 'Payment Pending', 7: 'Payment Completed'
};

export const FACILITY_PAYMENT_STATUS_LABELS: Record<FacilityPaymentStatus, string> = {
  1: 'Pending', 2: 'Completed', 3: 'Refunded'
};

export interface FacilityDto {
  id: number;
  societyId: number;
  name: string;
  type: FacilityType;
  description?: string | null;
  imageUrl?: string | null;
  location?: string | null;
  capacity: number;
  pricingType: FacilityPricingType;
  pricePerUnit: number;
  securityDeposit: number;
  cleaningCharge: number;
  additionalCharge: number;
  requiresApproval: boolean;
  advanceBookingDaysLimit: number;
  cancellationHoursBeforeStart: number;
  isActive: boolean;
}

export interface FacilityBlackoutDateDto {
  id: number;
  facilityId: number;
  blackoutDate: string;
  reason?: string | null;
}

export interface FacilitySlotDto {
  startTime: string;
  endTime: string;
  status: FacilityBookingStatus;
}

export interface FacilityBookingDto {
  id: number;
  societyId: number;
  facilityId: number;
  facilityName: string;
  flatId: number;
  flatNumber: string;
  bookedByUserId: number;
  bookedByName: string;
  bookingDate: string;
  startTime: string;
  endTime: string;
  purpose?: string | null;
  guestCount: number;
  notes?: string | null;
  rentalCharge: number;
  securityDepositAmount: number;
  cleaningChargeAmount: number;
  additionalChargeAmount: number;
  totalAmount: number;
  depositRefundAmount: number;
  paymentStatus: FacilityPaymentStatus;
  status: FacilityBookingStatus;
  rejectionReason?: string | null;
  createdAt: string;
}
