export type Gender = 1 | 2 | 3; // Male | Female | Other
export type MemberType = 1 | 2 | 3; // Owner | Tenant | FamilyMember
export type VehicleType = 1 | 2; // TwoWheeler | FourWheeler
export type ListingStatus = 1 | 2 | 3 | 4; // Available | UnderNegotiation | Sold | Withdrawn

export const GENDER_LABELS: Record<Gender, string> = { 1: 'Male', 2: 'Female', 3: 'Other' };

export const MEMBER_TYPE_LABELS: Record<MemberType, string> = {
  1: 'Owner', 2: 'Tenant', 3: 'Family Member'
};

export const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  1: 'Two Wheeler', 2: 'Four Wheeler'
};

export const LISTING_STATUS_LABELS: Record<ListingStatus, string> = {
  1: 'Available', 2: 'Under Negotiation', 3: 'Sold', 4: 'Withdrawn'
};

export interface ResidentImportRowResultDto {
  rowNumber: number;
  status: 'Created' | 'Updated' | 'Skipped' | 'Error';
  message: string;
}

export interface ResidentImportResultDto {
  totalRows: number;
  flatsCreated: number;
  membersCreated: number;
  residenciesCreated: number;
  rowsFailed: number;
  rowResults: ResidentImportRowResultDto[];
}

export interface MemberDto {
  id: number;
  societyId: number;
  firstName: string;
  lastName: string;
  phone: string;
  email?: string | null;
  gender?: Gender | null;
  dateOfBirth?: string | null;
  photoUrl?: string | null;
  userId?: number | null;
}

export interface ResidencySummaryDto {
  id: number;
  flatId: number;
  flatNumber: string;
  memberType: MemberType;
  moveInDate: string;
  moveOutDate?: string | null;
  isPrimaryContact: boolean;
}

export interface VehicleDto {
  id: number;
  memberId: number;
  memberName?: string | null;
  vehicleType: VehicleType;
  registrationNumber: string;
  make?: string | null;
  model?: string | null;
  color?: string | null;
  parkingSlotId?: number | null;
  parkingSlotNumber?: string | null;
}

export interface MemberDetailDto extends MemberDto {
  residencies: ResidencySummaryDto[];
  vehicles: VehicleDto[];
}

export interface FlatResidencyDto {
  id: number;
  flatId: number;
  memberId: number;
  memberName: string;
  memberPhone: string;
  memberType: MemberType;
  moveInDate: string;
  moveOutDate?: string | null;
  isPrimaryContact: boolean;
  notes?: string | null;
}

export interface FlatOccupancySummaryDto {
  flatId: number;
  isRented: boolean;
  primaryContact?: FlatResidencyDto | null;
  occupantCount: number;
  currentResidents: FlatResidencyDto[];
}

export interface EmergencyContactDto {
  id: number;
  flatId: number;
  flatNumber: string;
  contactName: string;
  relationship: string;
  phone: string;
  alternatePhone?: string | null;
}

export interface FlatResaleListingDto {
  id: number;
  flatId: number;
  flatNumber: string;
  buildingName: string;
  listedByMemberId: number;
  listedByMemberName: string;
  askingPrice: number;
  availableFrom: string;
  visitTimingNotes?: string | null;
  status: ListingStatus;
  nocRequested: boolean;
  nocIssuedDate?: string | null;
  nocDocumentUrl?: string | null;
  notes?: string | null;
}
