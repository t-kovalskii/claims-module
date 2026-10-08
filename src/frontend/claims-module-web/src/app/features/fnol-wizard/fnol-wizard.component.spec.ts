import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { environment } from '../../../environments/environment';
import { AuthenticatedUser } from '../../core/auth/models/authenticated-user.model';
import { CreateClaimRequest, CreateClaimResult } from '../../shared/models/claim.model';
import { CauseOfLossCode } from '../../shared/models/reference-data.model';
import { FnolWizardComponent } from './fnol-wizard.component';

// Mirrors AuthService's own storage key (see auth.service.ts) — seeding localStorage directly,
// the same way auth.service.spec.ts does, keeps AuthService itself real rather than mocked.
const AUTH_STORAGE_KEY = 'claims-module.auth-user';

describe('FnolWizardComponent', () => {
  let fixture: ComponentFixture<FnolWizardComponent>;
  let httpMock: HttpTestingController;
  const causeOfLossUrl = `${environment.apiBaseUrl}/reference/cause-of-loss-codes`;
  const claimsUrl = `${environment.apiBaseUrl}/claims`;

  const causeCodes: CauseOfLossCode[] = [
    { id: 'cause-1', code: 'COLLISION', name: 'Collision', perilCategory: 'Auto', sortOrder: 1 }
  ];

  function createComponent(): void {
    fixture = TestBed.createComponent(FnolWizardComponent);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === causeOfLossUrl).flush(causeCodes);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FnolWizardComponent, HttpClientTestingModule],
      providers: [provideRouter([]), provideNoopAnimations()]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('loads active cause-of-loss codes on init', () => {
    createComponent();
    expect(fixture.componentInstance.causeOfLossCodes()).toEqual(causeCodes);
    expect(fixture.componentInstance.isLoadingReferenceData()).toBeFalse();
  });

  it('keeps Step 1 invalid until loss date, description and cause of loss are filled, and rejects a future loss date', () => {
    createComponent();
    const step1 = fixture.componentInstance.step1;

    expect(step1.invalid).toBeTrue();

    step1.patchValue({
      lossDate: '2026-01-01',
      lossDescription: 'A rear-end collision occurred on the highway during rain.',
      causeOfLossCodeId: 'cause-1'
    });
    expect(step1.valid).toBeTrue();

    const farFuture = new Date();
    farFuture.setFullYear(farFuture.getFullYear() + 1);
    step1.get('lossDate')?.setValue(farFuture.toISOString().slice(0, 10));
    expect(step1.get('lossDate')?.hasError('futureDate')).toBeTrue();
  });

  it('rejects a loss description shorter than 20 characters', () => {
    createComponent();
    const description = fixture.componentInstance.step1.get('lossDescription');
    description?.setValue('too short');
    expect(description?.hasError('minlength')).toBeTrue();
  });

  it('requires at least one Claimant party before Step 2 is valid, and reflects that inline', () => {
    createComponent();

    // The wizard starts with one default party row (role Claimant, no name yet) and one default
    // risk object row (no description yet) — step2 is invalid because of those missing fields,
    // not the claimant rule, which is satisfied by the default role.
    expect(fixture.componentInstance.hasClaimant).toBeTrue();
    expect(fixture.componentInstance.step2.invalid).toBeTrue();

    // Switch the only party to a non-Claimant role — now the claimant rule itself fires too.
    fixture.componentInstance.partyGroup(0).patchValue({ role: 'Witness', firstName: 'Sam', lastName: 'Lee' });
    fixture.detectChanges();
    expect(fixture.componentInstance.hasClaimant).toBeFalse();
    expect(fixture.componentInstance.step2.hasError('noClaimant')).toBeTrue();

    // Fix it: make the party a named Claimant again, and fill in the default risk object so the
    // only remaining invalidity being tested is the claimant rule.
    fixture.componentInstance.partyGroup(0).patchValue({ role: 'Claimant' });
    fixture.componentInstance.riskObjectGroup(0).patchValue({ assetDescription: '2022 Honda Civic' });
    expect(fixture.componentInstance.hasClaimant).toBeTrue();
    expect(fixture.componentInstance.step2.valid).toBeTrue();
  });

  it('"Add Party" and "Add Risk Object" grow their respective FormArrays', () => {
    createComponent();
    const initialParties = fixture.componentInstance.parties.length;
    const initialRiskObjects = fixture.componentInstance.riskObjects.length;

    fixture.componentInstance.addParty();
    fixture.componentInstance.addRiskObject();

    expect(fixture.componentInstance.parties.length).toBe(initialParties + 1);
    expect(fixture.componentInstance.riskObjects.length).toBe(initialRiskObjects + 1);
  });

  it('submits the Ticket 04 request shape, shows a success snackbar and navigates to the created claim', () => {
    const loggedInUser: AuthenticatedUser = {
      token: 't',
      userId: 'user-1',
      username: 'handler1',
      displayName: 'Hank Handler',
      role: 'Handler',
      organizationId: 'org-1'
    };
    // Seeded before the component (and so AuthService, providedIn: 'root') is constructed — the
    // same way auth.service.spec.ts drives a logged-in session, so AuthService's own
    // restoreFromStorage() picks it up rather than this test replacing the service's behaviour.
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(loggedInUser));

    createComponent();
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const snackBar = TestBed.inject(MatSnackBar);
    const navigateSpy = spyOn(router, 'navigate');
    const snackBarSpy = spyOn(snackBar, 'open');

    component.step1.patchValue({
      unknownPolicy: true,
      lossDate: '2026-01-01',
      lossDescription: 'A rear-end collision occurred on the highway during rain.',
      lossLocation: 'I-95',
      causeOfLossCodeId: 'cause-1',
      estimatedLossAmount: 1000,
      policeReportNumber: 'PR-1'
    });
    component.partyGroup(0).patchValue({ role: 'Claimant', firstName: 'Jane', lastName: 'Doe' });
    component.riskObjectGroup(0).patchValue({ assetDescription: '2022 Honda Civic' });

    expect(component.form.valid).toBeTrue();

    component.submit();

    const req = httpMock.expectOne(claimsUrl);
    expect(req.request.method).toBe('POST');
    const body = req.request.body as CreateClaimRequest;
    expect(body.policyId).toBeNull();
    expect(body.assignedHandlerUserId).toBe('user-1');
    expect(body.lossEvent.lossDescription).toBe('A rear-end collision occurred on the highway during rain.');
    expect(body.parties).toEqual([
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
    ]);
    expect(body.riskObjects.length).toBe(1);
    expect(body.riskObjects[0].assetDescription).toBe('2022 Honda Civic');

    const result: CreateClaimResult = { id: 'new-claim-id', claimNumber: 'CLM-2026-0000005', status: 'Draft', warnings: [] };
    req.flush(result);

    expect(snackBarSpy).toHaveBeenCalled();
    expect(snackBarSpy.calls.mostRecent().args[0]).toContain('CLM-2026-0000005');
    expect(navigateSpy).toHaveBeenCalledWith(['/claims', 'new-claim-id']);
    expect(component.isSubmitting()).toBeFalse();
  });

  it('does not submit when the form is invalid', () => {
    createComponent();
    spyOn(TestBed.inject(Router), 'navigate');

    fixture.componentInstance.submit();

    httpMock.expectNone(claimsUrl);
    expect(fixture.componentInstance.form.touched).toBeTrue();
  });

  it('reports a no-policy-linked warning when no policy is selected', () => {
    createComponent();
    expect(fixture.componentInstance.warnings).toContain(
      'No policy linked. Policy must be associated before reserves can be set.'
    );
  });
});
