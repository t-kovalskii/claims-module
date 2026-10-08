import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ClaimAuditLogEntry, ClaimDetail, ClaimSummary, CreateClaimRequest, CreateClaimResult } from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { ClaimsService } from './claims.service';

describe('ClaimsService', () => {
  let service: ClaimsService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/claims`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(ClaimsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('list() sends only the filters that are set and translates the paged response', () => {
    const response: PagedResult<ClaimSummary> = {
      items: [
        {
          id: 'claim-1',
          claimNumber: 'CLM-2026-0000001',
          clientName: 'Jane Doe',
          policyNumber: 'POL-1',
          lossDate: '2026-01-01T00:00:00Z',
          status: 'Open',
          causeOfLossCode: 'COLLISION',
          totalReserves: 0
        }
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20
    };

    let actual: PagedResult<ClaimSummary> | undefined;
    service
      .list({ status: 'Open', page: 1, pageSize: 20 })
      .subscribe((result) => (actual = result));

    const req = httpMock.expectOne(
      (r) => r.url === baseUrl && r.params.get('status') === 'Open' && r.params.get('page') === '1'
    );
    expect(req.request.params.has('dateFrom')).toBeFalse();
    req.flush(response);

    expect(actual).toEqual(response);
  });

  it('getById() fetches claim detail by id', () => {
    const detail: ClaimDetail = {
      id: 'claim-1',
      claimNumber: 'CLM-2026-0000001',
      status: 'Draft',
      policyId: null,
      policyNumber: null,
      clientName: 'Jane Doe',
      assignedHandlerUserId: null,
      lossEvent: {
        lossDate: '2026-01-01T00:00:00Z',
        lossDescription: 'A collision occurred on the highway during rain.',
        lossLocation: null,
        causeOfLossCodeId: 'cause-1',
        causeOfLossCode: 'COLLISION',
        causeOfLossName: 'Collision',
        estimatedLossAmount: null,
        policeReportNumber: null
      },
      parties: [],
      riskObjects: [],
      reserves: [],
      documents: [],
      createdAt: '2026-01-01T00:00:00Z'
    };

    let actual: ClaimDetail | undefined;
    service.getById('claim-1').subscribe((result) => (actual = result));

    const req = httpMock.expectOne(`${baseUrl}/claim-1`);
    expect(req.request.method).toBe('GET');
    req.flush(detail);

    expect(actual).toEqual(detail);
  });

  it('create() posts the exact Ticket 04 request shape and returns the created claim', () => {
    const request: CreateClaimRequest = {
      policyId: null,
      lossEvent: {
        lossDate: '2026-01-01T00:00:00Z',
        lossDescription: 'A collision occurred on the highway during rain.',
        lossLocation: 'Main St',
        causeOfLossCodeId: 'cause-1',
        estimatedLossAmount: 1000,
        policeReportNumber: null
      },
      assignedHandlerUserId: 'user-1',
      parties: [
        {
          role: 'Claimant',
          partyType: 'Person',
          firstName: 'Jane',
          lastName: 'Doe',
          companyName: null,
          email: null,
          phone: null,
          notes: null
        }
      ],
      riskObjects: []
    };

    const result: CreateClaimResult = { id: 'claim-1', claimNumber: 'CLM-2026-0000001', status: 'Draft', warnings: [] };

    let actual: CreateClaimResult | undefined;
    service.create(request).subscribe((response) => (actual = response));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(result);

    expect(actual).toEqual(result);
  });

  it('getAuditLog() paginates and returns the audit entries', () => {
    const page: PagedResult<ClaimAuditLogEntry> = {
      items: [
        {
          id: 'audit-1',
          eventType: 'ClaimCreated',
          description: 'Claim created',
          oldValue: null,
          newValue: null,
          relatedEntityId: null,
          relatedEntityType: null,
          createdAt: '2026-01-01T00:00:00Z',
          createdByUserId: 'user-1'
        }
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20
    };

    let actual: PagedResult<ClaimAuditLogEntry> | undefined;
    service.getAuditLog('claim-1', 1, 20).subscribe((result) => (actual = result));

    const req = httpMock.expectOne((r) => r.url === `${baseUrl}/claim-1/audit`);
    expect(req.request.params.get('page')).toBe('1');
    req.flush(page);

    expect(actual).toEqual(page);
  });
});
