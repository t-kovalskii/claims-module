import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { PolicyCoverage, PolicySummary } from '../../shared/models/policy.model';
import { PoliciesService } from './policies.service';

describe('PoliciesService', () => {
  let service: PoliciesService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/policies`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(PoliciesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('search() sends the q param only when provided', () => {
    const results: PolicySummary[] = [
      {
        id: 'policy-1',
        policyNumber: 'POL-1',
        clientName: 'Jane Doe',
        effectiveDate: '2026-01-01',
        expirationDate: '2026-12-31',
        status: 'Active',
        coverageTypes: ['Collision']
      }
    ];

    let actual: PolicySummary[] | undefined;
    service.search('Jane').subscribe((result) => (actual = result));

    const req = httpMock.expectOne((r) => r.url === `${baseUrl}/search` && r.params.get('q') === 'Jane');
    req.flush(results);

    expect(actual).toEqual(results);
  });

  it('search() omits q when not provided', () => {
    service.search().subscribe();

    const req = httpMock.expectOne(`${baseUrl}/search`);
    expect(req.request.params.has('q')).toBeFalse();
    req.flush([]);
  });

  it('getCoverage() fetches coverages for a policy id', () => {
    const coverages: PolicyCoverage[] = [{ id: 'cov-1', coverageType: 'Collision' }];

    let actual: PolicyCoverage[] | undefined;
    service.getCoverage('policy-1').subscribe((result) => (actual = result));

    const req = httpMock.expectOne(`${baseUrl}/policy-1/coverage`);
    req.flush(coverages);

    expect(actual).toEqual(coverages);
  });
});
