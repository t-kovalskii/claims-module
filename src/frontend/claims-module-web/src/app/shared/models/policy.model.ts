/** `GET /api/policies/search` (Ticket 03). */
export interface PolicySummary {
  id: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  status: string;
  coverageTypes: string[];
}

/** `GET /api/policies/{id}/coverage` (Ticket 03) — not called directly by the FNOL wizard today,
 * since `PolicySummary.coverageTypes` already carries what Step 1 needs to display; kept on the
 * service as the typed seam for a later "view full coverage detail" extension. */
export interface PolicyCoverage {
  id: string;
  coverageType: string;
}
