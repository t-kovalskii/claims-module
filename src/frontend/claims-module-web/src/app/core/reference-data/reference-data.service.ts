import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CauseOfLossCode, ClaimStatusInfo, PerilCategory } from '../../shared/models/reference-data.model';

/** Typed seam for Ticket 03's `/api/reference` endpoints, consumed by the FNOL wizard's
 * cause-of-loss dropdown (Step 1) and available for Ticket 06's status-transition controls. */
@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly baseUrl = `${environment.apiBaseUrl}/reference`;

  constructor(private readonly http: HttpClient) {}

  getCauseOfLossCodes(perilCategory?: PerilCategory): Observable<CauseOfLossCode[]> {
    let params = new HttpParams();
    if (perilCategory) {
      params = params.set('perilCategory', perilCategory);
    }
    return this.http.get<CauseOfLossCode[]>(`${this.baseUrl}/cause-of-loss-codes`, { params });
  }

  getClaimStatuses(): Observable<ClaimStatusInfo[]> {
    return this.http.get<ClaimStatusInfo[]>(`${this.baseUrl}/claim-statuses`);
  }
}
