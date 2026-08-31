import { VehicleType } from '../../residents/models/resident.model';

export type VehicleScanSource = 1 | 2; // OcrCamera | ManualSearch
export type VehicleScanResultStatus = 1 | 2; // Matched | NotRegistered

export const SCAN_SOURCE_LABELS: Record<VehicleScanSource, string> = {
  1: 'Gate Entry', 2: 'Manual Search'
};

export const SCAN_RESULT_LABELS: Record<VehicleScanResultStatus, string> = {
  1: 'Registered', 2: 'Not Registered'
};

/** Owner fields are only populated by the API when the caller holds
 * Vehicles.ViewOwnerDetails — Watchman logins get undefined here, not an
 * error, so the UI just omits that section entirely. */
export interface VehicleScanResultDto {
  scanLogId: number;
  result: VehicleScanResultStatus;
  registrationNumber: string;

  vehicleId?: number | null;
  vehicleType?: VehicleType | null;
  make?: string | null;
  model?: string | null;
  color?: string | null;

  flatId?: number | null;
  flatNumber?: string | null;
  wingName?: string | null;
  buildingName?: string | null;

  parkingSlotNumber?: string | null;
  parkingStatus?: number | null;

  ownerName?: string | null;
  ownerPhone?: string | null;
  ownerEmail?: string | null;
}

export interface VehicleSearchItemDto {
  vehicleId: number;
  registrationNumber: string;
  vehicleType: VehicleType;
  flatNumber?: string | null;
}

/** Ephemeral OCR-assist result for the drag-to-crop step — never persisted
 * by itself. NormalizedText is a prefill suggestion, always left editable. */
export interface PlateOcrResultDto {
  recognizedText: string;
  normalizedText: string;
  confidence: number;
}

export interface VehicleScanHistoryDto {
  id: number;
  scannedAt: string;
  source: VehicleScanSource;
  normalizedRegistrationNumber: string;
  confidence?: number | null;
  result: VehicleScanResultStatus;
  scannedByName: string;
  gateName?: string | null;
  imageUrl?: string | null;
}
