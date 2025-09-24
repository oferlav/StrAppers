# Frontend Use Endpoints

This document lists all the API endpoints marked with the `/use/` prefix that are intended for frontend consumption.

## 📋 **Updated Endpoints**

### 1. **Student Registration Screen**

#### Create New Student
- **Endpoint**: `POST /api/students/use/create`
- **Purpose**: Add new student in student registration screen
- **Controller**: `StudentsController.CreateStudent`
- **Request Model**: `CreateStudentRequest`

#### Get Available Majors
- **Endpoint**: `GET /api/majors/use`
- **Purpose**: Populate majors dropdown for student registration screen
- **Controller**: `MajorsController.GetMajors`
- **Returns**: List of active majors

#### Get Available Years
- **Endpoint**: `GET /api/years/use`
- **Purpose**: Populate years dropdown for student registration screen
- **Controller**: `YearsController.GetYears`
- **Returns**: List of active years ordered by sort order

### 2. **Project Allocation Screen**

#### Get All Roles
- **Endpoint**: `GET /api/roles/use`
- **Purpose**: Populate roles list for project allocation screen
- **Controller**: `RolesController.GetRoles`
- **Returns**: List of active roles

#### Get Available Projects
- **Endpoint**: `GET /api/projectallocation/use/available-projects`
- **Purpose**: Show all available projects (status 1) for project allocation screen
- **Controller**: `ProjectAllocationController.GetAvailableProjects`
- **Returns**: List of projects without admin (available for allocation)

#### Get Students Allocated to Project
- **Endpoint**: `GET /api/projectallocation/use/project/{projectId}/students`
- **Purpose**: List all students allocated to a specific project
- **Controller**: `ProjectAllocationController.GetProjectStudentsForFrontend`
- **Returns**: List of students with full details (major, year, organization, roles)

#### Allocate Student to Project
- **Endpoint**: `POST /api/projectallocation/use/allocate`
- **Purpose**: Allocate a student to a project (updates Students table with ProjectId)
- **Controller**: `ProjectAllocationController.AllocateStudentToProject`
- **Request Model**: `ProjectAllocationRequest`

#### Deallocate Student from Project
- **Endpoint**: `POST /api/projectallocation/use/deallocate/{studentId}`
- **Purpose**: Remove student from project allocation (removes ProjectId from Students table)
- **Controller**: `ProjectAllocationController.DeallocateStudent`
- **Returns**: `ProjectAllocationResponse`

### 3. **Project Registration Screen**

#### Create New Project
- **Endpoint**: `POST /api/projects/use/create`
- **Purpose**: Add new project in project registration screen
- **Controller**: `ProjectsController.CreateProject`
- **Request Model**: `CreateProjectRequest`

#### Get Project Status by ID
- **Endpoint**: `GET /api/projects/use/status/{statusId}`
- **Purpose**: Get project status name and details by status ID
- **Controller**: `ProjectsController.GetProjectStatusById`
- **Returns**: `ProjectStatus` object with Name, Description, Color, etc.

### 4. **Organization Management**

#### Create New Organization
- **Endpoint**: `POST /api/organizations/use/create`
- **Purpose**: Add new organization for student/project registration
- **Controller**: `OrganizationsController.CreateOrganization`
- **Request Model**: `CreateOrganizationRequest`

### 5. **Slack Team Management**

#### Create Project Team with Status Change
- **Endpoint**: `POST /api/slack/use/create-project-team-with-status-change`
- **Purpose**: Main method for creating Slack teams with full workflow integration
- **Controller**: `SlackController.CreateProjectTeamWithStatusChange`
- **Request Model**: `CreateProjectTeamWithStatusRequest`

## 🎯 **Usage Guidelines**

### For Frontend Developers:
- Use only the endpoints marked with `/use/` prefix for frontend functionality
- These endpoints are specifically designed and tested for frontend consumption
- All other endpoints without `/use/` prefix are for internal/admin use

