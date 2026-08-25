export interface CommitteeMember {
  id: number;
  societyId: number;
  name: string;
  designation: string;
  flatNumber?: string | null;
  phone?: string | null;
  email?: string | null;
  displayOrder: number;
}
