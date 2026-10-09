import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, flush, tick } from '@angular/core/testing';
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

    it('shows an empty-state on the Reserves tab and the not-yet-implemented Documents placeholder', fakeAsync(() => {
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
      expect(fixture.nativeElement.textContent).toContain('No reserve components recorded yet.');
      expect(fixture.nativeElement.textContent).toContain('Add Reserve');

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

  describe('Ticket 07: Reserves tab', () => {
    afterEach(() => localStorage.removeItem(STORAGE_KEY));

    const detailWithReserves: ClaimDetail = {
      ...detail,
      reserves: [
        {
          id: 'reserve-1',
          componentType: 'Indemnity',
          currentAmount: 5000,
          status: 'Active',
          approvalStatus: 'AutoApproved',
          pendingAmount: null,
          history: [
            {
              id: 'history-1',
              transactionType: 'Add',
              amount: 5000,
              previousBalance: 0,
              newBalance: 5000,
              approvalStatus: 'AutoApproved',
              changeReason: 'Initial reserve',
              postingStatus: 'Pending',
              submittedByUserId: 'user-1',
              approvedByUserId: null,
              approvedAt: null,
              rejectedByUserId: null,
              rejectedAt: null,
              rejectionReason: null,
              changeSequence: 1,
              createdAt: '2026-01-05T00:00:00Z'
            }
          ]
        },
        {
          id: 'reserve-2',
          componentType: 'Expense',
          currentAmount: 0,
          status: 'Active',
          approvalStatus: 'PendingApproval',
          pendingAmount: 50000,
          history: [
            {
              id: 'history-2',
              transactionType: 'Add',
              amount: 50000,
              previousBalance: 0,
              newBalance: 50000,
              approvalStatus: 'PendingApproval',
              changeReason: 'Big expense',
              postingStatus: 'Pending',
              submittedByUserId: 'someone-else',
              approvedByUserId: null,
              approvedAt: null,
              rejectedByUserId: null,
              rejectedAt: null,
              rejectionReason: null,
              changeSequence: 1,
              createdAt: '2026-01-05T00:00:00Z'
            }
          ]
        }
      ]
    };

    function openReservesTab(): void {
      const tabGroup = fixture.componentInstance.tabGroup;
      tabGroup!.selectedIndex = 1;
      fixture.detectChanges();
      tick();
      fixture.detectChanges();
    }

    it('renders a summary card per component with its current amount, approval status, and pending amount', fakeAsync(() => {
      seedUser('Handler');
      setup();
      tick();
      fixture.detectChanges();
      flushInitialRequests(detailWithReserves);
      fixture.detectChanges();

      openReservesTab();

      const text = fixture.nativeElement.textContent;
      expect(text).toContain('Indemnity');
      expect(text).toContain('AutoApproved');
      expect(text).toContain('Expense');
      expect(text).toContain('PendingApproval');
      expect(text).toContain('Initial reserve');
      expect(text).toContain('Big expense');
    }));

    it('shows the Retract action only on the current user’s own PendingApproval transaction', fakeAsync(() => {
      seedUser('Handler'); // userId: 'user-1', matching history-1's submitter but not history-2's
      setup();
      tick();
      fixture.detectChanges();
      flushInitialRequests(detailWithReserves);
      fixture.detectChanges();

      openReservesTab();

      const retractButtons = Array.from(fixture.nativeElement.querySelectorAll('button')).filter(
        (b) => (b as HTMLButtonElement).textContent?.trim() === 'Retract'
      );
      // history-1 is AutoApproved (no Retract regardless of owner); history-2 is PendingApproval
      // but submitted by someone else — so neither row shows a Retract button for this user.
      expect(retractButtons.length).toBe(0);
    }));

    it('shows the Retract action for the submitter’s own PendingApproval transaction and submits the retraction', fakeAsync(() => {
      seedUser('Handler');
      setup();
      tick();
      fixture.detectChanges();
      const mine = {
        ...detailWithReserves,
        reserves: [
          {
            ...detailWithReserves.reserves[1],
            history: [{ ...detailWithReserves.reserves[1].history[0], submittedByUserId: 'user-1' }]
          }
        ]
      };
      flushInitialRequests(mine);
      fixture.detectChanges();

      openReservesTab();

      const retractButton = Array.from(fixture.nativeElement.querySelectorAll('button')).find(
        (b) => (b as HTMLButtonElement).textContent?.trim() === 'Retract'
      ) as HTMLButtonElement;
      expect(retractButton).toBeDefined();

      retractButton.click();
      fixture.detectChanges();

      const retractReq = httpMock.expectOne(`${baseUrl}/${claimId}/reserves/history-2/retract`);
      expect(retractReq.request.method).toBe('POST');
      retractReq.flush({ reserveComponentId: 'reserve-2', reserveHistoryId: 'history-2', currentAmount: 0 });

      httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
      httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
      flush(); // clears the snack bar's auto-dismiss timer
    }));

    it('opens the Add Reserve dialog and submits a new reserve, then reloads the claim', fakeAsync(() => {
      seedUser('Handler');
      setup();
      tick();
      fixture.detectChanges();
      flushInitialRequests();
      fixture.detectChanges();

      openReservesTab();

      const addButton = Array.from(fixture.nativeElement.querySelectorAll('button')).find(
        (b) => (b as HTMLButtonElement).textContent?.trim() === 'Add Reserve'
      ) as HTMLButtonElement;
      addButton.click();
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const dialogRoot = overlayContainer.getContainerElement();
      const amountInput = dialogRoot.querySelector('input[type="number"]') as HTMLInputElement;
      amountInput.value = '5000';
      amountInput.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      const submitButton = dialogRoot.querySelector('button[mat-raised-button]') as HTMLButtonElement;
      submitButton.click();
      fixture.detectChanges();
      tick();

      const postReq = httpMock.expectOne(`${baseUrl}/${claimId}/reserves`);
      expect(postReq.request.method).toBe('POST');
      expect(postReq.request.body).toEqual({ componentType: 'Indemnity', amount: 5000, changeReason: null });
      postReq.flush({
        reserveComponentId: 'reserve-1',
        reserveHistoryId: 'history-1',
        componentType: 'Indemnity',
        transactionType: 'Add',
        amount: 5000,
        previousBalance: 0,
        newBalance: 5000,
        approvalStatus: 'AutoApproved',
        currentAmount: 5000,
        changeSequence: 1
      });

      httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detailWithReserves);
      httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
      flush(); // clears the snack bar's auto-dismiss timer
    }));

    it('shows the pending-approval closure condition as unmet when a reserve is PendingApproval', async () => {
      seedUser('Supervisor');
      await setup();
      fixture.detectChanges();
      flushInitialRequests(detailWithReserves);
      fixture.detectChanges();

      fixture.componentInstance.requestTransition('Closed');
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const dialogText = overlayContainer.getContainerElement().textContent ?? '';
      expect(dialogText).toContain('No reserve components are pending approval');

      const confirmButton = overlayContainer.getContainerElement().querySelector('button[mat-raised-button]') as HTMLButtonElement;
      expect(confirmButton.disabled).toBe(true);
    });

    describe('Ticket 08: approve/reject', () => {
      function reserveButtons(label: string): HTMLButtonElement[] {
        return Array.from(fixture.nativeElement.querySelectorAll('button')).filter(
          (b) => (b as HTMLButtonElement).textContent?.trim() === label
        ) as HTMLButtonElement[];
      }

      it('hides Approve and Reject for a Handler role', fakeAsync(() => {
        seedUser('Handler');
        setup();
        tick();
        fixture.detectChanges();
        flushInitialRequests(detailWithReserves);
        fixture.detectChanges();

        openReservesTab();

        expect(reserveButtons('Approve').length).toBe(0);
        expect(reserveButtons('Reject').length).toBe(0);
      }));

      it('shows Approve and Reject for a Supervisor who did not submit the transaction', fakeAsync(() => {
        seedUser('Supervisor'); // userId: 'user-1'; history-2 was submitted by 'someone-else'
        setup();
        tick();
        fixture.detectChanges();
        flushInitialRequests(detailWithReserves);
        fixture.detectChanges();

        openReservesTab();

        expect(reserveButtons('Approve').length).toBe(1);
        expect(reserveButtons('Reject').length).toBe(1);
      }));

      it('hides Approve (but still shows Reject) for a Supervisor who submitted the transaction themselves', fakeAsync(() => {
        seedUser('Supervisor');
        setup();
        tick();
        fixture.detectChanges();
        const mine = {
          ...detailWithReserves,
          reserves: [
            {
              ...detailWithReserves.reserves[1],
              history: [{ ...detailWithReserves.reserves[1].history[0], submittedByUserId: 'user-1' }]
            }
          ]
        };
        flushInitialRequests(mine);
        fixture.detectChanges();

        openReservesTab();

        expect(reserveButtons('Approve').length).toBe(0);
        expect(reserveButtons('Reject').length).toBe(1);
      }));

      it('clicking Approve posts to the approve endpoint without the override flag, then reloads the claim', fakeAsync(() => {
        seedUser('Supervisor');
        setup();
        tick();
        fixture.detectChanges();
        flushInitialRequests(detailWithReserves);
        fixture.detectChanges();

        openReservesTab();
        reserveButtons('Approve')[0].click();
        fixture.detectChanges();

        const approveReq = httpMock.expectOne(`${baseUrl}/${claimId}/reserves/history-2/approve`);
        expect(approveReq.request.method).toBe('POST');
        expect(approveReq.request.body).toEqual({ overrideAggregateCap: false });
        approveReq.flush({ reserveComponentId: 'reserve-2', reserveHistoryId: 'history-2', approvalStatus: 'Approved', currentAmount: 50000 });

        httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
        httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
        flush(); // clears the snack bar's auto-dismiss timer
      }));

      it('clicking Reject requires a reason, then posts it to the reject endpoint and reloads the claim', fakeAsync(() => {
        seedUser('Supervisor');
        setup();
        tick();
        fixture.detectChanges();
        flushInitialRequests(detailWithReserves);
        fixture.detectChanges();

        openReservesTab();
        reserveButtons('Reject')[0].click();
        fixture.detectChanges();
        tick();
        fixture.detectChanges();

        const dialogRoot = overlayContainer.getContainerElement();
        const confirmButton = dialogRoot.querySelector('button[mat-raised-button]') as HTMLButtonElement;
        expect(confirmButton.disabled).toBe(true);

        const textarea = dialogRoot.querySelector('textarea') as HTMLTextAreaElement;
        textarea.value = 'Amount not substantiated';
        textarea.dispatchEvent(new Event('input'));
        fixture.detectChanges();
        expect(confirmButton.disabled).toBe(false);

        confirmButton.click();
        fixture.detectChanges();
        tick();
        fixture.detectChanges();

        const rejectReq = httpMock.expectOne(`${baseUrl}/${claimId}/reserves/history-2/reject`);
        expect(rejectReq.request.method).toBe('POST');
        expect(rejectReq.request.body).toEqual({ rejectionReason: 'Amount not substantiated' });
        rejectReq.flush({ reserveComponentId: 'reserve-2', reserveHistoryId: 'history-2', approvalStatus: 'Rejected' });

        httpMock.expectOne(`${baseUrl}/${claimId}`).flush(detail);
        httpMock.expectOne((r) => r.url === `${baseUrl}/${claimId}/audit`).flush(auditPage);
        flush(); // clears the snack bar's auto-dismiss timer
      }));
    });
  });
});
