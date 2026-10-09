import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, ViewChild, inject, signal, computed } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabGroup, MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ClaimsService } from '../../core/claims/claims.service';
import { hasRoleAuthority } from '../../shared/auth/role-authority';
import {
  ConfirmDialogChecklistItem,
  ConfirmDialogComponent,
  ConfirmDialogData,
  ConfirmDialogResult
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ClaimAuditLogEntry, ClaimDetail, ClaimParty, ClaimStatus, ReserveComponentType, ReserveHistoryEntry } from '../../shared/models/claim.model';
import { ReferenceDataService } from '../../core/reference-data/reference-data.service';
import { ClaimStatusInfo } from '../../shared/models/reference-data.model';
import { AddReserveDialogComponent, AddReserveDialogData, AddReserveDialogResult } from './add-reserve-dialog/add-reserve-dialog.component';

/**
 * Ticket 05's claim-detail screen: a persistent header plus four tabs.
 *
 * Tab layout — documented here because Tickets 06/07/10 build directly on it:
 * 0. Overview   — loss event, parties, risk objects (Ticket 05, fully built).
 * 1. Reserves   — real data (Ticket 07): one summary card per component (current amount,
 *    approval status, pending amount) plus its full transaction history, an "Add Reserve"
 *    dialog with a real-time authority-tier preview, and a Retract action on the submitter's own
 *    PendingApproval rows. Approve/Reject actions (Ticket 08) are role-gated (Supervisor/Manager,
 *    `hasRoleAuthority`) and hidden from the original submitter for Approve (self-approval is
 *    also server-enforced, 422) — Approve retries once with the aggregate-cap override flag if
 *    the server reports the $10,000,000 cap would be breached and the caller is a Manager.
 * 2. Documents  — placeholder; `claim.documents` is always `[]` until Ticket 10 adds upload/
 *    download. Replace the placeholder block, keep the tab index.
 * 3. Audit Log  — real data from `GET /api/claims/{id}/audit`, reverse-chronological, paginated.
 *
 * The header's status-transition control (FRS §11.3, Ticket 06) lives here, next to
 * `<app-status-badge>` inside `.detail-header`: a menu of the current claim's valid next
 * statuses (from `GET /api/reference/claim-statuses`, filtered to what the logged-in user's role
 * may perform), a confirmation dialog before committing, and — specifically for a Closed target —
 * a pre-flight checklist of closure conditions. Reuses `claim()` and re-fetches both claim detail
 * and the audit log after a successful transition.
 */
