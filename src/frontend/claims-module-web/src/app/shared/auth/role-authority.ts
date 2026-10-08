import { UserRole } from '../../core/auth/models/authenticated-user.model';

/** Mirrors the backend's `UserRole` enum declaration order (Handler < Supervisor < Manager,
 * ClaimsModule.Domain/Entities/UserRole.cs) — Supervisor/Manager inherit everything a Handler
 * can do, per FRS §3's role capability table. Ticket 06. */
const ROLE_RANK: Record<UserRole, number> = {
  Handler: 0,
  Supervisor: 1,
  Manager: 2
};

/** True when `actual` meets or exceeds the authority of `required`. */
export function hasRoleAuthority(actual: UserRole, required: UserRole): boolean {
  return ROLE_RANK[actual] >= ROLE_RANK[required];
}
