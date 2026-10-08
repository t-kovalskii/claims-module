import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { AuthService } from '../../core/auth/auth.service';
import { ClaimsService } from '../../core/claims/claims.service';
import { ReferenceDataService } from '../../core/reference-data/reference-data.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ClaimsFilter, ClaimStatus, ClaimSummary } from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { CauseOfLossCode } from '../../shared/models/reference-data.model';

/** FRS §4.1: the full set of statuses the dashboard's status filter can select from. The backend
 * query only supports a single `Status` value (not FRS §11.1's literal multi-select) — see this
 * ticket's Comments for that judgement call. */
const CLAIM_STATUSES: ClaimStatus[] = [
  'Draft',
  'Open',
  'UnderInvestigation',
  'PendingPayment',
  'Closed',
  'Reopened',
  'Withdrawn'
];

const PAGE_SIZE_OPTIONS = [10, 20, 50];

@Component({
  selector: 'app-claims-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    StatusBadgeComponent
  ],
  templateUrl: './claims-dashboard.component.html',
  styleUrl: './claims-dashboard.component.scss'
})
export class ClaimsDashboardComponent implements OnInit {
  readonly displayedColumns = [
    'claimNumber',
    'policyNumber',
    'clientName',
    'lossDate',
    'causeOfLossCode',
    'status',
    'totalReserves'
  ];

  readonly statusOptions = CLAIM_STATUSES;
  readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  readonly causeOfLossOptions = signal<CauseOfLossCode[]>([]);
  readonly result = signal<PagedResult<ClaimSummary> | null>(null);
  readonly loading = signal(false);

  status: ClaimStatus | null = null;
  dateFrom: string | null = null;
  dateTo: string | null = null;
  assignedToMeOnly = false;
  causeOfLossCode: string | null = null;
  searchTerm = '';

  page = 1;
  pageSize = PAGE_SIZE_OPTIONS[1];

  private readonly searchChanged = new Subject<string>();

  constructor(
    private readonly claimsService: ClaimsService,
    private readonly referenceDataService: ReferenceDataService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {
    this.searchChanged.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed()).subscribe((term) => {
      this.searchTerm = term;
      this.onFilterChange();
    });
  }

  ngOnInit(): void {
    this.referenceDataService.getCauseOfLossCodes().subscribe((codes) => this.causeOfLossOptions.set(codes));
    this.loadClaims();
  }

  onSearchInput(value: string): void {
    this.searchChanged.next(value);
  }

  onFilterChange(): void {
    this.page = 1;
    this.loadClaims();
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadClaims();
  }

  openClaim(claim: ClaimSummary): void {
    this.router.navigate(['/claims', claim.id]);
  }

  logNewClaim(): void {
    this.router.navigate(['/claims/new']);
  }

  private loadClaims(): void {
    this.loading.set(true);

    const filter: ClaimsFilter = {
      status: this.status,
      dateFrom: this.dateFrom,
      dateTo: this.dateTo,
      assignedHandlerUserId: this.assignedToMeOnly ? this.authService.currentUser()?.userId ?? null : null,
      causeOfLossCode: this.causeOfLossCode,
      search: this.searchTerm || null,
      page: this.page,
      pageSize: this.pageSize
    };

    this.claimsService.list(filter).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
