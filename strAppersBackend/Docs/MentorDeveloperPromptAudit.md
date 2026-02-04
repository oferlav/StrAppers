# Mentor developer prompt – audit and ordering

## 1. All developer prompt pieces (nothing missed)

### General mentor flow (HandleGeneralMentorResponse)

| # | Source | Content | When (developer only?) |
|---|--------|--------|------------------------|
| 1 | `GetPlatformContextAndLimitations()` | Platform context, mission, team roles, scouting, knowledge limits | All |
| 2 | `PromptConfig.Mentor.SystemPrompt` | Base mentor + Trello + code review rule + code snippets + **CRITICAL ASSUMPTIONS - GITHUB** (account exists, repo exists, git workflow only) | All |
| 3 | `BuildEnhancedSystemPrompt` | CURRENT CONTEXT (user, tasks, team, meeting) | All |
| 4 | `GitHubContextTemplate` + commit summary | GITHUB REPOSITORY STATUS | Yes – only if `isDeveloperRole && githubContextInfo` |
| 5 | `databaseInfoSection` | DATABASE CONNECTION INFORMATION (pointer to PROJECT INFORMATION / FALLBACK later) | Yes – only if `isDeveloperRole && databasePassword` |
| 6 | `DeveloperCapabilitiesInfo` | Repo access, README/connection parsing, Web API; references base prompt for GitHub/backticks | Yes – only if `isDeveloperRole` |
| 7 | `ContextReminder` | Tasks, deadlines, task breakdown alignment | All |
| 8 | Conversation note | New vs existing chat; no greeting if existing | All |
| 9 | `githubAccountInfo` | CRITICAL GITHUB INFORMATION + username + repo URL + backticks | Yes – when developer and (asking repo/repeat or has user/repo) |
| 10 | `repositoryStatusInfo` | “You have access to repo”, commits or no commits | Yes – when developer and (asking repo/connection) and status built |
| 11 | PROJECT INFORMATION (FROM README) | Full README content | Yes – when developer and `readmeContent` not empty |
| 12 | DATABASE CONNECTION INFORMATION (FALLBACK) | DB name, user, password, optional full connection string | Yes – when developer and no connection string in README and board has id/password |
| 13 | ABSOLUTELY CRITICAL INSTRUCTIONS | 16 rules for DB connection (use README/fallback above, ignore chat history) | Yes – when developer |
| 14 | WEB API INFORMATION | WebApi URL, Swagger URL from README | Yes – when developer and URLs extracted |
| 15 | Note when asking connection but README missing | “Information not available in repository” | Yes – when asking connection and no README |
| 16 | `codeReviewWarning` | “No commits – say so, don’t fake review” | Yes – when asking review and no commits |

### Code review flow (HandleCodeReviewIntent)

- Uses `PromptConfig.Mentor.CodeReview.ReviewSystemPrompt`, `ReviewUserPromptHeader`, `ReviewInstructions` plus dynamic task/module/code diff. Separate from the general prompt; no pieces missing.

### Conclusion

- All developer-specific content in the **general** flow is covered by items 4–6 and 9–16.
- Code review uses its own config blocks; no overlap with the list above.
- Nothing is missed.

---

## 2. Ordering (final sequence)

Order in the assembled system prompt:

1. Platform context  
2. Base system prompt (Mentor.SystemPrompt)  
3. CURRENT CONTEXT  
4. GitHub context section (if developer + commit summary)  
5. DATABASE CONNECTION INFORMATION (early pointer – “use PROJECT INFORMATION / FALLBACK **provided later**”)  
6. DeveloperCapabilitiesInfo / ContextReminder  
7. Conversation note  
8. CRITICAL GITHUB INFORMATION (githubAccountInfo)  
9. repositoryStatusInfo  
10. **=== PROJECT INFORMATION (FROM README) ===**  
11. **=== DATABASE CONNECTION INFORMATION (FALLBACK) ===** (if needed)  
12. **ABSOLUTELY CRITICAL INSTRUCTIONS** (“from the PROJECT INFORMATION section **above**”)  
13. WEB API INFORMATION  
14. Note (connection asked but README missing)  
15. codeReviewWarning  

So:

- “Above” in step 12 correctly refers to README (step 10) and/or FALLBACK (step 11).  
- The early DB block (step 5) says “provided later in this prompt”, which matches steps 10–11.  
- Ordering is correct.

