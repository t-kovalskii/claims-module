import { ClaimStatus } from './claim.model';
import { UserRole } from '../../core/auth/models/authenticated-user.model';

/** FRS §9 data model — mirrors the backend's `PerilCategory` enum. */
export type PerilCategory = 'Property' | 'Auto' | 'Liability' | 'Weather' | 'Equipment' | 'Crime' | 'General';

/** `GET /api/reference/cause-of-loss-codes` (Ticket 03). */
export interface CauseOfLossCode {
  id: string;
  code: string;
  name: string;
  perilCategory: string;
  sortOrder: number;
}

/** `GET /api/reference/claim-statuses` (Ticket 03) — consumed by Ticket 06's transition control
 * to compute the current claim's valid next statuses and the role each one requires. */
export interface ClaimStatusTransition {
  toStatus: ClaimStatus;
  requiredPermission: UserRole;
}

export interface ClaimStatusInfo {
  status: ClaimStatus;
  validNextTransitions: ClaimStatusTransition[];
}
