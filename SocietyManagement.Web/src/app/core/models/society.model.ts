export interface Society {
  id: number;
  name: string;
  code?: string | null;
  registrationNumber?: string | null;
  address: string;
  city: string;
  state: string;
  pincode: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  logoUrl?: string | null;
  establishedDate?: string | null;
  subscriptionStartDate: string;
  subscriptionEndDate: string;
  isSubscriptionSuspended: boolean;
  buildingCount: number;
}

export interface Building {
  id: number;
  societyId: number;
  name: string;
  description?: string | null;
  wingCount: number;
}

export interface Wing {
  id: number;
  buildingId: number;
  name: string;
  floorCount: number;
}

export interface Floor {
  id: number;
  wingId: number;
  floorNumber: number;
  name?: string | null;
  flatCount: number;
}

export type FlatType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;
export type FlatStatus = 1 | 2 | 3;

export const FLAT_TYPE_LABELS: Record<FlatType, string> = {
  1: '1 RK', 2: '1 BHK', 3: '2 BHK', 4: '3 BHK', 5: '4 BHK',
  6: 'Duplex', 7: 'Penthouse', 8: 'Shop', 9: 'Office'
};

export const FLAT_STATUS_LABELS: Record<FlatStatus, string> = {
  1: 'Vacant', 2: 'Occupied', 3: 'Under Maintenance'
};

export interface Flat {
  id: number;
  floorId: number;
  flatNumber: string;
  flatType: FlatType;
  areaSqFt?: number | null;
  status: FlatStatus;
  ownerName?: string | null;
  ownerPhone?: string | null;
  ownerEmail?: string | null;
}

export type ParkingType = 1 | 2 | 3;
export type ParkingStatus = 1 | 2 | 3;

export interface ParkingSlot {
  id: number;
  societyId: number;
  slotNumber: string;
  type: ParkingType;
  status: ParkingStatus;
  allocatedFlatId?: number | null;
  allocatedFlatNumber?: string | null;
}
