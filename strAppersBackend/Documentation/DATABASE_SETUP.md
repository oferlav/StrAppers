# PostgreSQL Database Setup

## Prerequisites
1. Install PostgreSQL on your machine
2. Create a database user (or use the default `postgres` user)
3. Update the connection string in `appsettings.json` and `appsettings.Development.json`

## Connection String Configuration
Update the connection strings in your configuration files:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=StrAppersDB;Username=postgres;Password=your_actual_password"
  }
}
```

## Database Migration Commands

### 1. Install EF Core Tools (if not already installed)
```bash
dotnet tool install --global dotnet-ef
```

### 2. Create Initial Migration
```bash
cd strAppersBackend
dotnet ef migrations add InitialCreate
```

### 3. Update Database
```bash
dotnet ef database update
```

### 4. (Optional) Remove Migration
```bash
dotnet ef migrations remove
```

## Database Schema
The initial migration will create the following tables:

### Users Table
- `Id` (Primary Key, Auto-increment)
- `Name` (Required, Max 100 characters)
- `Email` (Required, Max 255 characters, Unique)
- `CreatedAt` (Default: Current timestamp)

### Organizations Table
- `Id` (Primary Key, Auto-increment)
- `Name` (Required, Max 200 characters)
- `Description` (Max 500 characters)
- `Website` (Max 100 characters)
- `ContactEmail` (Max 255 characters)
- `Phone` (Max 20 characters)
- `Address` (Max 200 characters)
- `Type` (Max 50 characters)
- `IsActive` (Boolean, Default: true)
- `CreatedAt` (Default: Current timestamp)
- `UpdatedAt` (Nullable)

### Students Table
- `Id` (Primary Key, Auto-increment)
- `FirstName` (Required, Max 100 characters)
- `LastName` (Required, Max 100 characters)
- `Email` (Required, Max 255 characters, Unique)
- `StudentId` (Max 20 characters, Unique)
- `Major` (Max 50 characters)
- `Year` (Max 50 characters)
- `OrganizationId` (Foreign Key to Organizations)
- `CreatedAt` (Default: Current timestamp)
- `UpdatedAt` (Nullable)

### Projects Table
- `Id` (Primary Key, Auto-increment)
- `Title` (Required, Max 200 characters)
- `Description` (Max 1000 characters)
- `Status` (Max 50 characters, Default: "Planning")
- `Priority` (Max 50 characters, Default: "Medium")
- `StartDate` (Nullable)
- `EndDate` (Nullable)
- `DueDate` (Nullable)
- `OrganizationId` (Foreign Key to Organizations)
- `CreatedAt` (Default: Current timestamp)
- `UpdatedAt` (Nullable)

### Roles Table
- `Id` (Primary Key, Auto-increment)
- `Name` (Required, Max 100 characters)
- `Description` (Max 500 characters)
- `Category` (Max 50 characters, Default: "General")
- `IsActive` (Boolean, Default: true)
- `CreatedAt` (Default: Current timestamp)
- `UpdatedAt` (Nullable)

### StudentRoles Table (Many-to-Many)
- `Id` (Primary Key, Auto-increment)
- `StudentId` (Foreign Key to Students)
- `RoleId` (Foreign Key to Roles)
- `AssignedDate` (Default: Current timestamp)
- `EndDate` (Nullable)
- `Notes` (Max 200 characters)
- `IsActive` (Boolean, Default: true)

### StudentProjects Table (Many-to-Many)
- `StudentId` (Foreign Key to Students)
- `ProjectId` (Foreign Key to Projects)

## Test Data Seeding
The following test data will be automatically inserted when you run migrations:

### Organizations (3 records)
- Tech University (University)
- Innovation Labs (Company)
- Code for Good (Non-profit)

### Students (5 records)
- Alex Johnson (Computer Science, Junior)
- Sarah Williams (Software Engineering, Senior)
- Michael Brown (Data Science, Graduate)
- Emily Davis (Cybersecurity, Sophomore)
- David Miller (Computer Science, Freshman)

### Projects (5 records)
- Student Management System (In Progress, High Priority)
- AI Research Platform (Planning, Medium Priority)
- Community Outreach App (Completed, High Priority)
- Online Learning Platform (In Progress, Critical Priority)
- Data Analytics Dashboard (On Hold, Medium Priority)

### Roles (8 records)
- Project Manager (Leadership)
- Frontend Developer (Technical)
- Backend Developer (Technical)
- UI/UX Designer (Technical)
- Quality Assurance (Technical)
- Team Lead (Leadership)
- Research Assistant (Academic)
- Documentation Specialist (Administrative)

### Student-Role Assignments (8 records)
- Alex Johnson: Project Manager, Team Lead
- Sarah Williams: Frontend Developer, Documentation Specialist
- Michael Brown: Backend Developer, Research Assistant
- Emily Davis: UI/UX Designer
- David Miller: Quality Assurance

## Using the Database in Controllers
Inject `ApplicationDbContext` into your controllers:

```csharp
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }
}
```

## Troubleshooting
- Ensure PostgreSQL service is running
- Verify connection string credentials
- Check that the database exists (EF Core will create it if it doesn't exist)
- Make sure the `postgres` user has CREATE DATABASE privileges
