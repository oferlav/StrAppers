# Base44 `functions.invoke` index (from `src/`)

**Purpose:** Single list of every **`base44.functions.invoke('name', …)`** used in the React app so cutover work (§3 in `BASE44_MIGRATION_PLAN.md`) does not miss a call site.

**Policy reminder:** No new backend routes required for the current app; see **`BASE44_BACKEND_ENDPOINT_AUDIT.md`** for obsolete URL patterns vs **`BASE44_AUDIT_API_PATHS_FROM_DISK.txt`** for each Deno function’s `fetch` target.

**Source path:** `C:\StrAppers\strAppersFrontend\src` (recursive `*.jsx` / `*.js`)

**Regenerate (PowerShell):**

```powershell
Get-ChildItem "C:\StrAppers\strAppersFrontend\src" -Recurse -Include *.jsx,*.js |
  Select-String -Pattern "invoke\s*\(\s*['\`"]([a-zA-Z0-9_]+)['\`"]" -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
  Sort-Object -Unique
```

---

## Invoke names (83 unique, sorted)

| # | Function name |
|---|----------------|
| 1 | `addBoardChatMessage` |
| 2 | `addPrivateChatMessage` |
| 3 | `allocateStudentToProject` |
| 4 | `allocateWithPriority` |
| 5 | `askCustomer` |
| 6 | `askMentor` |
| 7 | `bindModules` |
| 8 | `changeEmployerPassword` |
| 9 | `checkBranchStatus` |
| 10 | `checkCandidateFollowed` |
| 11 | `checkEmployerByEmail` |
| 12 | `checkEmployerInvite` |
| 13 | `checkIsOpenBranch` |
| 14 | `checkPRStatus` |
| 15 | `checkStudentAllocatable` |
| 16 | `createDataModel` |
| 17 | `createEmployer` |
| 18 | `createGithubBranch` |
| 19 | `createGithubPR` |
| 20 | `createProject` |
| 21 | `createStakeholder` |
| 22 | `deallocateStudentFromProject` |
| 23 | `deleteStakeholder` |
| 24 | `downloadProjectDesign` |
| 25 | `figmaLinks` |
| 26 | `getAllocatedProjectsByEmail` |
| 27 | `getAllOrganizations` |
| 28 | `getAvailableProjects` |
| 29 | `getBoardChat` |
| 30 | `getBoardGitStatuses` |
| 31 | `getBoardMedia` |
| 32 | `getBoardStats` |
| 33 | `getBugDetails` |
| 34 | `getCRMMetadata` |
| 35 | `getCustomerChatHistory` |
| 36 | `getDashboardStats` |
| 37 | `getDataSchema` |
| 38 | `getEmployerByEmail` |
| 39 | `getHotProjects` |
| 40 | `getMeetingUrl` |
| 41 | `getMentorChatHistory` |
| 42 | `getMentorModels` |
| 43 | `getModuleCount` |
| 44 | `getModuleDescription` |
| 45 | `getMyTasks` |
| 46 | `getOpenBugs` |
| 47 | `getPrivateChat` |
| 48 | `getProjectCriteria` |
| 49 | `getProjectEngagementRules` |
| 50 | `getProjectStudents` |
| 51 | `getRoles` |
| 52 | `getShortlistedCandidates` |
| 53 | `getSprintSchedule` |
| 54 | `getStakeholders` |
| 55 | `getStripImage` |
| 56 | `getStudentBoardData` |
| 57 | `getStudentByEmail` |
| 58 | `getStudentsByRole` |
| 59 | `getUserStories` |
| 60 | `initiateModules` |
| 61 | `inviteEmployerToBoard` |
| 62 | `logBugHandler` |
| 63 | `logBugToBackend` |
| 64 | `loginEmployer` |
| 65 | `mergeGithubBranch` |
| 66 | `modifyBug` |
| 67 | `notifyApplicant` |
| 68 | `observeBoard` |
| 69 | `pushMentorSystemMessage` |
| 70 | `refineModule` |
| 71 | `registerUser` |
| 72 | `removeCandidate` |
| 73 | `resourceLinks` |
| 74 | `reviewCode` |
| 75 | `saveBoardMedia` |
| 76 | `scheduleEmployerMeeting` |
| 77 | `setCandidate` |
| 78 | `setCheckDone` |
| 79 | `submitSupportRequest` |
| 80 | `updateDataModel` |
| 81 | `updateModule` |
| 82 | `updateProject` |
| 83 | `updateStakeholder` |

---

*Last regenerated against disk: 2026-03-19*
</think>


<｜tool▁calls▁begin｜><｜tool▁call▁begin｜>
Shell