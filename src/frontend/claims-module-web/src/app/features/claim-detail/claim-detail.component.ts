import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabGroup, MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute } from '@angular/router';
import { ClaimsService } from '../../core/claims/claims.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ClaimAuditLogEntry, ClaimDetail, ClaimParty } from '../../shared/models/claim.model';

/**
 * Ticket 05's claim-detail screen: a persistent header plus four tabs.
 *
 * Tab layout — documented here because Tickets 06/07/10 build directly on it:
 * 0. Overview   — loss event, parties, risk objects (this ticket, fully built).
 * 1. Reserves   — placeholder; `claim.reserves` is always `[]` until Ticket 07/08 give it a real
 *    shape and UI (open/adjust/approve). Replace the placeholder block, keep the tab index.
 * 2. Documents  — placeholder; `claim.documents` is always `[]` until Ticket 10 adds upload/
 *    download. Replace the placeholder block, keep the tab index.
 * 3. Audit Log  — real data from `GET /api/claims/{id}/audit`, reverse-chronological, paginated.
 *
 * The header's status-transition control (FRS §11.3, Ticket 06) is NOT built here — only the
 * read-only status badge is. Ticket 06 should add its transition button next to the status badge
 * inside `.detail-header`, reusing `claim()` and re-fetching after a successful transition.
 */
@Component({
  selector: 'app-claim-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressBarModule, MatTabsModule, MatTooltipModule, StatusBadgeComponent],
  templateUrl: './claim-detail.component.html',
  styleUrl: './claim-detail.component.scss'
})
export class ClaimDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly claimsService = inject(ClaimsService);
  private readonly snackBar = inject(MatSnackBar);

  @ViewChild(MatTabGroup) tabGroup?: MatTabGroup;

  readonly claimId = this.route.snapshot.paramMap.get('id') ?? '';

  readonly claim = signal<ClaimDetail | null>(null);
  readonly isLoadingClaim = signal(true);

  readonly auditLog = signal<ClaimAuditLogEntry[]>([]);
  readonly isLoadingAuditLog = signal(true);

  ngOnInit(): void {
    this.claimsService.getById(this.claimId).subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.isLoadingClaim.set(false);
      },
      error: () => this.isLoadingClaim.set(false)
    });

    this.claimsService.getAuditLog(this.claimId, 1, 50).subscribe({
      next: (page) => {
        this.auditLog.set(page.items);
        this.isLoadingAuditLog.set(false);
      },
      error: () => this.isLoadingAuditLog.set(false)
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
}
