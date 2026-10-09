import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApproveReserveRequest,
  ApproveReserveResult,
  ClaimAuditLogEntry,
  ClaimDetail,
  ClaimDocument,
  ClaimDocumentDownloadUrl,
  ClaimsFilter,
  ClaimSummary,
  CreateClaimRequest,
  CreateClaimResult,
  RejectReserveRequest,
  RejectReserveResult,
  RetractReserveResult,
  SubmitReserveRequest,
  SubmitReserveResult,
  TransitionClaimStatusRequest,
  TransitionClaimStatusResult
} from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';

/**
 * Typed seam for Ticket 04's `/api/claims` endpoints (spec.md's frontend Testing Decision: this
 * service's HTTP-to-model translation is what tests exercise — components never call HttpClient
 * directly).
 */
@Injectable({ providedIn: 'root' })
export class ClaimsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/claims`;

  constructor(private readonly http: HttpClient) {}

  list(filter: ClaimsFilter): Observable<PagedResult<ClaimSummary>> {
    let params = new HttpParams().set('page', filter.page).set('pageSize', filter.pageSize);

    if (filter.status) {
      params = params.set('status', filter.status);
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }
    if (filter.assignedHandlerUserId) {
      params = params.set('assignedHandlerUserId', filter.assignedHandlerUserId);
    }
    if (filter.causeOfLossCode) {
      params = params.set('causeOfLossCode', filter.causeOfLossCode);
    }
    if (filter.search) {
      params = params.set('search', filter.search);
    }

    return this.http.get<PagedResult<ClaimSummary>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateClaimRequest): Observable<CreateClaimResult> {
    return this.http.post<CreateClaimResult>(this.baseUrl, request);
  }

  getAuditLog(id: string, page: number, pageSize: number): Observable<PagedResult<ClaimAuditLogEntry>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ClaimAuditLogEntry>>(`${this.baseUrl}/${id}/audit`, { params });
  }

  /** Ticket 06: `PUT /api/claims/{id}/status` (FRS §10.1 / BR-ST-01..04). */
  transitionStatus(id: string, request: TransitionClaimStatusRequest): Observable<TransitionClaimStatusResult> {
    return this.http.put<TransitionClaimStatusResult>(`${this.baseUrl}/${id}/status`, request);
  }

  /** Ticket 07: `POST /api/claims/{id}/reserves` — opens a reserve component or submits a
   * further transaction against an already-open one. */
  submitReserve(id: string, request: SubmitReserveRequest): Observable<SubmitReserveResult> {
    return this.http.post<SubmitReserveResult>(`${this.baseUrl}/${id}/reserves`, request);
  }

  /** Ticket 07: `POST /api/claims/{id}/reserves/{reserveHistoryId}/retract` — the original
   * submitter retracts their own still-pending reserve transaction. */
  retractReserve(id: string, reserveHistoryId: string): Observable<RetractReserveResult> {
    return this.http.post<RetractReserveResult>(`${this.baseUrl}/${id}/reserves/${reserveHistoryId}/retract`, {});
  }

  /** Ticket 08: `POST /api/claims/{id}/reserves/{reserveHistoryId}/approve` — Supervisor/Manager
   * only (server-enforced; the UI also hides the control per `hasRoleAuthority`). */
  approveReserve(id: string, reserveHistoryId: string, request: ApproveReserveRequest): Observable<ApproveReserveResult> {
    return this.http.post<ApproveReserveResult>(`${this.baseUrl}/${id}/reserves/${reserveHistoryId}/approve`, request);
  }

  /** Ticket 08: `POST /api/claims/{id}/reserves/{reserveHistoryId}/reject` — Supervisor/Manager
   * only, with a required rejection reason. */
  rejectReserve(id: string, reserveHistoryId: string, request: RejectReserveRequest): Observable<RejectReserveResult> {
    return this.http.post<RejectReserveResult>(`${this.baseUrl}/${id}/reserves/${reserveHistoryId}/reject`, request);
  }

  /** Ticket 10: `POST /api/claims/{id}/documents` — multipart upload; `documentType` is an
   * optional free-form category. */
  uploadDocument(id: string, file: File, documentType: string | null): Observable<ClaimDocument> {
    const formData = new FormData();
    formData.append('file', file);
    if (documentType) {
      formData.append('documentType', documentType);
    }
    return this.http.post<ClaimDocument>(`${this.baseUrl}/${id}/documents`, formData);
  }

  /** Ticket 10: `GET /api/claims/{id}/documents/{documentId}/download-url` — a short-lived
   * signed URL pointing directly at the storage provider, never streamed through this API. */
  getDocumentDownloadUrl(id: string, documentId: string): Observable<ClaimDocumentDownloadUrl> {
    return this.http.get<ClaimDocumentDownloadUrl>(`${this.baseUrl}/${id}/documents/${documentId}/download-url`);
  }
}
