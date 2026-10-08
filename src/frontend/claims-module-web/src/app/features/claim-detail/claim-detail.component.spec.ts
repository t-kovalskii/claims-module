import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { environment } from '../../../environments/environment';
import { ClaimAuditLogEntry, ClaimDetail } from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { ClaimDetailComponent } from './claim-detail.component';

describe('ClaimDetailComponent', () => {
  let fixture: ComponentFixture<ClaimDetailComponent>;
  let httpMock: HttpTestingController;
  const claimId = 'claim-1';
  const baseUrl = `${environment.apiBaseUrl}/claims`;

  const detail: ClaimDetail = {
    id: claimId,
    claimNumber: 'CLM-2026-0000001',
    status: 'Open',
    policyId: 'policy-1',
    policyNumber: 'POL-1',
    clientName: 'Jane Doe',
    assignedHandlerUserId: 'user-1',
    lossEvent: {
      lossDate: '2026-01-05T00:00:00Z',
      lossDescription: 'A rear-end collision occurred on the highway during rain.',
      lossLocation: 'I-95',
      causeOfLossCodeId: 'cause-1',
      causeOfLossCode: 'COLLISION',
      causeOfLossName: 'Collision',
      estimatedLossAmount: 5000,
      policeReportNumber: 'PR-123'
    },
    parties: [
      {
        id: 'party-1',
        role: 'Claimant',
        partyType: 'Person',
        firstName: 'Jane',
        lastName: 'Doe',
        companyName: null,
        email: 'jane@example.com',
        phone: null,
        notes: null,
        isActive: true
      }
    ],
    riskObjects: [
      {
        id: 'risk-1',
        assetType: 'Vehicle',
        assetDescription: '2022 Honda Civic',
        damageDescription: 'Rear bumper damage',
        isPrimary: true,
        assetReference: null
      }
    ],
    reserves: [],
    documents: [],
    createdAt: '2026-01-05T00:00:00Z'
  };

  const auditPage: PagedResult<ClaimAuditLogEntry> = {
    items: [
      {
        id: 'audit-1',
        eventType: 'ClaimCreated',
        description: 'Claim created',
        oldValue: null,
        newValue: null,
        relatedEntityId: null,
        relatedEntityType: null,
        createdAt: '2026-01-05T00:00:00Z',
        createdByUserId: 'user-1'
      }
    ],
    totalCount: 1,
    page: 1,
    pageSize: 50
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimDetailComponent, HttpClientTestingModule],
      providers: [
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => claimId } } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimDetailComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads claim detail and audit log, rendering the header and Overview tab', () => {
    fixture.detectChanges();

    httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
    httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('CLM-2026-0000001');
    expect(text).toContain('POL-1');
    expect(text).toContain('Collision');
    expect(text).toContain('Jane Doe');
    expect(text).toContain('2022 Honda Civic');
  });

  it('renders the reverse-chronological audit log on the Audit Log tab', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
    httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
    fixture.detectChanges();

    expect(fixture.componentInstance.auditLog().length).toBe(1);
    expect(fixture.componentInstance.auditLog()[0].eventType).toBe('ClaimCreated');
  });

  it('shows empty-state placeholders for the not-yet-implemented Reserves/Documents tabs', fakeAsync(() => {
    fixture.detectChanges();
    httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
    httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
    fixture.detectChanges();

    // Material lazily attaches tab body content for the selected tab only — switch selection
    // via the real MatTabGroup instance rather than simulating label clicks, which depend on
    // animation-frame timing this test doesn't otherwise need to exercise.
    const tabGroup = fixture.componentInstance.tabGroup;
    expect(tabGroup).toBeDefined();

    tabGroup!.selectedIndex = 1; // Reserves
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reserve management is not yet implemented');

    tabGroup!.selectedIndex = 2; // Documents
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Document upload/download is not yet implemented');
  }));
});
