export type PersonRelationship = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;

export const PERSON_RELATIONSHIP_LABELS: Record<PersonRelationship, string> = {
  1: 'Self', 2: 'Spouse', 3: 'Son', 4: 'Daughter', 5: 'Parent', 6: 'Grandparent', 7: 'Sibling', 8: 'Other'
};

export interface OccupancyMember {
  id: number;
  personId: number;
  personName: string;
  phone?: string | null;
  email?: string | null;
  whatsAppNumber?: string | null;
  photoUrl?: string | null;
  relationship: PersonRelationship;
  isPrimary: boolean;
  residentStatus: number;
  joinedDate: string;
  leftDate?: string | null;
}
