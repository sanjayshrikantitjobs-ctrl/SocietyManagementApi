export interface UserProfile {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber: string;
  profilePhotoUrl?: string | null;
  roleName: string;
  /** Null for Super Admin (no tenant boundary). */
  societyId?: number | null;
  permissions: string[];
  mustChangePassword: boolean;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}

export interface UserListItem {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber: string;
  profilePhotoUrl?: string | null;
  roleId: number;
  roleName: string;
  societyId?: number | null;
  societyName?: string | null;
  isActive: boolean;
  isLocked: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
}

export interface RoleListItem {
  id: number;
  name: string;
  description?: string | null;
  isSystemRole: boolean;
  userCount: number;
}

export interface RoleDetail {
  id: number;
  name: string;
  description?: string | null;
  isSystemRole: boolean;
  permissionIds: number[];
}

export interface PermissionItem {
  id: number;
  module: string;
  action: string;
  code: string;
  description?: string | null;
}
