# Database Changes: Add Observed to ProjectBoards and JobDescription to EmployerAdds

## Summary
This update adds two new fields to the database:
1. **Observed** (boolean, default false) to `ProjectBoards` table
2. **JobDescription** (TEXT) to `EmployerAdds` table

## Changes Made

### Models Updated:
- `strAppersBackend/Models/ProjectBoard.cs` - Added `Observed` property
- `strAppersBackend/Models/EmployerAdd.cs` - Added `JobDescription` property

### DbContext Updated:
- `strAppersBackend/Data/ApplicationDbContext.cs` - Added configuration for both new fields

### Migration Created:
- `strAppersBackend/Migrations/20250127000001_AddObservedToProjectBoardsAndJobDescriptionToEmployerAdds.cs`

### SQL Scripts Created:
- `strAppersBackend/Scripts/AddObservedToProjectBoardsAndJobDescriptionToEmployerAdds_PostgreSQL.sql`
- `strAppersBackend/Scripts/AddObservedToProjectBoardsAndJobDescriptionToEmployerAdds_SQLServer.sql`

## How to Apply

### Option A: Using EF Core Migration (Recommended for Dev)
```bash
cd strAppersBackend
dotnet ef database update
```

### Option B: Manual SQL Scripts (For Prod or when migration isn't available)

#### For PostgreSQL:
```sql
-- Run the PostgreSQL script
-- File: strAppersBackend/Scripts/AddObservedToProjectBoardsAndJobDescriptionToEmployerAdds_PostgreSQL.sql
```

#### For SQL Server:
```sql
-- Run the SQL Server script
-- File: strAppersBackend/Scripts/AddObservedToProjectBoardsAndJobDescriptionToEmployerAdds_SQLServer.sql
```

## Verification

### PostgreSQL:
```sql
-- Verify ProjectBoards.Observed column
SELECT column_name, data_type, is_nullable, column_default 
FROM information_schema.columns 
WHERE table_name = 'ProjectBoards' 
AND column_name = 'Observed';

-- Verify EmployerAdds.JobDescription column
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'EmployerAdds' 
AND column_name = 'JobDescription';
```

### SQL Server:
```sql
-- Verify ProjectBoards.Observed column
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ProjectBoards'
AND COLUMN_NAME = 'Observed';

-- Verify EmployerAdds.JobDescription column
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'EmployerAdds'
AND COLUMN_NAME = 'JobDescription';
```

## Notes
- The `Observed` field defaults to `false` for all existing records
- The `JobDescription` field is nullable (TEXT) and can be left empty
- Both scripts are idempotent (safe to run multiple times)




