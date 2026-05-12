# Dokxai / StrAppers — Accounts & Infrastructure Reference

## Microsoft 365 / Azure Tenant

| Field | Value |
|-------|-------|
| Tenant ID | `ab73d17f-f7e3-463d-a948-2ae025be44f4` |
| Tenant domain | `Dokxai.onmicrosoft.com` |

---

## Key Accounts

### Global / Azure Admin
| Field | Value |
|-------|-------|
| Email | `azure-admin@Dokxai.onmicrosoft.com` |
| Role | Global Admin, Teams Admin, SharePoint Admin |
| License | Microsoft 365 Business Premium |
| Used for | Azure Portal, Teams Admin Center, SharePoint Admin, PowerShell admin sessions |
| Password | Familiar password (known) |

### Teams Service Account (Meeting Organizer)
| Field | Value |
|-------|-------|
| Email | `skill-in-meetings@Dokxai.onmicrosoft.com` |
| User Object ID (Oid) | `05d3dc91-c4cb-4ff1-8b27-cc4a3e91da6c` |
| Role | Meeting organizer for all board meetings |
| License | Microsoft 365 Business Premium |
| Used for | Creating Teams meetings via Graph API, organizer of all student board meetings |
| Password | Familiar password (known) |
| Notes | OneDrive must be provisioned for recordings/transcripts to upload — see below |

---

## Azure App Registration (Microsoft Graph API)

| Field | Value |
|-------|-------|
| App Name | `skill-in` (in Azure Portal → App Registrations) |
| Client ID | `14850f4e-3d65-4c86-9c5f-a73ce1026ad6` |
| Tenant ID | `ab73d17f-f7e3-463d-a948-2ae025be44f4` |
| Client Secret | In `appsettings.json` → `MicrosoftGraph:ClientSecret` |
| ServiceAccountEmail | `skill-in-meetings@Dokxai.onmicrosoft.com` |
| ServiceAccountUserId | `dca0634d-d13d-4d0e-af73-f0729fda1a42` *(note: this is a different Oid — verify which is current)* |

### API Permissions granted
- `OnlineMeetings.ReadWrite.All` (Application)
- `Calendars.ReadWrite` (Application)
- `Mail.Send` (Application)
- `OnlineMeetingTranscript.Read.All` (Application) ✓ added May 2026
- `CallRecords.Read.All` (Application)

---

## Teams Application Access Policy

Required for Graph API to read transcripts/meetings for specific users.

```powershell
# Already created (May 2026):
New-CsApplicationAccessPolicy -Identity "TranscriptAccess" -AppIds "14850f4e-3d65-4c86-9c5f-a73ce1026ad6" -Description "Allow transcript access"
Grant-CsApplicationAccessPolicy -PolicyName "TranscriptAccess" -Identity "dca0634d-d13d-4d0e-af73-f0729fda1a42"
```

To run again or grant to additional users, connect via:
```powershell
Connect-MicrosoftTeams  # sign in as azure-admin@Dokxai.onmicrosoft.com
```

---

## OneDrive Provisioning (for skill-in-meetings)

Recordings and transcripts upload to the organizer's (skill-in-meetings) OneDrive.
If upload fails with "owner's OneDrive isn't set up", run:

```powershell
Install-Module -Name Microsoft.Online.SharePoint.PowerShell -Force
Connect-SPOService -Url https://dokxai-admin.sharepoint.com  # sign in as azure-admin
Request-SPOPersonalSite -UserEmails @("skill-in-meetings@Dokxai.onmicrosoft.com") -NoWait
```

Wait 5–10 minutes after running, then test a new meeting.

---

## Admin Portals

| Portal | URL |
|--------|-----|
| Microsoft 365 Admin Center | https://admin.microsoft.com |
| Azure Portal | https://portal.azure.com |
| Teams Admin Center | https://admin.teams.microsoft.com |
| SharePoint Admin | https://dokxai-admin.sharepoint.com |
| Azure App Registrations | https://portal.azure.com → App Registrations → `skill-in` |

> **Note on GoDaddy redirect**: Navigating to admin.microsoft.com may redirect to GoDaddy (domain is registered there). Use a private/incognito browser or append `?auth_upn=azure-admin@Dokxai.onmicrosoft.com` to the URL to bypass.

---

## Backend Deployment

| Field | Value |
|-------|-------|
| App Service | `skill-in-backend-dvdmgbe7fmhmg4hp.eastus2-01.azurewebsites.net` |
| Kudu / ZipDeploy | `https://skill-in-backend-dvdmgbe7fmhmg4hp.scm.eastus2-01.azurewebsites.net/ZipDeployUI` |
| Git repo | `https://github.com/oferlav/StrAppers.git` |
| Deploy branch | `main` |
| Runtime | .NET 8 |

---

## Notes

- Meetings are created by `skill-in-meetings` as organizer → all recordings/transcripts go to their OneDrive
- The Oid embedded in Teams join URLs (`context` param) identifies the organizer — currently `05d3dc91-c4cb-4ff1-8b27-cc4a3e91da6c`
- `ServiceAccountUserId` in appsettings (`dca0634d-...`) may differ from the organizer Oid in join URLs (`05d3dc91-...`) — verify these are consistent
