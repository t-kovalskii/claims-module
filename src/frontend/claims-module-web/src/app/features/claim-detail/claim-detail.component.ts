import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-claim-detail',
  standalone: true,
  template: `
    <h1>Claim Detail</h1>
    <p>Claim {{ claimId }} — placeholder, filled in by Ticket 05 (FNOL intake: frontend).</p>
  `
})
export class ClaimDetailComponent {
  private readonly route = inject(ActivatedRoute);

  readonly claimId = this.route.snapshot.paramMap.get('id');
}
