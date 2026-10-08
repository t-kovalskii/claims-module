import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PolicyCoverage, PolicySummary } from '../../shared/models/policy.model';

/** Typed seam for Ticket 03's `/api/policies` endpoints, consumed by the FNOL wizard's policy
 * typeahead (Step 1). */
@Injectable({ providedIn: 'root' })
export class PoliciesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/policies`;

  constructor(private readonly http: HttpClient) {}

  search(q?: string): Observable<PolicySummary[]> {
    let params = new HttpParams();
    if (q) {
      params = params.set('q', q);
    }
    return this.http.get<PolicySummary[]>(`${this.baseUrl}/search`, { params });
  }

  getCoverage(policyId: string): Observable<PolicyCoverage[]> {
    return this.http.get<PolicyCoverage[]>(`${this.baseUrl}/${policyId}/coverage`);
  }
}