@Component({
  selector: 'app-claim-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatTooltipModule,
    StatusBadgeComponent
  ],
  templateUrl: './claim-detail.component.html',
  styleUrl: './claim-detail.component.scss'
})
export class ClaimDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly claimsService = inject(ClaimsService);
  private readonly referenceDataService = inject(ReferenceDataService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  @ViewChild(MatTabGroup) tabGroup?: MatTabGroup;

  readonly claimId = this.route.snapshot.paramMap.get('id') ?? '';

  readonly claim = signal<ClaimDetail | null>(null);
  readonly isLoadingClaim = signal(true);

  readonly auditLog = signal<ClaimAuditLogEntry[]>([]);
  readonly isLoadingAuditLog = signal(true);

  readonly statuses = signal<ClaimStatusInfo[]>([]);
  readonly isTransitioning = signal(false);

  readonly isSubmittingReserve = signal(false);
  readonly retractingReserveHistoryId = signal<string | null>(null);
  readonly approvingReserveHistoryId = signal<string | null>(null);
  readonly rejectingReserveHistoryId = signal<string | null>(null);

  /** FRS §6.4's RULE: a component with a transaction already PendingApproval can't accept a
   * further submission until retracted — fed to the Add Reserve dialog so it disables those
   * options instead of letting the user hit the 422 blindly. */
  readonly pendingComponentTypes = computed<ReserveComponentType[]>(() => {
    const claim = this.claim();
    if (!claim) {
      return [];
    }
    return claim.reserves.filter((r) => r.approvalStatus === 'PendingApproval').map((r) => r.componentType);
  });

  /** FRS §11.3 "Transition button": only the statuses valid from the claim's current status
   * AND permitted by the logged-in user's role (BR-ST-01's reference-data-driven permission,
   * not a static per-endpoint role attribute — see Ticket 06's Comments). */
  readonly validNextStatuses = computed<ClaimStatus[]>(() => {
    const claim = this.claim();
    const user = this.authService.currentUser();
    if (!claim || !user) {
      return [];
    }

    const info = this.statuses().find((s) => s.status === claim.status);
    if (!info) {
      return [];
    }

    return info.validNextTransitions
      .filter((t) => hasRoleAuthority(user.role, t.requiredPermission))
      .map((t) => t.toStatus);
  });

  ngOnInit(): void {
    this.loadClaim();
    this.loadAuditLog();

    this.referenceDataService.getClaimStatuses().subscribe({
      next: (statuses) => this.statuses.set(statuses),
      error: () => this.statuses.set([])
    });
  }

  copyClaimNumber(claimNumber: string): void {
    navigator.clipboard
      ?.writeText(claimNumber)
      .then(() => this.snackBar.open('Claim number copied.', 'Dismiss', { duration: 3000 }));
  }

  partyDisplayName(party: ClaimParty): string {
    if (party.partyType === 'Company') {
      return party.companyName ?? 'Unknown company';
    }
    return [party.firstName, party.lastName].filter(Boolean).join(' ') || 'Unknown';
  }

  /** FRS §11.3: opens the confirmation dialog for a requested transition — with a required
   * reason for Reopened/Withdrawn, and a pre-flight closure checklist for Closed — and submits
   * the transition on confirm. */
  requestTransition(targetStatus: ClaimStatus): void {
    const claim = this.claim();
    if (!claim || this.isTransitioning()) {
      return;
    }

    const requiresReason = targetStatus === 'Reopened' || targetStatus === 'Withdrawn';
    const checklist = targetStatus === 'Closed' ? this.buildClosureChecklist(claim) : undefined;

    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, ConfirmDialogResult>(ConfirmDialogComponent, {
      width: '480px',
      data: {
        title: `Transition to ${targetStatus}`,
        message: `Transition claim ${claim.claimNumber} from ${claim.status} to ${targetStatus}?`,
        checklist,
        requireReason: requiresReason,
        reasonLabel: targetStatus === 'Reopened' ? 'Reopen reason' : targetStatus === 'Withdrawn' ? 'Withdrawal reason' : undefined,
        confirmLabel: 'Transition'
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (!result?.confirmed) {
        return;
      }
      this.submitTransition(claim.id, targetStatus, result.reason);
    });
  }

  /** FRS §4.3's closure conditions (CC-01..03). CC-01 (no PendingApproval reserves) is now
   * computed from real data (Ticket 07) — see Ticket 06's Comments for why CC-02 still always
   * shows satisfied (no persisted "validation issue" concept exists). Computed client-side from
   * the already-loaded claim detail, as a preview — the server independently re-checks on submit
   * (BR-ST-03). */
  private buildClosureChecklist(claim: ClaimDetail): ConfirmDialogChecklistItem[] {
    return [
      {
        label: 'At least one Claimant party exists on the claim',
        satisfied: claim.parties.some((p) => p.role === 'Claimant' && p.isActive)
      },
      {
        label: 'No reserve components are pending approval',
        satisfied: !claim.reserves.some((r) => r.approvalStatus === 'PendingApproval')
      },
      { label: 'No unresolved Critical validation issues remain', satisfied: true }
    ];
  }

  /** Ticket 07: opens the "Add Reserve" dialog and submits on confirm. */
  openAddReserveDialog(): void {
    const claim = this.claim();
    if (!claim || this.isSubmittingReserve()) {
      return;
    }

    const dialogRef = this.dialog.open<AddReserveDialogComponent, AddReserveDialogData, AddReserveDialogResult>(
      AddReserveDialogComponent,
      { width: '480px', data: { pendingComponentTypes: this.pendingComponentTypes() } }
    );

    dialogRef.afterClosed().subscribe((result) => {
      if (!result) {
        return;
      }
      this.isSubmittingReserve.set(true);
      this.claimsService
        .submitReserve(claim.id, {
          componentType: result.componentType,
          amount: result.amount,
          changeReason: result.changeReason
        })
        .subscribe({
          next: (submitted) => {
            this.isSubmittingReserve.set(false);
            const message =
              submitted.approvalStatus === 'AutoApproved'
                ? `${result.componentType} reserve of ${result.amount} auto-approved.`
                : `${result.componentType} reserve of ${result.amount} submitted, pending approval.`;
            this.snackBar.open(message, 'Dismiss', { duration: 5000 });
            this.loadClaim();
            this.loadAuditLog();
          },
          error: () => this.isSubmittingReserve.set(false)
        });
    });
  }

  /** FRS §6.4's RULE: only the original submitter may retract their own still-pending
   * transaction — the server enforces this too (422); this just avoids showing the control to
   * anyone else in the first place. */
  canRetract(submittedByUserId: string | null): boolean {
    const user = this.authService.currentUser();
    return !!user && !!submittedByUserId && user.userId === submittedByUserId;
  }

  retractReserve(claimId: string, reserveHistoryId: string): void {
    if (this.retractingReserveHistoryId()) {
      return;
    }
    this.retractingReserveHistoryId.set(reserveHistoryId);
    this.claimsService.retractReserve(claimId, reserveHistoryId).subscribe({
      next: () => {
        this.retractingReserveHistoryId.set(null);
        this.snackBar.open('Reserve transaction retracted.', 'Dismiss', { duration: 5000 });
        this.loadClaim();
        this.loadAuditLog();
      },
      error: () => this.retractingReserveHistoryId.set(null)
    });
  }

  /** FRS §6.3/BR-R-03: Supervisor/Manager only, and never the original submitter (the server
   * enforces both too — this just avoids showing the control where it would just 422). */
  canApprove(entry: ReserveHistoryEntry): boolean {
    const user = this.authService.currentUser();
    return !!user && hasRoleAuthority(user.role, 'Supervisor') && entry.submittedByUserId !== user.userId;
  }

  canReject(): boolean {
    const user = this.authService.currentUser();
    return !!user && hasRoleAuthority(user.role, 'Supervisor');
  }

  /** FRS §6.4/BR-R-05: approves without the override flag first; if the server reports the
   * $10,000,000 aggregate cap would be breached and the caller is a Manager, offers to retry
   * with it set rather than making the Manager know in advance that this particular approval
   * needs it. */
  approveReserve(claimId: string, reserveHistoryId: string, overrideAggregateCap = false): void {
    if (this.approvingReserveHistoryId()) {
      return;
    }
    this.approvingReserveHistoryId.set(reserveHistoryId);
    this.claimsService.approveReserve(claimId, reserveHistoryId, { overrideAggregateCap }).subscribe({
      next: () => {
        this.approvingReserveHistoryId.set(null);
        this.snackBar.open('Reserve transaction approved.', 'Dismiss', { duration: 5000 });
        this.loadClaim();
        this.loadAuditLog();
      },
      error: (error: unknown) => {
        this.approvingReserveHistoryId.set(null);
        this.offerAggregateCapOverrideIfApplicable(error, claimId, reserveHistoryId);
      }
    });
  }

  rejectReserve(claimId: string, reserveHistoryId: string): void {
    if (this.rejectingReserveHistoryId()) {
      return;
    }

    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, ConfirmDialogResult>(ConfirmDialogComponent, {
      width: '480px',
      data: {
        title: 'Reject reserve transaction',
        message: 'Reject this pending reserve transaction? The submitter will be able to resubmit a revised amount.',
        requireReason: true,
        reasonLabel: 'Rejection reason',
        confirmLabel: 'Reject'
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (!result?.confirmed || !result.reason) {
        return;
      }
      this.rejectingReserveHistoryId.set(reserveHistoryId);
      this.claimsService.rejectReserve(claimId, reserveHistoryId, { rejectionReason: result.reason }).subscribe({
        next: () => {
          this.rejectingReserveHistoryId.set(null);
          this.snackBar.open('Reserve transaction rejected.', 'Dismiss', { duration: 5000 });
          this.loadClaim();
          this.loadAuditLog();
        },
        error: () => this.rejectingReserveHistoryId.set(null)
      });
    });
  }

  private offerAggregateCapOverrideIfApplicable(error: unknown, claimId: string, reserveHistoryId: string): void {
    const user = this.authService.currentUser();
    const detail = error instanceof HttpErrorResponse && typeof error.error?.detail === 'string' ? error.error.detail : null;
    if (user?.role !== 'Manager' || !detail?.includes('$10,000,000')) {
      return;
    }

    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, ConfirmDialogResult>(ConfirmDialogComponent, {
      width: '480px',
      data: {
        title: 'Approve over the aggregate cap?',
        message: `${detail} Set the override flag and approve anyway?`,
        confirmLabel: 'Set override and approve'
      }
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result?.confirmed) {
        this.approveReserve(claimId, reserveHistoryId, true);
      }
    });
  }

  private submitTransition(claimId: string, targetStatus: ClaimStatus, reason: string | null): void {
    this.isTransitioning.set(true);
    this.claimsService.transitionStatus(claimId, { targetStatus, reason }).subscribe({
      next: () => {
        this.isTransitioning.set(false);
        this.snackBar.open(`Claim transitioned to ${targetStatus}.`, 'Dismiss', { duration: 5000 });
        this.loadClaim();
        this.loadAuditLog();
      },
      error: () => this.isTransitioning.set(false)
    });
  }

  private loadClaim(): void {
    this.claimsService.getById(this.claimId).subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.isLoadingClaim.set(false);
      },
      error: () => this.isLoadingClaim.set(false)
    });
  }

  private loadAuditLog(): void {
    this.claimsService.getAuditLog(this.claimId, 1, 50).subscribe({
      next: (page) => {
        this.auditLog.set(page.items);
        this.isLoadingAuditLog.set(false);
      },
      error: () => this.isLoadingAuditLog.set(false)
    });
  }
}
