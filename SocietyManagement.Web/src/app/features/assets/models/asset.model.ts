export type AssetCategory = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;
export type AssetPricingType = 1 | 2 | 3 | 4;
export type AssetBookingStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export const ASSET_CATEGORY_LABELS: Record<AssetCategory, string> = {
  1: 'Chairs', 2: 'Tables', 3: 'Speakers', 4: 'Microphones', 5: 'Projectors',
  6: 'Crockery', 7: 'Decoration', 8: 'Fans & Lights', 9: 'Other'
};

export const ASSET_PRICING_TYPE_LABELS: Record<AssetPricingType, string> = {
  1: 'Per Item', 2: 'Per Hour', 3: 'Per Day', 4: 'Lumpsum'
};

export const ASSET_BOOKING_STATUS_LABELS: Record<AssetBookingStatus, string> = {
  1: 'Pending', 2: 'Approved', 3: 'Rejected', 4: 'Cancelled', 5: 'Completed', 6: 'Payment Pending', 7: 'Payment Completed'
};

export interface AssetDto {
  id: number;
  societyId: number;
  name: string;
  category: AssetCategory;
  description?: string | null;
  imageUrl?: string | null;
  totalQuantity: number;
  pricingType: AssetPricingType;
  rentalPrice: number;
  securityDeposit: number;
  damageCharge: number;
  lateReturnCharge: number;
  isActive: boolean;
}

export interface AssetBookingItemDto {
  id: number;
  assetId: number;
  assetName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  quantityIssued: number;
  quantityReturned: number;
  quantityDamaged: number;
  quantityLost: number;
}

export interface AssetBookingDto {
  id: number;
  societyId: number;
  flatId: number;
  flatNumber: string;
  requestedByUserId: number;
  requestedByName: string;
  startDate: string;
  endDate: string;
  notes?: string | null;
  rentalCharge: number;
  securityDepositAmount: number;
  damageChargeAmount: number;
  lateChargeAmount: number;
  totalAmount: number;
  depositRefundAmount: number;
  status: AssetBookingStatus;
  rejectionReason?: string | null;
  facilityBookingId?: number | null;
  createdAt: string;
  items: AssetBookingItemDto[];
}
