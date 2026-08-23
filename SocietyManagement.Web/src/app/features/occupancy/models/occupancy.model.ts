export type Gender = 1 | 2 | 3; // Male | Female | Other
export type OccupancyType = 1 | 2; // Owner | Tenant
export type PersonRelationship = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8; // Self..Other
export type ResidentStatus = 1 | 2; // Residing | NotResiding
export type PoliceVerificationStatus = 1 | 2; // Pending | Done

export const OCCUPANCY_TYPE_LABELS: Record<OccupancyType, string> = { 1: 'Owner', 2: 'Tenant' };

export const PERSON_RELATIONSHIP_LABELS: Record<PersonRelationship, string> = {
  1: 'Self', 2: 'Spouse', 3: 'Son', 4: 'Daughter', 5: 'Parent', 6: 'Grandparent', 7: 'Sibling', 8: 'Other'
};

export const RESIDENT_STATUS_LABELS: Record<ResidentStatus, string> = { 1: 'Residing', 2: 'Not Residing' };

export const POLICE_VERIFICATION_LABELS: Record<PoliceVerificationStatus, string> = { 1: 'Pending', 2: 'Done' };

export interface PersonDto {
  id: number;
  societyId: number;
  firstName: string;
  lastName: string;
  phone: string;
  email?: string | null;
  gender?: Gender | null;
  dateOfBirth?: string | null;
  photoUrl?: string | null;
  aadhaarNumber?: string | null;
  panNumber?: string | null;
}

export interface OccupancyMembershipSummaryDto {
  flatOccupancyId: number;
  flatId: number;
  flatNumber: string;
  type: OccupancyType;
  relationship: PersonRelationship;
  isPrimary: boolean;
  joinedDate: string;
  leftDate?: string | null;
}

export interface PersonDetailDto extends PersonDto {
  memberships: OccupancyMembershipSummaryDto[];
}

export interface OccupancyMemberDto {
  id: number;
  personId: number;
  personName: string;
  phone: string;
  email?: string | null;
  photoUrl?: string | null;
  relationship: PersonRelationship;
  isPrimary: boolean;
  residentStatus: ResidentStatus;
  joinedDate: string;
  leftDate?: string | null;
}

export interface RentalAgreementDto {
  id: number;
  flatOccupancyId: number;
  agreementStartDate: string;
  agreementEndDate: string;
  securityDeposit: number;
  rentAmount?: number | null;
  policeVerificationStatus: PoliceVerificationStatus;
  policeVerificationReference?: string | null;
  agreementDocumentUrl?: string | null;
}

export interface FlatOccupancyDto {
  id: number;
  flatId: number;
  type: OccupancyType;
  startDate: string;
  endDate?: string | null;
  notes?: string | null;
  members: OccupancyMemberDto[];
  rentalAgreement?: RentalAgreementDto | null;
}

export interface FlatOccupancyOverviewDto {
  flatId: number;
  currentOwnerOccupancy?: FlatOccupancyDto | null;
  currentTenantOccupancy?: FlatOccupancyDto | null;
}

export interface OccupancySettingsDto {
  societyId: number;
  allowMultiplePrimaryOwners: boolean;
}

/** Shared "reuse existing person, or fill these fields for a new one"
 * shape used by every Add Owner Member / Add Tenant / Add Family Member
 * dialog and service call. */
export interface PersonFieldsPayload {
  personId?: number | null;
  firstName?: string | null;
  lastName?: string | null;
  phone?: string | null;
  email?: string | null;
  gender?: Gender | null;
  dateOfBirth?: string | null;
  photoUrl?: string | null;
  aadhaarNumber?: string | null;
  panNumber?: string | null;
}
