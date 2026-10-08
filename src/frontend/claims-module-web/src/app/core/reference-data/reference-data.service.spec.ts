import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { CauseOfLossCode, ClaimStatusInfo } from '../../shared/models/reference-data.model';
import { ReferenceDataService } from './reference-data.service';

describe('ReferenceDataService', () => {
  let service: ReferenceDataService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/reference`;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(ReferenceDataService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getCauseOfLossCodes() fetches active codes, optionally filtered by peril category', () => {
    const codes: CauseOfLossCode[] = [
      { id: 'cause-1', code: 'COLLISION', name: 'Collision', perilCategory: 'Auto', sortOrder: 1 }
    ];

    let actual: CauseOfLossCode[] | undefined;
    service.getCauseOfLossCodes('Auto').subscribe((result) => (actual = result));

    const req = httpMock.expectOne(
      (r) => r.url === `${baseUrl}/cause-of-loss-codes` && r.params.get('perilCategory') === 'Auto'
    );
    req.flush(codes);

    expect(actual).toEqual(codes);
  });

  it('getClaimStatuses() fetches every status with its valid transitions', () => {
    const statuses: ClaimStatusInfo[] = [
      { status: 'Draft', validNextTransitions: [{ toStatus: 'Open', requiredPermission: 'Handler' }] }
    ];

    let actual: ClaimStatusInfo[] | undefined;
    service.getClaimStatuses().subscribe((result) => (actual = result));

    const req = httpMock.expectOne(`${baseUrl}/claim-statuses`);
    req.flush(statuses);

    expect(actual).toEqual(statuses);
  });
});
