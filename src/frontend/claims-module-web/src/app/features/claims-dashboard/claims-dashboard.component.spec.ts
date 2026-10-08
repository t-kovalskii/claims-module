import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { ClaimSummary } from '../../shared/models/claim.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { ClaimsDashboardComponent } from './claims-dashboard.component';

describe('ClaimsDashboardComponent', () => {
  let fixture: ComponentFixture<ClaimsDashboardComponent>;
  let httpMock: HttpTestingController;
  const claimsUrl = `${environment.apiBaseUrl}/claims`;
  const causeOfLossUrl = `${environment.apiBaseUrl}/reference/cause-of-loss-codes`;

  const sampleClaim: ClaimSummary = {
    id: 'claim-1',
    claimNumber: 'CLM-2026-0000001',
    clientName: 'Jane Doe',
    policyNumber: 'POL-1',
    lossDate: '2026-01-05T00:00:00Z',
    status: 'Open',
    causeOfLossCode: 'COLLISION',
    totalReserves: 0
  };

  function flushInitialRequests(items: ClaimSummary[] = [sampleClaim]): void {
    httpMock.expectOne((r) => r.url === causeOfLossUrl).flush([]);
    const page: PagedResult<ClaimSummary> = { items, totalCount: items.length, page: 1, pageSize: 20 };
    httpMock.expectOne((r) => r.url === claimsUrl).flush(page);
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsDashboardComponent, HttpClientTestingModule],
      providers: [provideRouter([]), provideNoopAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimsDashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads claims on init with the default page/pageSize and renders them in the table', () => {
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('table tbody tr');
    expect(rows.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('CLM-2026-0000001');
    expect(fixture.nativeElement.textContent).toContain('Jane Doe');
  });

  it('shows the empty state when no claims match the current filters', () => {
    fixture.detectChanges();
    flushInitialRequests([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No claims match the current filters.');
  });

  it('re-fetches with the status filter and resets to page 1 when a filter changes', () => {
    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    fixture.componentInstance.status = 'Closed';
    fixture.componentInstance.onFilterChange();

    const req = httpMock.expectOne((r) => r.url === claimsUrl && r.params.get('status') === 'Closed');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('navigates to the FNOL wizard when "Log New Claim" is clicked', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    fixture.detectChanges();
    flushInitialRequests();
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('.dashboard-header button');
    button.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/claims/new']);
  });
});
