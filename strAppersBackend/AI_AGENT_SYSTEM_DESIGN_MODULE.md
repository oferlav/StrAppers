# AI Agent System Design Module

## Overview
The AI Agent System Design Module provides intelligent system design generation and sprint planning capabilities using OpenAI GPT-4o. It consists of two main components:

1. **System Design Module**: Generates structured system design documents from project descriptions
2. **Sprint Planning Module**: Creates detailed sprint plans with epics, stories, and tasks

## Database Schema

### Projects Table Updates
- `SystemDesign` (TEXT): Stores the generated system design document as JSON
- `SystemDesignDoc` (BYTEA): Stores the embedded PDF version of the design document

### DesignVersions Table
- `Id` (SERIAL PRIMARY KEY): Unique identifier
- `ProjectId` (INTEGER): Foreign key to Projects table
- `VersionNumber` (INTEGER): Version number for design iterations
- `DesignDocument` (TEXT): The design document content
- `DesignDocumentPdf` (BYTEA): PDF version of the design document
- `CreatedAt` (TIMESTAMP): Creation timestamp
- `CreatedBy` (VARCHAR): Creator identifier
- `IsActive` (BOOLEAN): Active status flag

## API Endpoints

### System Design Controller (`/api/systemdesign`)

#### 1. Generate Design Document
**POST** `/api/systemdesign/use/generate-design-document`

Generates a comprehensive system design document for a project.

**Request Body:**
```json
{
  "projectId": 1,
  "extendedDescription": "A mobile app for campus events management...",
  "createdBy": "admin@university.edu"
}
```

**Response:**
```json
{
  "success": true,
  "message": "System design created successfully",
  "designVersionId": 1,
  "designDocument": "{...structured design document...}",
  "designDocumentPdf": "base64_encoded_pdf_bytes"
}
```

#### 2. Get Latest Design Version
**GET** `/api/systemdesign/use/project/{projectId}/latest-design`

Retrieves the most recent design version for a project.

**Response:**
```json
{
  "id": 1,
  "projectId": 1,
  "versionNumber": 1,
  "designDocument": "{...design document...}",
  "createdAt": "2024-01-15T10:30:00Z",
  "createdBy": "admin@university.edu",
  "isActive": true
}
```

#### 3. Get All Design Versions
**GET** `/api/systemdesign/use/project/{projectId}/design-versions`

Retrieves all design versions for a project.

#### 4. Get Design Version by ID
**GET** `/api/systemdesign/use/design-version/{designVersionId}`

Retrieves a specific design version.

#### 5. Get Current System Design
**GET** `/api/systemdesign/use/project/{projectId}/current-design`

Retrieves the current system design status for a project.

**Response:**
```json
{
  "id": 1,
  "title": "Campus Events App",
  "systemDesign": "{...design document...}",
  "hasSystemDesign": true,
  "hasSystemDesignPdf": true
}
```

### Sprint Planning Controller (`/api/sprintplanning`)

#### 1. Generate Sprint Plan
**POST** `/api/sprintplanning/use/generate-project-sprints`

Generates a detailed sprint plan based on the project's system design.

**Request Body:**
```json
{
  "projectId": 1,
  "sprintLengthWeeks": 2,
  "createdBy": "admin@university.edu"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Sprint plan generated successfully",
  "sprintPlan": {
    "epics": [
      {
        "id": "epic1",
        "name": "User Authentication",
        "description": "Implement user authentication system",
        "userStories": [
          {
            "id": "story1",
            "title": "User Registration",
            "description": "Allow users to register accounts",
            "acceptanceCriteria": "Users can create accounts with email/password",
            "tasks": [
              {
                "id": "task1",
                "title": "Create registration form",
                "description": "Build user registration form",
                "roleId": 2,
                "roleName": "Frontend Developer",
                "estimatedHours": 8,
                "priority": 1,
                "dependencies": []
              }
            ],
            "storyPoints": 5,
            "priority": 1
          }
        ],
        "priority": 1
      }
    ],
    "sprints": [
      {
        "sprintNumber": 1,
        "name": "Sprint 1",
        "startDate": "2024-01-15",
        "endDate": "2024-01-29",
        "tasks": [],
        "totalStoryPoints": 0,
        "roleWorkload": {
          "1": 20,
          "2": 15,
          "3": 10
        }
      }
    ],
    "totalSprints": 4,
    "totalTasks": 25,
    "estimatedWeeks": 8
  }
}
```

#### 2. Get Team Composition
**GET** `/api/sprintplanning/use/project/{projectId}/team-composition`

Retrieves the team composition for sprint planning.

**Response:**
```json
{
  "projectId": 1,
  "projectTitle": "Campus Events App",
  "hasSystemDesign": true,
  "teamRoles": [
    {
      "roleId": 1,
      "roleName": "Backend Developer",
      "studentCount": 2,
      "students": [
        {
          "id": 1,
          "firstName": "John",
          "lastName": "Doe",
          "email": "john.doe@university.edu"
        }
      ]
    }
  ],
  "totalStudents": 5
}
```

#### 3. Validate Sprint Readiness
**GET** `/api/sprintplanning/use/project/{projectId}/sprint-readiness`

Validates if a project is ready for sprint planning.

**Response:**
```json
{
  "projectId": 1,
  "projectTitle": "Campus Events App",
  "isReadyForSprintPlanning": true,
  "requirements": {
    "hasSystemDesign": true,
    "hasAllocatedStudents": true,
    "hasStudentRoles": true,
    "studentCount": 5,
    "roleCount": 3
  },
  "missingRequirements": []
}
```

## Configuration

### OpenAI Configuration
Add to `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here"
  }
}
```

## Usage Workflow

### 1. System Design Generation
1. Ensure project has `ExtendedDescription` filled
2. Allocate students with roles to the project
3. Call `POST /api/systemdesign/use/generate-design-document`
4. System generates structured design document
5. Design is stored in `Projects.SystemDesign` and `DesignVersions` table

### 2. Sprint Planning
1. Ensure project has generated system design
2. Verify students are allocated with roles
3. Call `POST /api/sprintplanning/use/generate-project-sprints`
4. System generates detailed sprint plan with epics, stories, and tasks
5. Plan includes role-based task allocation and workload distribution

## Error Handling

All endpoints include comprehensive error handling:
- Validation errors (400 Bad Request)
- Not found errors (404 Not Found)
- Server errors (500 Internal Server Error)
- Detailed error messages for debugging

## Dependencies

- OpenAI API (GPT-4o)
- Entity Framework Core
- PostgreSQL Database
- ASP.NET Core 8.0

## Security Considerations

- OpenAI API key stored securely in configuration
- Input validation on all endpoints
- Proper error handling without exposing sensitive information
- Role-based access control (can be extended)

## Future Enhancements

- PDF generation using iTextSharp or Puppeteer
- GitHub integration for automatic issue creation
- Design document templates
- Sprint plan export to various formats
- Integration with project management tools
