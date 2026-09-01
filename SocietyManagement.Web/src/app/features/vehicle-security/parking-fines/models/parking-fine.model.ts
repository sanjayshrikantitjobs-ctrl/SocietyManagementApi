export type ParkingFineReason = 1 | 2 | 3;

export const PARKING_FINE_REASON_LABELS: Record<number, string> = {
  1: 'No Parking Zone',
  2: "Wrong Allotted Slot",
  3: 'Other'
};

export interface ParkingFine {
  id: number;
  vehicleId: number;
  registrationNumber: string;
  flatNumber?: string | null;
  parkingSlotNumber?: string | null;
  reason: ParkingFineReason;
  notes?: string | null;
  amount: number;
  photoUrl?: string | null;
  fineDate: string;
  issuedByName: string;
  createdAt: string;
}

export interface CreateParkingFinePayload {
  societyId: number;
  vehicleId: number;
  parkingSlotId?: number | null;
  reason: ParkingFineReason;
  notes?: string | null;
  amount: number;
  fineDate: string;
  photoBytes?: string | null;
}
