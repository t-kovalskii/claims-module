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

/** `GET /api/reference/claim-statuses` (Ticket 03) — consumed by Ticket 06's transition controls;
 * not rendered by this ticket beyond being available on the service. */
export interface ClaimStatusTransition {
  toStatus: string;
  requiredPermission: string;
}

export interface ClaimStatusInfo {
  status: string;
  validNextTransitions: ClaimStatusTransition[];
}
