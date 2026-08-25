export type StaffCategory = 1 | 2 | 3 | 4 | 5 | 6; // Watchman|Sweeper|Gardener|Electrician|Plumber|Other

export const STAFF_CATEGORY_LABELS: Record<StaffCategory, string> = {
  1: 'Watchman', 2: 'Sweeper', 3: 'Gardener', 4: 'Electrician', 5: 'Plumber', 6: 'Other'
};

export interface StaffDto {
  id: number;
  societyId: number;
  firstName: string;
  lastName: string;
  category: StaffCategory;
  phone: string;
  email?: string | null;
  address?: string | null;
  joiningDate: string;
  joiningDocumentUrl?: string | null;
  photoUrl?: string | null;
  salary: number;
  salaryPayDay: number;
  isActive: boolean;
}
