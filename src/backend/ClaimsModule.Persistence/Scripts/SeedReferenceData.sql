-- Ticket 03: reference data + simulated policies.
--
-- Independently re-runnable: every INSERT is guarded by a WHERE NOT EXISTS check against the
-- natural key of the row it would create, so running this script again (locally via sqlcmd,
-- in CI, or automatically on every app start via ReferenceDataSeeder) is a safe no-op once the
-- data is present. Each INSERT is additionally wrapped in TRY/CATCH, swallowing only a unique-key
-- violation (2601/2627): the NOT EXISTS pre-check alone is still racy against two callers running
-- the script at the same instant (ASP.NET Core's WebApplicationFactory, used by the integration
-- test host, is known to invoke Program's startup path more than once concurrently when resolving
-- the test server) — the TRY/CATCH is what makes this genuinely safe under concurrent re-runs,
-- not just sequential ones. Deliberately NOT EF Core migration seed data (HasData) per spec.md's
-- Implementation Decisions ("Policy simulation" / ADR-0004 context) and this ticket's own
-- checklist.
--
-- Source: Claims_Module_Candidate_Specification.docx (FRS) §5.5 "Simulated Policy Data",
-- §5.6 "Cause of Loss Reference Data", §4.1/4.2 "Claim Status Values / Valid Status
-- Transitions", §3 "User Roles & Permissions".

-- =====================================================================================
-- Cause of loss codes (FRS §5.6, Table "Code | Name | Peril Category | Notes")
-- =====================================================================================
BEGIN TRY
    INSERT INTO dbo.CauseOfLossCodes (Id, Code, Name, PerilCategory, IsActive, SortOrder, CreatedAt, CreatedBy, IsDeleted)
    SELECT NEWID(), v.Code, v.Name, v.PerilCategory, 1, v.SortOrder, SYSDATETIMEOFFSET(), 'seed-script', 0
    FROM (VALUES
        ('COL-FIRE',     'Fire',                   'Property',  10),
        ('COL-FLOOD',    'Flood',                  'Weather',   20),
        ('COL-THEFT',    'Theft',                  'Crime',     30),
        ('COL-VEH-COL',  'Vehicle Collision',      'Auto',      40),
        ('COL-VEH-COMP', 'Vehicle Comprehensive',  'Auto',      50),
        ('COL-LIAB',     'Third Party Liability',  'Liability', 60),
        ('COL-EQUIP',    'Equipment Breakdown',    'Equipment', 70),
        ('COL-WIND',     'Wind / Storm',           'Weather',   80),
        ('COL-INJURY',   'Bodily Injury',          'Liability', 90),
        ('COL-OTHER',    'Other / Unknown',        'General',   100)
    ) AS v(Code, Name, PerilCategory, SortOrder)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CauseOfLossCodes c WHERE c.Code = v.Code
    );
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;

-- =====================================================================================
-- Claim status transitions (FRS §4.2, Table "From Status | To Status | Rules / Conditions").
--
-- RequiredPermission is the minimum UserRole able to perform the transition (Supervisor and
-- Manager inherit everything a Handler can do, per FRS §3's capability table). FRS §4.2 names
-- "Handler" explicitly for the routine transitions; Closed and Reopened are gated to Supervisor
-- per BR-ST-04 (Reopened requires Supervisor explicitly) and FRS §3's "manage claim status
-- transitions" capability, which it lists only from Supervisor upward.
-- =====================================================================================
BEGIN TRY
    INSERT INTO dbo.ClaimStatusTransitions (Id, FromStatus, ToStatus, RequiredPermission, CreatedAt, CreatedBy, IsDeleted)
    SELECT NEWID(), v.FromStatus, v.ToStatus, v.RequiredPermission, SYSDATETIMEOFFSET(), 'seed-script', 0
    FROM (VALUES
        ('Draft',              'Open',               'Handler'),
        ('Open',               'UnderInvestigation', 'Handler'),
        ('Open',               'PendingPayment',     'Handler'),
        ('Open',               'Closed',             'Supervisor'),
        ('Open',               'Withdrawn',          'Handler'),
        ('UnderInvestigation', 'Open',               'Handler'),
        ('UnderInvestigation', 'PendingPayment',     'Handler'),
        ('UnderInvestigation', 'Closed',             'Supervisor'),
        ('UnderInvestigation', 'Withdrawn',          'Handler'),
        ('PendingPayment',     'Closed',             'Supervisor'),
        ('Closed',             'Reopened',           'Supervisor'),
        ('Reopened',           'Open',               'Supervisor')
    ) AS v(FromStatus, ToStatus, RequiredPermission)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ClaimStatusTransitions t
        WHERE t.FromStatus = v.FromStatus AND t.ToStatus = v.ToStatus
    );
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;

-- =====================================================================================
-- Simulated policies (FRS §5.5). POL-2023-000099 is the FRS's own expired-policy example,
-- used later to exercise the loss-date-outside-effective-period warning.
-- =====================================================================================
BEGIN TRY
    INSERT INTO dbo.Policies (Id, PolicyNumber, ClientName, EffectiveDate, ExpirationDate, Status, CreatedAt, CreatedBy, IsDeleted)
    SELECT NEWID(), v.PolicyNumber, v.ClientName, v.EffectiveDate, v.ExpirationDate, v.Status, SYSDATETIMEOFFSET(), 'seed-script', 0
    FROM (VALUES
        ('POL-2024-001001', 'Meridian Transport LLC',    '2024-01-01', '2026-12-31', 'Active'),
        ('POL-2024-001002', 'Harborview Properties Inc', '2024-06-01', '2026-05-31', 'Active'),
        ('POL-2025-002001', 'Coastal Builders Group',    '2025-03-01', '2027-02-28', 'Active'),
        ('POL-2025-002002', 'Stanton Medical Group',     '2025-01-01', '2026-12-31', 'Active'),
        ('POL-2023-000099', 'Archived Corp',              '2020-01-01', '2021-12-31', 'Expired')
    ) AS v(PolicyNumber, ClientName, EffectiveDate, ExpirationDate, Status)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Policies p WHERE p.PolicyNumber = v.PolicyNumber
    );
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;

-- =====================================================================================
-- Policy coverages (FRS §5.5 "Coverage Types" column, normalised into its own table per
-- spec.md's Implementation Decisions).
-- =====================================================================================
BEGIN TRY
    INSERT INTO dbo.PolicyCoverages (Id, PolicyId, CoverageType, CreatedAt, CreatedBy, IsDeleted)
    SELECT NEWID(), p.Id, v.CoverageType, SYSDATETIMEOFFSET(), 'seed-script', 0
    FROM (VALUES
        ('POL-2024-001001', 'Vehicle'),
        ('POL-2024-001001', 'Cargo'),
        ('POL-2024-001002', 'Property'),
        ('POL-2024-001002', 'Liability'),
        ('POL-2025-002001', 'Property'),
        ('POL-2025-002001', 'Equipment'),
        ('POL-2025-002002', 'Liability'),
        ('POL-2025-002002', 'Vehicle'),
        ('POL-2023-000099', 'Property')
    ) AS v(PolicyNumber, CoverageType)
    INNER JOIN dbo.Policies p ON p.PolicyNumber = v.PolicyNumber
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.PolicyCoverages pc WHERE pc.PolicyId = p.Id AND pc.CoverageType = v.CoverageType
    );
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
