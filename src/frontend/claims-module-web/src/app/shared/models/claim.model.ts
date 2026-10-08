/** FRS §9.3 / BR-P-02 — mirrors the backend's `ClaimPartyRole` enum (JSON string, Ticket 04). */
export type ClaimPartyRole = 'Claimant' | 'Insured' | 'ThirdParty' | 'Witness' | 'Attorney';

/** Mirrors the backend's `ClaimPartyType` enum. */
export type ClaimPartyType = 'Person' | 'Company';

/** Mirrors the backend's `ClaimRiskObjectAssetType` enum. */
export type ClaimRiskObjectAssetType = 'Vehicle' | 'Property' | 'Person' | 'Equipment' | 'Other';

/** All claim statuses a `ClaimSummary`/`ClaimDetail` may report (`ClaimStatus` enum, as a string). */
export type ClaimStatus =
  | 'Draft'
  | 'Open'
  | 'UnderInvestigation'
  | 'PendingPayment'
  | 'Closed'
  | 'Reopened'
  | 'Withdrawn';

/** `GET /api/claims` row shape (Ticket 04). `totalReserves` is always 0 until Ticket 07 populates
 * real reserve aggregates server-side. */
export interface ClaimSummary {
  id: string;
  claimNumber: string;
  clientName: string;
  policyNumber: string | null;
  lossDate: string;
  status: ClaimStatus;
  causeOfLossCode: string;
  totalReserves: number;
}

/** Server-side filters accepted by `GET /api/claims` (Ticket 04). Note: no `policyId` filter —
 * flagged as a genuine backend gap in Ticket 04's Comments; not added here since this ticket is
 * frontend-only. */
export interface ClaimsFilter {
  status?: ClaimStatus | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  assignedHandlerUserId?: string | null;
  causeOfLossCode?: string | null;
  search?: string | null;
  page: number;
  pageSize: number;
}

export interface LossEvent {
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCodeId: string;
  causeOfLossCode: string;
  causeOfLossName: string;
  estimatedLossAmount: number | null;
  policeReportNumber: string | null;
}

export interface ClaimParty {
  id: string;
  role: ClaimPartyRole;
  partyType: ClaimPartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
  isActive: boolean;
}

export interface ClaimRiskObject {
  id: string;
  assetType: ClaimRiskObjectAssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

/** `GET /api/claims/{id}` (Ticket 04). `reserves`/`documents` are typed `unknown[]` — the backend
 * DTO itself types them `List<object>` as a placeholder (always `[]`) until Tickets 07/10 give
 * them real shapes. The Reserves/Documents tabs in this ticket render only an empty-state off
 * these arrays; Tickets 07/10 should replace this type with real typed arrays once those DTOs
 * exist, matching whatever the backend starts returning. */
export interface ClaimDetail {
  id: string;
  claimNumber: string;
  status: ClaimStatus;
  policyId: string | null;
  policyNumber: string | null;
  clientName: string;
  assignedHandlerUserId: string | null;
  lossEvent: LossEvent;
  parties: ClaimParty[];
  riskObjects: ClaimRiskObject[];
  reserves: unknown[];
  documents: unknown[];
  createdAt: string;
}

export interface ClaimAuditLogEntry {
  id: string;
  eventType: string;
  description: string;
  oldValue: string | null;
  newValue: string | null;
  relatedEntityId: string | null;
  relatedEntityType: string | null;
  createdAt: string;
  createdByUserId: string | null;
}

export interface CreateClaimLossEventRequest {
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCodeId: string;
  estimatedLossAmount: number | null;
  policeReportNumber: string | null;
}

export interface CreateClaimPartyRequest {
  role: ClaimPartyRole;
  partyType: ClaimPartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
}

export interface CreateClaimRiskObjectRequest {
  assetType: ClaimRiskObjectAssetType;
  assetDescription: string;
  damageDescription: string | null;
  isPrimary: boolean;
  assetReference: string | null;
}

/** `POST /api/claims` body (Ticket 04's exact contract, per its Comments). */
export interface CreateClaimRequest {
  policyId: string | null;
  lossEvent: CreateClaimLossEventRequest;
  assignedHandlerUserId: string | null;
  parties: CreateClaimPartyRequest[];
  riskObjects: CreateClaimRiskObjectRequest[];
}

export interface CreateClaimResult {
  id: string;
  claimNumber: string;
  status: ClaimStatus;
  warnings: string[];
}

/** `PUT /api/claims/{id}/status` body (Ticket 06, FRS §10.1 / BR-ST-01..04). `reason` is required
 * by the backend only for Reopened (BR-ST-04) and Withdrawn (FRS §4.2) target statuses. */
export interface TransitionClaimStatusRequest {
  targetStatus: ClaimStatus;
  reason: string | null;
}

/** Deliberately minimal — the claim-detail screen re-fetches full claim detail after a
 * successful transition rather than relying on this response's shape. */
export interface TransitionClaimStatusResult {
  id: string;
  status: ClaimStatus;
}
