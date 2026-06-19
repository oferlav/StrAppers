-- ============================================================
-- Skill-in × ATS Integration — Generic DB Migration Script
-- Supports Greenhouse now; designed to extend to Lever, Workday, etc.
-- Run manually in order. All statements are idempotent (IF NOT EXISTS).
-- ============================================================


-- ────────────────────────────────────────────────────────────
-- 1. AtsConnections
--    One row per employer per ATS provider.
--    Provider-specific config (board token, API keys, OAuth tokens)
--    lives in ConnectionConfigJson — no provider-specific columns.
--
--    Provider values: 'greenhouse' | 'lever' | 'workday' | 'manual'
--    Mode values:     'greenhouse-push' | 'greenhouse-pull' |
--                     'manual-token' | 'open'
--
--    Example ConnectionConfigJson for Greenhouse:
--    {
--      "boardToken": "acme-corp",
--      "jobBoardApiKey": "<encrypted>",
--      "assessmentApiKey": "<encrypted>"
--    }
-- ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "AtsConnections" (
    "Id"                   SERIAL PRIMARY KEY,
    "EmployerId"           INTEGER      NOT NULL REFERENCES "Employers"("Id") ON DELETE CASCADE,
    "Provider"             VARCHAR(50)  NOT NULL,
    "Mode"                 VARCHAR(50)  NOT NULL,
    "ConnectionConfigJson" TEXT,
    "IsActive"             BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS "IX_AtsConnections_EmployerId"
    ON "AtsConnections"("EmployerId");

CREATE INDEX IF NOT EXISTS "IX_AtsConnections_EmployerId_Provider"
    ON "AtsConnections"("EmployerId", "Provider");


-- ────────────────────────────────────────────────────────────
-- 2. AtsJobPostings
--    One row per job posting — either synced from an ATS or
--    entered manually by the employer.
--    Custom fields (Challenge, Resource, Expectations, QA) always
--    editable; mapped to Greenhouse custom fields of the same name
--    when ATS is connected.
-- ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "AtsJobPostings" (
    "Id"                SERIAL PRIMARY KEY,
    "EmployerId"        INTEGER      NOT NULL REFERENCES "Employers"("Id") ON DELETE CASCADE,
    "AtsConnectionId"   INTEGER      REFERENCES "AtsConnections"("Id") ON DELETE SET NULL,

    "Provider"          VARCHAR(50)  NOT NULL DEFAULT 'manual',
    "ExternalJobId"     VARCHAR(100),               -- ATS job ID (e.g. Greenhouse job_id); null = manual
    "IsAtsSynced"       BOOLEAN      NOT NULL DEFAULT FALSE,

    -- Standard job fields (locked in UI when IsAtsSynced = true)
    "Title"             VARCHAR(500) NOT NULL,
    "Location"          VARCHAR(500),
    "Department"        VARCHAR(200),
    "EmploymentType"    VARCHAR(100),
    "Description"       TEXT,

    -- Skill-in custom fields (always editable)
    "Challenge"         TEXT,                       -- maps to GH custom field "Challenge" (long_text)
    "Resource"          VARCHAR(1000),              -- maps to GH custom field "Resource" (short_text)
    "ResourceGithubUrl" VARCHAR(1000),              -- populated via GitHub OAuth repo picker
    "Expectations"      TEXT,                       -- maps to GH custom field "Expectations" (long_text)
    "QA"                TEXT,                       -- maps to GH custom field "Q&A" (long_text)

    "RawMetadataJson"   TEXT,                       -- full ATS metadata snapshot for audit

    -- true = job is publicly listed on Skill-in for self-apply (pull / open modes)
    "IsPublic"          BOOLEAN      NOT NULL DEFAULT FALSE,
    "IsActive"          BOOLEAN      NOT NULL DEFAULT TRUE,
    "LastSyncedAt"      TIMESTAMPTZ,
    "CreatedAt"         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"         TIMESTAMPTZ,

    -- Prevents duplicate ATS job syncs for the same employer
    CONSTRAINT "UQ_AtsJobPostings_ExternalJobId_EmployerId"
        UNIQUE NULLS NOT DISTINCT ("ExternalJobId", "EmployerId")
);

CREATE INDEX IF NOT EXISTS "IX_AtsJobPostings_EmployerId"
    ON "AtsJobPostings"("EmployerId");

CREATE INDEX IF NOT EXISTS "IX_AtsJobPostings_AtsConnectionId"
    ON "AtsJobPostings"("AtsConnectionId");

CREATE INDEX IF NOT EXISTS "IX_AtsJobPostings_IsAtsSynced"
    ON "AtsJobPostings"("IsAtsSynced");


-- ────────────────────────────────────────────────────────────
-- 3. AtsAssessmentInstances
--    One row per candidate per job posting.
--
--    TOKEN FLOW (coupon-based registration):
--      ExternalInterviewId = the token/coupon given to the candidate.
--      Candidate registers on Skill-in with this coupon:
--        Students.Coupon    = ExternalInterviewId
--        Students.InstituteId = EmployerId (from the linked job posting)
--      On registration, StudentId is backfilled here.
--
--    GREENHOUSE PUSH MODE:
--      Greenhouse calls /send-test → we generate the token → email it to the candidate.
--      Candidate registers → does project → CacheMetrics generated.
--      We call GreenhouseAssessmentService.CompleteAssessmentAsync(token, score, profileUrl).
--      That PATCHes CallbackUrl → Greenhouse polls /test-status → reads Score + ProfileUrl.
--
--    MANUAL-TOKEN MODE (no ATS):
--      Employer clicks "Add Candidate" on the portal → enters candidate email → token generated.
--      Same registration flow; no CallbackUrl → no Greenhouse notification.
-- ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "AtsAssessmentInstances" (
    "Id"                   SERIAL PRIMARY KEY,
    "JobPostingId"         INTEGER      REFERENCES "AtsJobPostings"("Id") ON DELETE SET NULL,

    "Provider"             VARCHAR(50)  NOT NULL,       -- 'greenhouse' | 'manual' | etc.

    -- The token: returned to Greenhouse as partner_interview_id;
    -- used by the candidate as their registration coupon.
    "ExternalInterviewId"  VARCHAR(100) NOT NULL,
    "ExternalTestId"       VARCHAR(100) NOT NULL,       -- assessment type slug / partner_test_id

    -- Candidate details (provided by ATS on send_test, or entered manually)
    "CandidateEmail"       VARCHAR(255) NOT NULL,
    "CandidateFirstName"   VARCHAR(100),
    "CandidateLastName"    VARCHAR(100),
    "ExternalProfileUrl"   VARCHAR(1000),               -- ATS candidate profile URL

    -- Greenhouse: PATCH this when assessment is complete to trigger result pull
    "CallbackUrl"          VARCHAR(1000),               -- null in manual mode

    -- Assessment state
    "Status"               VARCHAR(50)  NOT NULL DEFAULT 'not_started',
    "Score"                DECIMAL(10,2),
    "ProfileUrl"           VARCHAR(1000),               -- our results page URL; returned to ATS

    "SentBy"               VARCHAR(255),                -- recruiter email or employer user
    "InvitationSentAt"     TIMESTAMPTZ,                 -- when we emailed the candidate

    -- Backfilled when the candidate registers using the token
    "StudentId"            INTEGER,

    "CreatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ,

    CONSTRAINT "UQ_AtsAssessmentInstances_ExternalInterviewId"
        UNIQUE ("ExternalInterviewId")
);

CREATE INDEX IF NOT EXISTS "IX_AtsAssessmentInstances_CandidateEmail"
    ON "AtsAssessmentInstances"("CandidateEmail");

CREATE INDEX IF NOT EXISTS "IX_AtsAssessmentInstances_Status"
    ON "AtsAssessmentInstances"("Status");

CREATE INDEX IF NOT EXISTS "IX_AtsAssessmentInstances_StudentId"
    ON "AtsAssessmentInstances"("StudentId");

CREATE INDEX IF NOT EXISTS "IX_AtsAssessmentInstances_JobPostingId"
    ON "AtsAssessmentInstances"("JobPostingId");
