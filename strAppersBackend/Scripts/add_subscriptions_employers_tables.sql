-- Add Subscriptions, Employers, EmployerBoard, EmployerAdds tables and update Students table
-- Run this script on both DEV and PROD databases

-- ============================================
-- 1. Create Subscriptions table
-- ============================================
CREATE TABLE IF NOT EXISTS "Subscriptions" (
    "Id" SERIAL PRIMARY KEY,
    "Description" VARCHAR(100) NOT NULL,
    "Price" DECIMAL(18,2) NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP
);

-- Insert seed data for Subscriptions
INSERT INTO "Subscriptions" ("Id", "Description", "Price", "CreatedAt")
VALUES
    (1, 'Junior', 0, CURRENT_TIMESTAMP),
    (2, 'Product', 0, CURRENT_TIMESTAMP),
    (3, 'Enterprise A', 0, CURRENT_TIMESTAMP),
    (4, 'Enterprise B', 0, CURRENT_TIMESTAMP)
ON CONFLICT ("Id") DO UPDATE SET
    "Description" = EXCLUDED."Description",
    "Price" = EXCLUDED."Price";

-- Set sequence to continue from 4
SELECT setval('"Subscriptions_Id_seq"', 4, true);

-- ============================================
-- 2. Create Employers table
-- ============================================
CREATE TABLE IF NOT EXISTS "Employers" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Logo" VARCHAR(500),
    "Website" VARCHAR(500),
    "ContactEmail" VARCHAR(255) NOT NULL,
    "Phone" VARCHAR(20),
    "Address" VARCHAR(500),
    "Description" VARCHAR(1000),
    "SubscriptionTypeId" INTEGER NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    CONSTRAINT "FK_Employers_Subscriptions" FOREIGN KEY ("SubscriptionTypeId") REFERENCES "Subscriptions"("Id") ON DELETE RESTRICT
);

-- Create indexes for Employers
CREATE INDEX IF NOT EXISTS "IX_Employers_ContactEmail" ON "Employers"("ContactEmail");
CREATE INDEX IF NOT EXISTS "IX_Employers_SubscriptionTypeId" ON "Employers"("SubscriptionTypeId");

-- ============================================
-- 3. Create EmployerBoard table
-- ============================================
CREATE TABLE IF NOT EXISTS "EmployerBoards" (
    "Id" SERIAL PRIMARY KEY,
    "EmployerId" INTEGER NOT NULL,
    "BoardId" VARCHAR(50) NOT NULL,
    "Observed" BOOLEAN NOT NULL DEFAULT FALSE,
    "Approved" BOOLEAN NOT NULL DEFAULT FALSE,
    "MeetRequest" TIMESTAMP,
    "Message" TEXT,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    CONSTRAINT "FK_EmployerBoards_Employers" FOREIGN KEY ("EmployerId") REFERENCES "Employers"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_EmployerBoards_ProjectBoards" FOREIGN KEY ("BoardId") REFERENCES "ProjectBoards"("BoardId") ON DELETE CASCADE,
    CONSTRAINT "UQ_EmployerBoards_EmployerId_BoardId" UNIQUE ("EmployerId", "BoardId")
);

-- Create indexes for EmployerBoards
CREATE INDEX IF NOT EXISTS "IX_EmployerBoards_EmployerId" ON "EmployerBoards"("EmployerId");
CREATE INDEX IF NOT EXISTS "IX_EmployerBoards_BoardId" ON "EmployerBoards"("BoardId");

-- ============================================
-- 4. Create EmployerAdds table
-- ============================================
CREATE TABLE IF NOT EXISTS "EmployerAdds" (
    "Id" SERIAL PRIMARY KEY,
    "EmployerId" INTEGER NOT NULL,
    "RoleId" INTEGER NOT NULL,
    "Tags" TEXT,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,
    CONSTRAINT "FK_EmployerAdds_Employers" FOREIGN KEY ("EmployerId") REFERENCES "Employers"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_EmployerAdds_Roles" FOREIGN KEY ("RoleId") REFERENCES "Roles"("Id") ON DELETE RESTRICT
);

-- Create indexes for EmployerAdds
CREATE INDEX IF NOT EXISTS "IX_EmployerAdds_EmployerId" ON "EmployerAdds"("EmployerId");
CREATE INDEX IF NOT EXISTS "IX_EmployerAdds_RoleId" ON "EmployerAdds"("RoleId");

-- ============================================
-- 5. Add CV and SubscriptionTypeId columns to Students table
-- ============================================
-- Add CV column (TEXT for base64 string)
ALTER TABLE "Students"
ADD COLUMN IF NOT EXISTS "CV" TEXT;

-- Add SubscriptionTypeId column
ALTER TABLE "Students"
ADD COLUMN IF NOT EXISTS "SubscriptionTypeId" INTEGER;

-- Add foreign key constraint for SubscriptionTypeId
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'FK_Students_Subscriptions_SubscriptionTypeId'
        AND table_name = 'Students'
    ) THEN
        ALTER TABLE "Students"
        ADD CONSTRAINT "FK_Students_Subscriptions_SubscriptionTypeId"
        FOREIGN KEY ("SubscriptionTypeId") REFERENCES "Subscriptions"("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- Create index for SubscriptionTypeId
CREATE INDEX IF NOT EXISTS "IX_Students_SubscriptionTypeId" ON "Students"("SubscriptionTypeId");

-- ============================================
-- Verification queries (optional - can be run separately)
-- ============================================
-- SELECT * FROM "Subscriptions";
-- SELECT COUNT(*) FROM "Employers";
-- SELECT COUNT(*) FROM "EmployerBoards";
-- SELECT COUNT(*) FROM "EmployerAdds";
-- SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'Students' AND column_name IN ('CV', 'SubscriptionTypeId');

