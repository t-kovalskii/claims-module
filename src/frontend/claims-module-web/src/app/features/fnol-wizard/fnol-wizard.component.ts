import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatStepperModule } from '@angular/material/stepper';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { AuthService } from '../../core/auth/auth.service';
import { ClaimsService } from '../../core/claims/claims.service';
import { PoliciesService } from '../../core/policies/policies.service';
import { ReferenceDataService } from '../../core/reference-data/reference-data.service';
import { ClaimPartyRole, ClaimPartyType, ClaimRiskObjectAssetType, CreateClaimRequest } from '../../shared/models/claim.model';
import { PolicySummary } from '../../shared/models/policy.model';
import { CauseOfLossCode } from '../../shared/models/reference-data.model';

/** FRS §5.4/§8: a loss date set in the future is a blocking error. */
function notFutureDateValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value) {
    return null;
  }
  return new Date(control.value).getTime() > Date.now() ? { futureDate: true } : null;
}

/** FRS §11.2 Step 2: at least one Claimant party is required before the step can advance. */
function atLeastOneClaimantValidator(group: AbstractControl): ValidationErrors | null {
  const parties = group.get('parties') as FormArray;
  const hasClaimant = parties.controls.some((c) => c.value.role === 'Claimant');
  return hasClaimant ? null : { noClaimant: true };
}

/** A person needs a name, a company needs a company name — not enforced by the backend's
 * `CreateClaimPartyRequest` (all name fields are optional there), but needed so the wizard
 * doesn't let a handler submit a nameless party row. */
function partyNameRequiredValidator(group: AbstractControl): ValidationErrors | null {
  const partyType = group.get('partyType')?.value as ClaimPartyType;
  if (partyType === 'Company') {
    return group.get('companyName')?.value ? null : { companyNameRequired: true };
  }
  return group.get('firstName')?.value || group.get('lastName')?.value ? null : { nameRequired: true };
}

@Component({
  selector: 'app-fnol-wizard',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatStepperModule,
    MatTooltipModule
  ],
  templateUrl: './fnol-wizard.component.html',
  styleUrl: './fnol-wizard.component.scss'
})
export class FnolWizardComponent implements OnInit {
  readonly partyRoles: ClaimPartyRole[] = ['Claimant', 'Insured', 'ThirdParty', 'Witness', 'Attorney'];
  readonly partyTypes: ClaimPartyType[] = ['Person', 'Company'];
  readonly assetTypes: ClaimRiskObjectAssetType[] = ['Vehicle', 'Property', 'Person', 'Equipment', 'Other'];

  readonly causeOfLossCodes = signal<CauseOfLossCode[]>([]);
  readonly isLoadingReferenceData = signal(true);
  readonly isSubmitting = signal(false);

  readonly policySearchControl = new FormControl('');
  readonly policyOptions = signal<PolicySummary[]>([]);
  readonly selectedPolicy = signal<PolicySummary | null>(null);
  readonly isSearchingPolicies = signal(false);

