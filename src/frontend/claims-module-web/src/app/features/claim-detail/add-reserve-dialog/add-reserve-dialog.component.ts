import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ReserveComponentType } from '../../../shared/models/claim.model';

/** FRS §6.3's authority thresholds, hardcoded here per this ticket's own instructions (spec-fixed
 * values, not server-driven) — the backend enforces the same $10,000/$100,000 cutoffs
 * (`ReserveApprovalThresholds` in `ClaimsModule.Domain`); this is a client-side preview only,
 * re-validated authoritatively by the server on submit. */
const AUTO_APPROVE_MAX = 10_000;
const SUPERVISOR_APPROVE_MAX = 100_000;

export const RESERVE_COMPONENT_TYPES: ReserveComponentType[] = ['Indemnity', 'Expense', 'ALAE', 'SubrogationRecoverable'];

export interface AddReserveDialogData {
  /** Component types that already have a transaction pending approval — FRS §6.4's RULE: these
   * can't accept a further submission until retracted, so the dialog disables them with an
   * explanatory hint rather than letting the user hit the 422 blindly. */
  pendingComponentTypes: ReserveComponentType[];
}

export interface AddReserveDialogResult {
  componentType: ReserveComponentType;
  amount: number;
  changeReason: string | null;
}

export interface ReserveTierPreview {
  label: string;
  cssClass: string;
}

/**
 * Ticket 07's "Add Reserve" form (FRS §11.3 Tab 3's "slide-in panel with Component, Amount,
 * Reason fields and real-time authority indicator"). A `MatDialog`, following
 * `ConfirmDialogComponent`'s precedent, rather than an actual slide-in panel — this ticket's
 * checklist only requires the real-time tier indicator and the three fields, not the specific
 * slide-in interaction pattern, and reusing the dialog seam keeps the claim-detail component
 * simple.
 */
@Component({
  selector: 'app-add-reserve-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './add-reserve-dialog.component.html',
  styleUrl: './add-reserve-dialog.component.scss'
})
export class AddReserveDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<AddReserveDialogComponent, AddReserveDialogResult | undefined>>(MatDialogRef);
  readonly data = inject<AddReserveDialogData>(MAT_DIALOG_DATA);

  readonly componentTypes = RESERVE_COMPONENT_TYPES;

  readonly form = new FormGroup({
    componentType: new FormControl<ReserveComponentType>('Indemnity', { nonNullable: true, validators: [Validators.required] }),
    amount: new FormControl<number | null>(null, { validators: [Validators.required] }),
    changeReason: new FormControl<string>('', { nonNullable: true })
  });

  isPending(componentType: ReserveComponentType): boolean {
    return this.data.pendingComponentTypes.includes(componentType);
  }

  /** BR-R-01: only SubrogationRecoverable may be negative; every other type must be > 0. Read
   * directly off the form on every change-detection pass rather than as a FormControl validator,
   * since the valid sign depends on the *other* field (componentType). */
  get amountError(): string | null {
    const { componentType, amount } = this.form.getRawValue();
    if (amount == null) {
      return null;
    }
    if (componentType === 'SubrogationRecoverable') {
      return amount === 0 ? 'SubrogationRecoverable amount must not be zero.' : null;
    }
    return amount <= 0 ? 'Reserve amount must be greater than zero.' : null;
  }

  /** FRS §6.3 / story 12: the real-time auto/supervisor/manager tier indicator, computed off the
   * entered amount's absolute value (the backend applies the threshold to the transaction's own
   * amount, not the running total — same rule mirrored here). */
  get tierPreview(): ReserveTierPreview | null {
    const amount = this.form.controls.amount.value;
    if (amount == null || this.amountError) {
      return null;
    }

    const absAmount = Math.abs(amount);
    if (absAmount <= AUTO_APPROVE_MAX) {
      return { label: 'Auto-approved (≤ $10,000)', cssClass: 'tier-auto' };
    }
    if (absAmount <= SUPERVISOR_APPROVE_MAX) {
      return { label: 'Requires Supervisor or Manager approval ($10,000–$100,000)', cssClass: 'tier-supervisor' };
    }
    return { label: 'Requires Manager approval (> $100,000)', cssClass: 'tier-manager' };
  }

  get canSubmit(): boolean {
    const { componentType, amount } = this.form.getRawValue();
    return !!componentType && !this.isPending(componentType) && amount != null && !this.amountError;
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }
    const { componentType, amount, changeReason } = this.form.getRawValue();
    this.dialogRef.close({
      componentType,
      amount: amount!,
      changeReason: changeReason?.trim() || null
    });
  }

  cancel(): void {
    this.dialogRef.close(undefined);
  }
}