---

## 3. Duplications (and fixes)

| Duplication | Where | Fix |
|-------------|--------|-----|
| “Student already has GitHub account / repo exists” + “focus on git workflow only” | SystemPrompt, DeveloperCapabilitiesInfo, githubAccountInfo | Keep in SystemPrompt; DeveloperCapabilitiesInfo and githubAccountInfo reference base prompt and add only concrete details (e.g. username/URL) and “wrap commands in backticks”. |
| “Wrap code/commands in triple backticks” | SystemPrompt, DeveloperCapabilitiesInfo, githubAccountInfo | Keep once in SystemPrompt; in DeveloperCapabilitiesInfo say “same as base”; in githubAccountInfo keep one short reminder next to Git steps. |
| Database “use README / ignore chat history” | databaseInfoSection (early), ABSOLUTELY CRITICAL INSTRUCTIONS (later) | Early block is already a short pointer (“use PROJECT INFORMATION or FALLBACK provided later”); long rules only in CRITICAL INSTRUCTIONS. No further change. |

Applied in code:
- **DeveloperCapabilitiesInfo** (appsettings.json, appsettings.Production.json, publish-prod): Removed duplicate "student already has GitHub account... focus on workflow" and duplicate "CODE FORMATTING" sentence. Now references "base prompt and any CRITICAL GITHUB INFORMATION section below when present."
- **githubAccountInfo** (MentorController): Replaced the 6 repeated rules with one line: "Apply the base prompt's GitHub rules (no account/repo creation; Git workflow only). When giving Git instructions, use the details below and ALWAYS wrap commands in triple backticks." Kept dynamic username and repository URL.
- **ABSOLUTELY CRITICAL INSTRUCTIONS** (MentorController): Points 1 and 6 rephrased so they work when only FALLBACK is present (no PROJECT INFORMATION): "get the EXACT connection string from the PROJECT INFORMATION (FROM README) section above if present; otherwise use the DATABASE CONNECTION INFORMATION (FALLBACK) section above" and "from the section(s) above (README if present, otherwise FALLBACK)."

### Log check (latest DebugSystemPrompt run)

- **Ordering:** Correct. Early DB block says "provided later in this prompt"; FALLBACK appears before CRITICAL INSTRUCTIONS; "above" now correctly covers both README-if-present and FALLBACK.
- **Duplications:** None. GitHub/repo rules appear once in base prompt; CRITICAL GITHUB INFORMATION references base prompt. Code-formatting in base + short reminder in CRITICAL GITHUB INFORMATION. DB rules only in CRITICAL INSTRUCTIONS. "WEB API" in DeveloperCapabilitiesInfo is a general statement ("URLs are available in project information"); the later "=== WEB API INFORMATION ===" block provides the actual URLs—intentional, not duplicate.
- **All developer pieces:** Present for this request: Platform context, base SystemPrompt, CURRENT CONTEXT, GitHub context ("No commits found"), database early pointer, DeveloperCapabilitiesInfo + WEB API blurb, ContextReminder, conversation note, CRITICAL GITHUB INFORMATION (username, repo URL), FALLBACK (no README fetched for "check again"), CRITICAL INSTRUCTIONS. Conditional pieces correctly absent: repositoryStatusInfo (user didn't ask about repo/connection), PROJECT INFORMATION (README not fetched), WEB API block (no README), codeReviewWarning (no review request).

---

## 4. Web API URL for mentor (backend + frontend README)

- **Mentor gets actual API URL from:** (1) README (backend or frontend) if it contains a full `https://...` WebApi/API URL; (2) **fallback:** `ProjectBoard.WebApiUrl` when README has only a relative path (e.g. `/swagger`) because the domain was set after repo creation.
- **Backend README:** After Railway domain is created, `UpdateBackendReadmeWithWebApiUrlsAsync` rewrites backend README with full WebApi URL and Swagger URL so future mentor context and students see the real URL.
- **Frontend README:** After Railway domain is created, `UpdateFrontendReadmeWithWebApiUrlsAsync` rewrites frontend README with full Backend API URL (same flow as backend README update in BoardsController). Frontend README then shows the actual API URL for students and for any context that reads the frontend repo.
