# Main Slack Team Creation Method

## Overview
The `CreateProjectTeamWithStatusChange` method is the **primary and most comprehensive method** for creating Slack teams with full workflow integration. This method handles the complete end-to-end process of setting up a project team.

## Endpoint
```
POST /api/slack/use/create-project-team-with-status-change
```

## Purpose
This method is designed to be the **main method** you use when you need to:
- Create a new Slack channel for a project
- Add team members (students) to the channel
- Update the JoinRequests table with all students
- Change project status from 'New' (1) to 'Planning' (2)
- Send welcome messages to the team

## Request Model
```csharp
public class CreateProjectTeamWithStatusRequest
{
    [Required]
    public int ProjectId { get; set; }
    
    [Required]
    public int RequestStudentId { get; set; }
    
    [Required]
    [MinLength(1, ErrorMessage = "At least one student ID is required")]
    public List<int> StudentIds { get; set; } = new List<int>();
    
    public bool SendWelcomeMessage { get; set; } = true;
}
```

## Request Example
```json
{
    "projectId": 123,
    "requestStudentId": 1,
    "studentIds": [1, 2, 3, 4, 5],
    "sendWelcomeMessage": true
}
```

## What This Method Does

### 1. **Admin Validation**
- Validates that the requesting student exists
- Ensures the requesting student is an admin (IsAdmin = true)
- Returns "Only admins can create teams" if not admin

### 2. **Project & Student Validation**
- Retrieves the project with all related data
- Validates that the project status is 'New' (StatusId = 1)
- Retrieves and validates all student IDs exist

### 3. **Business Rule Validation**
- Ensures at least one student has a "Backend Developer" role (Role.Id = 3)
- Ensures at least one student is designated as admin
- Validates all student IDs are valid

### 4. **Slack Team Creation**
- Creates a new Slack channel for the project
- Adds all students to the channel using their email addresses
- Sends welcome message if requested

### 5. **Database Updates**
- Creates JoinRequest records for all students in the JoinRequests table
- Changes project status from 'New' (1) to 'Planning' (2)
- Updates project timestamp

### 6. **Comprehensive Response**
- Returns detailed information about the created team
- Includes project information, Slack team details, and join request statistics

## Response Model
```csharp
public class SlackTeamCreationWithStatusResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public Project? Project { get; set; }
    public SlackTeamInfo? SlackTeam { get; set; }
    public SlackTeamCreationResult? SlackCreationResult { get; set; }
    public int JoinRequestsCreated { get; set; }
    public bool ProjectStatusChanged { get; set; }
    public string? NewProjectStatus { get; set; }
}
```

## Response Example
```json
{
    "success": true,
    "message": "Project team created successfully! Slack channel '123_myproject_team' created, project status changed to 'Planning', and 5 join requests created.",
    "project": {
        "id": 123,
        "title": "My Project",
        "statusId": 2,
        "status": {
            "name": "Planning"
        }
    },
    "slackTeam": {
        "projectId": 123,
        "projectTitle": "My Project",
        "channelId": "C1234567890",
        "channelName": "123_myproject_team",
        "teamName": "123_myproject_team",
        "totalMembers": 5,
        "adminCount": 1,
        "regularMemberCount": 4,
        "members": [
            {
                "studentId": 1,
                "firstName": "John",
                "lastName": "Doe",
                "email": "john.doe@example.com",
                "isAdmin": true,
                "isInSlack": false
            }
        ]
    },
    "slackCreationResult": {
        "success": true,
        "channelId": "C1234567890",
        "channelName": "123_myproject_team",
        "teamName": "123_myproject_team",
        "memberResults": [...],
        "welcomeMessageSent": true
    },
    "joinRequestsCreated": 5,
    "projectStatusChanged": true,
    "newProjectStatus": "Planning"
}
```

## Business Rules

### Prerequisites
1. **Admin Request**: The requesting student must be an admin (IsAdmin = true)
2. **Project Status**: Project must have status 'New' (StatusId = 1)
3. **Backend Developer**: At least one student must have "Backend Developer" role (Role.Id = 3)
4. **Admin Required**: At least one student must be designated as admin
5. **Valid Students**: All provided student IDs must exist in the database

### What Happens
1. **Slack Channel**: Creates a private Slack channel named `{ProjectId}_{ProjectTitle}_team`
2. **Team Members**: Adds all students to the channel using their email addresses
3. **Join Requests**: Creates JoinRequest records for tracking student workspace invitations
4. **Status Change**: Automatically changes project status from 'New' to 'Planning'
5. **Welcome Message**: Sends a welcome message with project details to the channel

## Error Handling

### Common Error Responses
- **404 Not Found**: Project, requesting student, or team students not found
- **400 Bad Request**: 
  - Requesting student is not an admin ("Only admins can create teams")
  - Project status is not 'New'
  - Missing Backend Developer role (Role.Id = 3)
  - No admin students in the team
  - Invalid student IDs
  - Student has no email address
  - Slack channel name/ID is missing
  - Slack creation failed
- **500 Internal Server Error**: 
  - Database constraint violations (duplicate data, foreign key issues, null constraints)
  - Specific database error messages with detailed context
  - Partial success response if Slack team created but database tracking failed
  - Unexpected system errors

### Database Issue Resolution
- **Fixed**: Foreign key constraint issues with JoinRequest table
- **Automatic Cleanup**: Removes existing unprocessed join requests before creating new ones
- **Enhanced Logging**: Detailed logging for database operations and error diagnosis

## Usage Guidelines

### When to Use This Method
- ✅ **Primary method** for creating new project teams
- ✅ When you need to set up a complete project workflow
- ✅ When you want to change project status automatically
- ✅ When you need to track join requests for students

### When NOT to Use This Method
- ❌ For testing Slack functionality (use test methods instead)
- ❌ When you only want to create a channel without status changes
- ❌ When you need to add students to existing channels

## Integration with Other Systems

### JoinRequests Table
This method automatically creates JoinRequest records for all students, which can be used to:
- Track which students need workspace invitations
- Monitor team setup progress
- Generate reports on team creation

### Project Status Workflow
The method integrates with the project status workflow by:
- Automatically advancing projects from 'New' to 'Planning'
- Ensuring proper business rule validation
- Maintaining audit trails with timestamps

## Testing
The method includes comprehensive test coverage:
- Invalid project ID handling
- Empty student ID validation
- Invalid student ID handling
- Business rule validation
- Error response testing

## Logging
The method provides detailed logging for:
- Workflow start and completion
- Business rule validation results
- Slack creation success/failure
- Database operation results
- Error conditions and troubleshooting

## Conclusion
This method is designed to be your **go-to solution** for creating project teams. It handles all the complexity of team setup, status management, and tracking in a single, robust operation. Use this method whenever you need to set up a new project team with full workflow integration.
