export interface SocietyServiceDto {
  id: number;
  societyId: number;
  serviceName: string;
  vendorName: string;
  contactPerson?: string | null;
  contactNumber: string;
  email?: string | null;
  renewalDate: string;
  notes?: string | null;
  isActive: boolean;
}

export interface ExpiringServiceDto {
  id: number;
  serviceName: string;
  vendorName: string;
  renewalDate: string;
  daysRemaining: number;
}
