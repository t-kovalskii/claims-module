/** FRS §9.3 / BR-P-02 — mirrors the backend's `ClaimPartyRole` enum (JSON string, Ticket 04). */
export type ClaimPartyRole = 'Claimant' | 'Insured' | 'ThirdParty' | 'Witness' | 'Attorney';

/** Mirrors the backend's `ClaimPartyType` enum. */
export type ClaimPartyType = 'Person' | 'Company';

/** Mirrors the backend's `ClaimRiskObjectAssetType` enum. */
export type ClaimRiskObjectAssetType = 'Vehicle' | 'Property' | 'Person' | 'Equipment' | 'Other';

/** FRS §6.2 / CONTEXT.md — mirrors the backend's `ReserveComponentType` enum (Ticket 07). Only
 * `SubrogationRecoverable` may hold a negative amount. */
export type ReserveComponentType = 'Indemnity' | 'Expense' | 'ALAE' | 'SubrogationRecoverable';

/** FRS §9.5's component lifecycle flag — distinct from the per-transaction `ReserveApprovalStatus`
 * below. This slice never produces `Closed`. */
export type ReserveComponentStatus = 'Active' | 'Closed';

/** Mirrors the backend's `ReserveApprovalStatus` enum (Ticket 07). `Approved`/`Rejected` are only
 * ever set by Ticket 08's approval workflow; this ticket's own commands only ever produce
 * `AutoApproved`, `PendingApproval`, or `Cancelled` (via retraction). */
export type ReserveApprovalStatus = 'AutoApproved' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';

/** Mirrors the backend's `ReserveTransactionType` enum. */
export type ReserveTransactionType = 'Add' | 'Adjust' | 'Reverse';

/** Mirrors the backend's `ReservePostingStatus` enum (Ticket 08 sets anything beyond `Pending`). */
export type ReservePostingStatus = 'Pending' | 'Posted' | 'Failed' | 'Cancelled';

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

/** Ticket 07: one row per `ReserveComponent`. `approvalStatus`/`pendingAmount` are computed by
 * the backend from the latest entry in `history` — not persisted fields on the component itself
 * (that's `status`, the FRS §9.5 Active/Closed lifecycle flag). */
export interface ReserveComponent {
  id: string;
  componentType: ReserveComponentType;
  currentAmount: number;
  status: ReserveComponentStatus;
  approvalStatus: ReserveApprovalStatus | null;
  pendingAmount: number | null;
  history: ReserveHistoryEntry[];
}

/** Ticket 07: one row per `ReserveHistory` transaction, reverse-chronological (highest
 * `changeSequence` first) within its component. */
export interface ReserveHistoryEntry {
  id: string;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  changeReason: string | null;
  postingStatus: ReservePostingStatus;
  submittedByUserId: string | null;
  approvedByUserId: string | null;
  approvedAt: string | null;
  rejectedByUserId: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  changeSequence: number;
  createdAt: string;
}

/** `GET /api/claims/{id}` (Ticket 04). `documents` is typed `unknown[]` — the backend DTO itself
 * types it `List<object>` as a placeholder (always `[]`) until Ticket 10 gives it a real shape.
 * `reserves` is now a real typed array (Ticket 07, replacing the `unknown[]` placeholder). */
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
  reserves: ReserveComponent[];
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

/** `POST /api/claims/{id}/reserves` body (Ticket 07). No `transactionType` field — the backend
 * derives Add-vs-Adjust itself from whether the component already exists on the claim; see
 * `SubmitReserveTransactionCommand`'s remarks. */
export interface SubmitReserveRequest {
  componentType: ReserveComponentType;
  amount: number;
  changeReason: string | null;
}

/** FRS §10.2: "the created transaction and its approval status." */
export interface SubmitReserveResult {
  reserveComponentId: string;
  reserveHistoryId: string;
  componentType: string;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  currentAmount: number;
  changeSequence: number;
}

/** `POST /api/claims/{id}/reserves/{reserveHistoryId}/retract` response (Ticket 07). */
export interface RetractReserveResult {
  reserveComponentId: string;
  reserveHistoryId: string;
  currentAmount: number;
}

/** `POST /api/claims/{id}/reserves/{reserveHistoryId}/approve` body (Ticket 08). Optional and
 * entirely omittable; `overrideAggregateCap` is ignored unless this approval would actually
 * breach the $10,000,000 cap (BR-R-05) — only a Manager setting it true then has any effect. */
export interface ApproveReserveRequest {
  overrideAggregateCap: boolean;
}

export interface ApproveReserveResult {
  reserveComponentId: string;
  reserveHistoryId: string;
  approvalStatus: ReserveApprovalStatus;
  currentAmount: number;
}

/** `POST /api/claims/{id}/reserves/{reserveHistoryId}/reject` body (Ticket 08). */
export interface RejectReserveRequest {
  rejectionReason: string;
}

export interface RejectReserveResult {
  reserveComponentId: string;
  reserveHistoryId: string;
  approvalStatus: ReserveApprovalStatus;
}
