import { Component, Input, computed, signal } from '@angular/core';
import { ClaimStatus } from '../../models/claim.model';

/** FRS §11.1: the dashboard's and claim detail header's colour-coded status badge mapping.
 * Shared by the claims dashboard and the claim detail header so the two never drift. */
const STATUS_COLOURS: Record<ClaimStatus, string> = {
  Draft: 'grey',
  Open: 'blue',
  UnderInvestigation: 'orange',
  PendingPayment: 'purple',
  Closed: 'green',
  Reopened: 'amber',
  Withdrawn: 'dark-grey'
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="status-badge" [class]="colourClass()">{{ displayStatus() }}</span>`,
  styleUrl: './status-badge.component.scss'
})
export class StatusBadgeComponent {
  private readonly statusSignal = signal<ClaimStatus | null>(null);

  @Input({ required: true })
  set status(value: ClaimStatus) {
    this.statusSignal.set(value);
  }

  readonly displayStatus = computed(() => this.statusSignal() ?? '');
  readonly colourClass = computed(() => {
    const value = this.statusSignal();
    return `status-badge--${(value && STATUS_COLOURS[value]) ?? 'grey'}`;
  });
}
