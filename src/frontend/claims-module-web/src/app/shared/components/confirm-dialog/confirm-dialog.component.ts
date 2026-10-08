import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

/** One item in an optional pre-flight checklist (Ticket 06 FRS §11.3's "pre-flight checklist of
 * closure conditions"), rendered as satisfied/unmet. */
export interface ConfirmDialogChecklistItem {
  label: string;
  satisfied: boolean;
}

export interface ConfirmDialogData {
  title: string;
  message: string;
  /** When present, rendered as a satisfied/unmet checklist; confirming is blocked while any
   * item is unmet. */
  checklist?: ConfirmDialogChecklistItem[];
  /** When true, a reason textarea is required before confirming. */
  requireReason?: boolean;
  reasonLabel?: string;
  confirmLabel?: string;
  cancelLabel?: string;
}

export interface ConfirmDialogResult {
  confirmed: true;
  reason: string | null;
}

/**
 * Generic confirmation dialog — Ticket 06's status-transition confirmation (with an optional
 * closure pre-flight checklist and an optional required reason) is its first use, but it's
 * deliberately not ticket-specific: Tickets 07/08/10 (reserve approve/reject, document
 * deletion, etc.) can reuse it rather than each growing their own near-duplicate dialog.
 */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss'
})
export class ConfirmDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<ConfirmDialogComponent, ConfirmDialogResult | undefined>>(MatDialogRef);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);

  readonly reasonControl = new FormControl('', this.data.requireReason ? [Validators.required] : []);

  get allConditionsSatisfied(): boolean {
    return !this.data.checklist || this.data.checklist.every((item) => item.satisfied);
  }

  get canConfirm(): boolean {
    return this.allConditionsSatisfied && (!this.data.requireReason || !!this.reasonControl.value?.trim());
  }

  confirm(): void {
    if (!this.canConfirm) {
      return;
    }
    this.dialogRef.close({ confirmed: true, reason: this.reasonControl.value?.trim() || null });
  }

  cancel(): void {
    this.dialogRef.close(undefined);
  }
}
