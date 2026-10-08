export type UserRole = 'Handler' | 'Supervisor' | 'Manager';

export interface AuthenticatedUser {
  token: string;
  userId: string;
  username: string;
  displayName: string;
  role: UserRole;
  organizationId: string;
}

export type LoginResponse = AuthenticatedUser;
