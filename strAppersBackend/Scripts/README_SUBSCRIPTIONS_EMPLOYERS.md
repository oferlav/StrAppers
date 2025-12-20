# Subscriptions and Employers Schema Enhancement

## Summary
This enhancement adds new tables for subscriptions and employers, and adds CV and SubscriptionType fields to the Students table.

## Changes Made

### 1. New Tables Created:
- **Subscriptions**: Stores subscription types (Junior, Product, Enterprise A, Enterprise B)
- **Employers**: Stores employer information with subscription type
- **EmployerBoards**: Links employers to project boards with observation/approval status
- **EmployerAdds**: Stores employer job postings by role

### 2. Students Table Updates:
- Added **CV** column (TEXT) - stores base64 encoded PDF/document
- Added **SubscriptionTypeId** column (INTEGER, FK to Subscriptions)

## Files Created/Modified

### Models Created:
- `strAppersBackend/Models/Subscription.cs`
- `strAppersBackend/Models/Employer.cs`
- `strAppersBackend/Models/EmployerBoard.cs`
- `strAppersBackend/Models/EmployerAdd.cs`

### Models Modified:
- `strAppersBackend/Models/Student.cs` - Added CV and SubscriptionTypeId fields

### DbContext Updated:
- `strAppersBackend/Data/ApplicationDbContext.cs` - Added DbSets and entity configurations

### SQL Script:
- `strAppersBackend/Scripts/add_subscriptions_employers_tables.sql` - Run this on both DEV and PROD

## How to Apply

### Step 1: Run SQL Script
Execute the SQL script on both databases:
```sql
-- Run in pgAdmin on DEV database
-- Then run on PROD database
-- File: strAppersBackend/Scripts/add_subscriptions_employers_tables.sql
```

### Step 2: Verify
Run these queries to verify:
```sql
-- Check Subscriptions
SELECT * FROM "Subscriptions";

-- Check new columns in Students
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'Students' 
AND column_name IN ('CV', 'SubscriptionTypeId');

-- Check new tables exist
SELECT table_name 
FROM information_schema.tables 
WHERE table_name IN ('Subscriptions', 'Employers', 'EmployerBoards', 'EmployerAdds');
```

## Subscription Seed Data
The script automatically inserts:
1. Junior (Price: 0)
2. Product (Price: 0)
3. Enterprise A (Price: 0)
4. Enterprise B (Price: 0)

## Foreign Key Relationships
- Employers.SubscriptionTypeId → Subscriptions.Id
- EmployerBoards.EmployerId → Employers.Id
- EmployerBoards.BoardId → ProjectBoards.Id
- EmployerAdds.EmployerId → Employers.Id
- EmployerAdds.RoleId → Roles.Id
- Students.SubscriptionTypeId → Subscriptions.Id (nullable)

## Notes
- All new tables include CreatedAt and UpdatedAt timestamps
- CV field in Students is TEXT type to store base64 encoded documents
- SubscriptionTypeId in Students is nullable (students may not have a subscription)
- EmployerBoard has unique constraint on (EmployerId, BoardId) to prevent duplicates




