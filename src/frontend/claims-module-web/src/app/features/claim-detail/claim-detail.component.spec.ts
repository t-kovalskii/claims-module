import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { OverlayContainer } from '@angular/cdk/overlay';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthenticatedUser } from '../../core/auth/models/authenticated-user.model';
import { ClaimAuditLogEntry, ClaimDetail } from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { ClaimStatusInfo } from '../../shared/models/reference-data.model';
import { ClaimDetailComponent } from './claim-detail.component';

const STORAGE_KEY = 'claims-module.auth-user';

function seedUser(role: AuthenticatedUser['role']): void {
  const user: AuthenticatedUser = {
    token: 'token',
    userId: 'user-1',
    username: 'handler1',
    displayName: 'Hana Handler',
    role,
    organizationId: 'org-1'
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
}

describe('ClaimDetailComponent', () => {
  let fixture: ComponentFixture<ClaimDetailComponent>;
  let httpMock: HttpTestingController;
  let overlayContainer: OverlayContainer;
  const claimId = 'claim-1';
  const baseUrl = `${environment.apiBaseUrl}/claims`;
  const statusesUrl = `${environment.apiBaseUrl}/reference/claim-statuses`;

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

  /** Mirrors `GET /api/reference/claim-statuses` (Ticket 03): Open can go to UnderInvestigation
   * (Handler), PendingPayment (Handler), Closed (Supervisor), or Withdrawn (Handler). */
  const statuses: ClaimStatusInfo[] = [
    {
      status: 'Open',
      validNextTransitions: [
        { toStatus: 'UnderInvestigation', requiredPermission: 'Handler' },
        { toStatus: 'PendingPayment', requiredPermission: 'Handler' },
        { toStatus: 'Closed', requiredPermission: 'Supervisor' },
        { toStatus: 'Withdrawn', requiredPermission: 'Handler' }
      ]
    },
    { status: 'Draft', validNextTransitions: [{ toStatus: 'Open', requiredPermission: 'Handler' }] }
  ];

  async function setup(): Promise<void> {
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
    overlayContainer = TestBed.inject(OverlayContainer);
  }

  function flushInitialRequests(claimDetail: ClaimDetail = detail): void {
    httpMock.expectOne(`${baseUrl}/${claimId}`).flush(claimDetail);
    httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
    httpMock.expectOne(statusesUrl).flush(statuses);
  }

  afterEach(() => {
    httpMock.verify();
    overlayContainer.getContainerElement().innerHTML = '';
  });

  describe('without a logged-in user', () => {
    beforeEach(setup);

    it('loads claim detail and audit log, rendering the header and Overview tab', () => {
      fixture.detectChanges();
      flushInitialRequests();
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
      flushInitialRequests();
      fixture.detectChanges();

      expect(fixture.componentInstance.auditLog().length).toBe(1);
      expect(fixture.componentInstance.auditLog()[0].eventType).toBe('ClaimCreated');
    });

    it('shows empty-state placeholders for the not-yet-implemented Reserves/Documents tabs', fakeAsync(() => {
      fixture.detectChanges();
      flushInitialRequests();
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

    it('does not render a transition control with no logged-in user', () => {
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      expect(fixture.componentInstance.validNextStatuses()).toEqual([]);
      expect(fixture.nativeElement.textContent).not.toContain('Transition status');
    });
  });

  describe('Ticket 06: status transition control', () => {
    afterEach(() => localStorage.removeItem(STORAGE_KEY));

    it('lists only the statuses permitted for the logged-in Handler role', async () => {
      seedUser('Handler');
      await setup();
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      // Handler may do UnderInvestigation/PendingPayment/Withdrawn but not the
      // Supervisor-gated Closed transition.
      expect(fixture.componentInstance.validNextStatuses()).toEqual(['UnderInvestigation', 'PendingPayment', 'Withdrawn']);
      expect(fixture.nativeElement.textContent).toContain('Transition status');
    });

    it('includes Closed for a Supervisor role', async () => {
      seedUser('Supervisor');
      await setup();
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      expect(fixture.componentInstance.validNextStatuses()).toContain('Closed');
    });

    it('opens a confirmation dialog and submits the transition on confirm, then reloads the claim', async () => {
      seedUser('Handler');
      await setup();
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      fixture.componentInstance.requestTransition('UnderInvestigation');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const dialogText = overlayContainer.getContainerElement().textContent ?? '';
      expect(dialogText).toContain('Transition to UnderInvestigation');

      const confirmButton = overlayContainer.getContainerElement().querySelector('button[mat-raised-button]') as HTMLButtonElement;
      confirmButton.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const putReq = httpMock.expectOne(`${baseUrl}/${claimId}/status`);
      expect(putReq.request.method).toBe('PUT');
      expect(putReq.request.body).toEqual({ targetStatus: 'UnderInvestigation', reason: null });
      putReq.flush({ id: claimId, status: 'UnderInvestigation' });

      // Reload after a successful transition.
      httpMock.expectOne(`${baseUrl}/${claimId}`).flush({ ...detail, status: 'UnderInvestigation' });
      httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
    });

    it('requires a non-empty reason before confirming a Withdrawn transition', async () => {
      seedUser('Handler');
      await setup();
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      fixture.componentInstance.requestTransition('Withdrawn');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const confirmButton = overlayContainer.getContainerElement().querySelector('button[mat-raised-button]') as HTMLButtonElement;
      expect(confirmButton.disabled).toBe(true);

      const textarea = overlayContainer.getContainerElement().querySelector('textarea') as HTMLTextAreaElement;
      textarea.value = 'Claimant withdrew the claim.';
      textarea.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(confirmButton.disabled).toBe(false);
    });

    it('shows an unmet pre-flight checklist item and disables confirm when attempting to close without a Claimant', async () => {
      seedUser('Supervisor');
      await setup();
      fixture.detectChanges();
      flushInitialRequests({ ...detail, parties: [] });
      fixture.detectChanges();

      fixture.componentInstance.requestTransition('Closed');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const dialogText = overlayContainer.getContainerElement().textContent ?? '';
      expect(dialogText).toContain('At least one Claimant party exists on the claim');

      const confirmButton = overlayContainer.getContainerElement().querySelector('button[mat-raised-button]') as HTMLButtonElement;
      expect(confirmButton.disabled).toBe(true);
    });

    it('allows confirming a Closed transition once all checklist conditions are satisfied', async () => {
      seedUser('Supervisor');
      await setup();
      fixture.detectChanges();
      flushInitialRequests(); // detail already has an active Claimant party
      fixture.detectChanges();

      fixture.componentInstance.requestTransition('Closed');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const confirmButton = overlayContainer.getContainerElement().querySelector('button[mat-raised-button]') as HTMLButtonElement;
      expect(confirmButton.disabled).toBe(false);
    });
  });
});