  readonly form: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly claimsService: ClaimsService,
    private readonly policiesService: PoliciesService,
    private readonly referenceDataService: ReferenceDataService,
    private readonly authService: AuthService,
    private readonly snackBar: MatSnackBar,
    private readonly router: Router
  ) {
    this.form = this.fb.group({
      step1: this.fb.group({
        unknownPolicy: [false],
        lossDate: [null, [Validators.required, notFutureDateValidator]],
        lossDescription: ['', [Validators.required, Validators.minLength(20)]],
        lossLocation: [''],
        causeOfLossCodeId: [null, Validators.required],
        estimatedLossAmount: [null],
        policeReportNumber: ['']
      }),
      step2: this.fb.group(
        {
          parties: this.fb.array([]),
          riskObjects: this.fb.array([])
        },
        { validators: atLeastOneClaimantValidator }
      )
    });

    this.policySearchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((value) => {
          // A selected option writes the full PolicySummary object into this control (via
          // MatAutocomplete's displayWith); only plain typed text should trigger a new search.
          if (typeof value !== 'string' || value.length === 0) {
            this.isSearchingPolicies.set(false);
            return of<PolicySummary[]>([]);
          }
          this.isSearchingPolicies.set(true);
          return this.policiesService.search(value);
        }),
        takeUntilDestroyed()
      )
      .subscribe((results) => {
        this.policyOptions.set(results);
        this.isSearchingPolicies.set(false);
      });

    this.addParty();
    this.addRiskObject();
  }

  ngOnInit(): void {
    this.referenceDataService.getCauseOfLossCodes().subscribe({
      next: (codes) => {
        this.causeOfLossCodes.set(codes);
        this.isLoadingReferenceData.set(false);
      },
      error: () => this.isLoadingReferenceData.set(false)
    });
  }

  get step1(): FormGroup {
    return this.form.get('step1') as FormGroup;
  }

  get step2(): FormGroup {
    return this.form.get('step2') as FormGroup;
  }

  get parties(): FormArray {
    return this.step2.get('parties') as FormArray;
  }

  get riskObjects(): FormArray {
    return this.step2.get('riskObjects') as FormArray;
  }

  partyGroup(index: number): FormGroup {
    return this.parties.at(index) as FormGroup;
  }

  riskObjectGroup(index: number): FormGroup {
    return this.riskObjects.at(index) as FormGroup;
  }

  get hasClaimant(): boolean {
    return !this.step2.hasError('noClaimant');
  }

  addParty(): void {
    this.parties.push(this.createPartyGroup());
  }

  removeParty(index: number): void {
    this.parties.removeAt(index);
  }

  addRiskObject(): void {
    this.riskObjects.push(this.createRiskObjectGroup());
  }

  removeRiskObject(index: number): void {
    this.riskObjects.removeAt(index);
  }

  selectPolicy(policy: PolicySummary): void {
    this.selectedPolicy.set(policy);
    this.policyOptions.set([]);
  }

  clearPolicy(): void {
    this.selectedPolicy.set(null);
    this.policySearchControl.setValue('', { emitEvent: false });
    this.policyOptions.set([]);
  }

  /** Renders the autocomplete's selected value — a plain string while the user is still typing,
   * or the full `PolicySummary` MatAutocomplete writes into the control once an option is picked. */
  displayPolicy = (value: PolicySummary | string | null): string => {
    if (!value) {
      return '';
    }
    return typeof value === 'string' ? value : `${value.policyNumber} — ${value.clientName}`;
  };

  onUnknownPolicyToggle(checked: boolean): void {
    if (checked) {
      this.clearPolicy();
    }
  }

  /** FRS §11.2 Step 1 "in-force indicator": green when the loss date falls inside the selected
   * policy's effective period, amber otherwise. 'unknown' when no policy/loss date is set yet. */
  get policyInForceStatus(): 'in-force' | 'out-of-force' | 'unknown' {
    const policy = this.selectedPolicy();
    const lossDate = this.step1.get('lossDate')?.value;
    if (!policy || !lossDate) {
      return 'unknown';
    }
    const loss = new Date(lossDate).getTime();
    const from = new Date(policy.effectiveDate).getTime();
    const to = new Date(policy.expirationDate).getTime();
    return loss >= from && loss <= to ? 'in-force' : 'out-of-force';
  }

  get lossDescriptionLength(): number {
    return ((this.step1.get('lossDescription')?.value as string) ?? '').length;
  }

  causeOfLossName(id: string | null): string {
    return this.causeOfLossCodes().find((c) => c.id === id)?.name ?? '';
  }

  /** FRS §5.2/§8 BR-C-02/BR-C-06: non-blocking warnings, computed client-side from what's already
   * on screen so Step 3's review can surface them before submission — the backend only returns
   * these as `CreateClaimResult.Warnings` *after* the claim is created, so this is a best-effort
   * preview, not a guarantee the server won't report something slightly different. */
  get warnings(): string[] {
    const warnings: string[] = [];
    if (!this.selectedPolicy()) {
      warnings.push('No policy linked. Policy must be associated before reserves can be set.');
    } else if (this.policyInForceStatus === 'out-of-force') {
      warnings.push("Loss date is outside the policy's effective period.");
    }
    return warnings;
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    const step1Value = this.step1.value;

    const request: CreateClaimRequest = {
      policyId: this.selectedPolicy()?.id ?? null,
      lossEvent: {
        lossDate: new Date(step1Value.lossDate).toISOString(),
        lossDescription: step1Value.lossDescription,
        lossLocation: step1Value.lossLocation || null,
        causeOfLossCodeId: step1Value.causeOfLossCodeId,
        estimatedLossAmount: step1Value.estimatedLossAmount,
        policeReportNumber: step1Value.policeReportNumber || null
      },
      assignedHandlerUserId: this.authService.currentUser()?.userId ?? null,
      parties: this.parties.controls.map((c) => ({
        role: c.value.role,
        partyType: c.value.partyType,
        firstName: c.value.firstName || null,
        lastName: c.value.lastName || null,
        companyName: c.value.companyName || null,
        email: c.value.email || null,
        phone: c.value.phone || null,
        notes: c.value.notes || null
      })),
      riskObjects: this.riskObjects.controls.map((c) => ({
        assetType: c.value.assetType,
        assetDescription: c.value.assetDescription,
        damageDescription: c.value.damageDescription || null,
        isPrimary: c.value.isPrimary,
        assetReference: c.value.assetReference || null
      }))
    };

    this.claimsService.create(request).subscribe({
      next: (result) => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Claim ${result.claimNumber} created successfully.`, 'Dismiss', { duration: 8000 });
        this.router.navigate(['/claims', result.id]);
      },
      error: () => this.isSubmitting.set(false)
    });
  }

  private createPartyGroup(): FormGroup {
    return this.fb.group(
      {
        role: ['Claimant', Validators.required],
        partyType: ['Person', Validators.required],
        firstName: [''],
        lastName: [''],
        companyName: [''],
        email: ['', Validators.email],
        phone: [''],
        notes: ['']
      },
      { validators: partyNameRequiredValidator }
    );
  }

  private createRiskObjectGroup(): FormGroup {
    return this.fb.group({
      assetType: ['Vehicle', Validators.required],
      assetDescription: ['', Validators.required],
      damageDescription: [''],
      isPrimary: [false],
      assetReference: ['']
    });
  }
}