### For Backend Developers:
- When adding new frontend-facing endpoints, use the `/use/` prefix
- Keep the original endpoints for backward compatibility and internal use
- Update this document when adding new `/use/` endpoints

## 📝 **Request/Response Examples**

### Student Registration
```json
POST /api/students/use/create
{
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "studentId": "STU001",
    "majorId": 1,
    "yearId": 3,
    "linkedInUrl": "https://linkedin.com/in/johndoe",
    "organizationId": 1
}
```

### Project Registration
```json
POST /api/projects/use/create
{
    "title": "New Project",
    "description": "Project description",
    "statusId": 1,
    "priority": "High",
    "startDate": "2025-01-01",
    "dueDate": "2025-06-01",
    "organizationId": 1
}
```

### Project Status Lookup
```json
GET /api/projects/use/status/{statusId}
```

**Response Example:**
```json
{
    "id": 1,
    "name": "New",
    "description": "Newly created project",
    "color": "#10B981",
    "sortOrder": 1,
    "isActive": true,
    "createdAt": "2025-08-06T07:10:23Z",
    "updatedAt": null
}
```

### Organization Creation
```json
POST /api/organizations/use/create
{
    "name": "New Tech Company",
    "description": "A technology company focused on innovation",
    "website": "https://newtech.com",
    "contactEmail": "contact@newtech.com",
    "phone": "555-0123",
    "address": "123 Tech Street, Tech City",
    "type": "Company",
    "isActive": true
}
```

**Response Example:**
```json
{
    "id": 4,
    "name": "New Tech Company",
    "description": "A technology company focused on innovation",
    "website": "https://newtech.com",
    "contactEmail": "contact@newtech.com",
    "phone": "555-0123",
    "address": "123 Tech Street, Tech City",
    "type": "Company",
    "isActive": true,
    "createdAt": "2025-01-14T16:45:00Z",
    "updatedAt": null,
    "students": [],
    "projects": []
}
```

### Slack Team Creation
```json
POST /api/slack/use/create-project-team-with-status-change
{
    "projectId": 123,
    "requestStudentId": 1,
    "studentIds": [1, 2, 3, 4, 5],
    "sendWelcomeMessage": true
}
```

### Get Project Students
```
GET /api/projectallocation/use/project/123/students
```

**Response Example:**
```json
[
    {
        "id": 1,
        "firstName": "John",
        "lastName": "Doe",
        "email": "john.doe@example.com",
        "isAdmin": true,
        "major": {
            "id": 1,
            "name": "Computer Science"
        },
        "year": {
            "id": 3,
            "name": "Junior"
        },
        "organization": {
            "id": 1,
            "name": "Tech University"
        },
        "studentRoles": [
            {
                "id": 1,
                "role": {
                    "id": 1,
                    "name": "Project Manager"
                },
                "isActive": true
            }
        ]
    }
]
```

### Allocate Student to Project
```json
POST /api/projectallocation/use/allocate
{
    "studentId": 123,
    "projectId": 456,
    "isAdmin": false
}
```

**Response Example:**
```json
{
    "success": true,
    "message": "Student successfully allocated to project 'Mobile App Development'",
    "student": {
        "id": 123,
        "firstName": "Jane",
        "lastName": "Smith",
        "projectId": 456,
        "isAdmin": false
    },
    "project": {
        "id": 456,
        "title": "Mobile App Development",
        "hasAdmin": false
    }
}
```

### Deallocate Student from Project
```
POST /api/projectallocation/use/deallocate/123
```

**Response Example:**
```json
{
    "success": true,
    "message": "Student successfully removed from project 'Mobile App Development'",
    "student": {
        "id": 123,
        "firstName": "Jane",
        "lastName": "Smith",
        "projectId": null,
        "isAdmin": false
    }
}
```

## 🔄 **Migration Notes**

- All original endpoints remain functional for backward compatibility
- Frontend should gradually migrate to use the new `/use/` endpoints
- Test all endpoints thoroughly before deploying to production
- Monitor logs for any issues with the new routing structure
