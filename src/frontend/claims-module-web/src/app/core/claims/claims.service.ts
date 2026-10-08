import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClaimAuditLogEntry,
  ClaimDetail,
  ClaimsFilter,
  ClaimSummary,
  CreateClaimRequest,
  CreateClaimResult
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
}
