using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text;
using System.Linq;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IDEController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IDEController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAIService _aiService;

    public IDEController(
        ApplicationDbContext context,
        ILogger<IDEController> logger,
        IConfiguration configuration,
        IAIService aiService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _aiService = aiService;
    }

    /// <summary>
    /// Generate initial IDE codebase structure based on SystemDesign and student's programming language
    /// </summary>
    [HttpGet("use/code-base")]
    public async Task<ActionResult<CodebaseStructureResponse>> GetCodebase(
        [FromQuery] int studentId,
        [FromQuery] int projectId,
        [FromQuery] string githubPagesUrl,
        [FromQuery] int mockRecordsPerTable = 10)
    {
        try
        {
            _logger.LogInformation("Generating codebase structure for StudentId: {StudentId}, ProjectId: {ProjectId}", studentId, projectId);

            // Fetch student with programming language
            var student = await _context.Students
                .Include(s => s.ProgrammingLanguage)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                _logger.LogWarning("Student with ID {StudentId} not found", studentId);
                return NotFound($"Student with ID {studentId} not found.");
            }

            if (student.ProgrammingLanguage == null)
            {
                _logger.LogWarning("Student {StudentId} does not have a programming language preference", studentId);
                return BadRequest($"Student with ID {studentId} does not have a programming language preference set.");
            }

            // Fetch project with SystemDesign
            var project = await _context.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                _logger.LogWarning("Project with ID {ProjectId} not found", projectId);
                return NotFound($"Project with ID {projectId} not found.");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                _logger.LogWarning("Project {ProjectId} does not have SystemDesign content", projectId);
                return BadRequest($"Project with ID {projectId} does not have a system design. Please generate the system design first.");
            }

            // Fetch project modules from database
            var projectModules = await _context.ProjectModules
                .Where(pm => pm.ProjectId == projectId)
                .OrderBy(pm => pm.Sequence)
                .ToListAsync();

            // Parse SystemDesign to extract modules and SQL scripts
            var parsedModules = ParseSystemDesignModules(project.SystemDesign);
            
            // Get SQL script from ProjectModules (ModuleType = 1 is the data model module)
            // Note: project.DataSchema contains base64 PNG image, not SQL text
            var dataModelModule = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 1);
            var sqlScript = dataModelModule?.Description ?? ExtractSqlScriptFromSystemDesign(project.SystemDesign);

            // Validate GitHub Pages URL
            if (string.IsNullOrEmpty(githubPagesUrl))
            {
                _logger.LogWarning("GitHub Pages URL is required but not provided");
                return BadRequest("GitHub Pages URL is required. Please provide the githubPagesUrl query parameter.");
            }

            // Generate codebase structure using AI
            var codebaseStructure = await GenerateCodebaseStructureAsync(
                project,
                student.ProgrammingLanguage,
                parsedModules,
                sqlScript,
                githubPagesUrl,
                mockRecordsPerTable);

            var response = new CodebaseStructureResponse
            {
                StudentId = studentId,
                ProjectId = projectId,
                ProgrammingLanguage = student.ProgrammingLanguage.Name,
                ProjectTitle = project.Title,
                CodebaseStructure = codebaseStructure
            };

            _logger.LogInformation("Successfully generated codebase structure for StudentId: {StudentId}, ProjectId: {ProjectId}", studentId, projectId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating codebase structure for StudentId: {StudentId}, ProjectId: {ProjectId}", studentId, projectId);
            return StatusCode(500, $"An error occurred while generating codebase structure: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse SystemDesign content to extract modules with inputs and outputs
    /// </summary>
    private List<ParsedModule> ParseSystemDesignModules(string systemDesign)
    {
        var modules = new List<ParsedModule>();
        
        try
        {
            // Pattern to match module sections (e.g., "### Module 1: Title" or "## Module Title")
            var modulePattern = new Regex(@"##+\s*(?:Module\s+\d+:|Module\s+\d+|Module:)?\s*(.+?)(?=\n|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var matches = modulePattern.Matches(systemDesign);

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var moduleTitle = match.Groups[1].Value.Trim();
                
                // Find the content between this module and the next one (or end of document)
                var startIndex = match.Index + match.Length;
                var endIndex = i < matches.Count - 1 ? matches[i + 1].Index : systemDesign.Length;
                var moduleContent = systemDesign.Substring(startIndex, endIndex - startIndex).Trim();

                // Extract inputs and outputs
                var inputs = ExtractField(moduleContent, "Inputs?:", "Outputs?:");
                var outputs = ExtractField(moduleContent, "Outputs?:", @"##+|$");

                modules.Add(new ParsedModule
                {
                    Title = moduleTitle,
                    Description = CleanDescription(moduleContent, inputs, outputs),
                    Inputs = inputs,
                    Outputs = outputs
                });
            }

            _logger.LogInformation("Parsed {Count} modules from SystemDesign", modules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing SystemDesign modules, attempting alternative parsing");
            // Fallback: split by common separators
            var sections = systemDesign.Split(new[] { "---", "###", "##" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var section in sections)
            {
                if (section.Trim().Length > 50) // Only process substantial sections
                {
                    modules.Add(new ParsedModule
                    {
                        Title = ExtractTitle(section),
                        Description = section.Trim(),
                        Inputs = ExtractField(section, "Inputs?:", "Outputs?:"),
                        Outputs = ExtractField(section, "Outputs?:", @"##+|$")
                    });
                }
            }
        }

        return modules;
    }

    private string ExtractTitle(string content)
    {
        var lines = content.Split('\n');
        return lines.Length > 0 ? lines[0].Trim().TrimStart('#').Trim() : "Untitled Module";
    }

    private string ExtractField(string content, string startPattern, string endPattern)
    {
        var pattern = new Regex($@"{startPattern}\s*(.+?)(?={endPattern})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var match = pattern.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private string CleanDescription(string content, string inputs, string outputs)
    {
        // Remove inputs and outputs from description
        var cleaned = content;
        if (!string.IsNullOrEmpty(inputs))
        {
            cleaned = Regex.Replace(cleaned, @"Inputs?:\s*" + Regex.Escape(inputs), "", RegexOptions.IgnoreCase);
        }
        if (!string.IsNullOrEmpty(outputs))
        {
            cleaned = Regex.Replace(cleaned, @"Outputs?:\s*" + Regex.Escape(outputs), "", RegexOptions.IgnoreCase);
        }
        return cleaned.Trim();
    }

    /// <summary>
    /// Extract SQL script from SystemDesign content (fallback method)
    /// Note: DataSchema field contains base64 PNG, not SQL. SQL should come from ProjectModules.
    /// </summary>
    private string ExtractSqlScriptFromSystemDesign(string systemDesign)
    {
        if (string.IsNullOrEmpty(systemDesign))
        {
            return string.Empty;
        }

        // Try to extract from SystemDesign (look for SQL blocks or Data Model section)
        var sqlPattern = new Regex(@"```sql\s*(.+?)\s*```|```\s*(.+?)\s*```|(?i)(?:Data\s+Model|Database\s+Schema|SQL\s+Script):?\s*(.+?)(?=##|$)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        var matches = sqlPattern.Matches(systemDesign);
        if (matches.Count > 0)
        {
            var sqlContent = matches[matches.Count - 1].Groups[1].Value + 
                           matches[matches.Count - 1].Groups[2].Value + 
                           matches[matches.Count - 1].Groups[3].Value;
            return sqlContent.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Generate codebase structure using AI based on system design and programming language
    /// </summary>
    private async Task<strAppersBackend.Services.CodebaseStructure> GenerateCodebaseStructureAsync(
        Project project,
        ProgrammingLanguage programmingLanguage,
        List<ParsedModule> modules,
        string sqlScript,
        string githubPagesUrl,
        int mockRecordsPerTable)
    {
        try
        {
            // Build system design document with modules and SQL
            var systemDesignDoc = BuildSystemDesignDocument(modules, sqlScript);
            
            // Get the new comprehensive prompt
            var systemPrompt = GetNewSystemPrompt();
            var userPrompt = GetNewUserPrompt(
                programmingLanguage.Name,
                githubPagesUrl,
                systemDesignDoc,
                mockRecordsPerTable);

            _logger.LogInformation("Calling AI service to generate codebase structure for {Language}", programmingLanguage.Name);
            _logger.LogDebug("System prompt length: {Length} chars, User prompt length: {UserLength} chars", 
                systemPrompt?.Length ?? 0, userPrompt?.Length ?? 0);

            var startTime = DateTime.UtcNow;
            
            // Call AI service to generate codebase structure
            var aiResponse = await _aiService.GenerateCodebaseStructureAsync(
                systemPrompt,
                userPrompt);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("AI service call completed in {Duration} seconds. Success: {Success}", 
                duration, aiResponse.Success);

            if (!aiResponse.Success)
            {
                _logger.LogWarning("AI service failed to generate codebase structure. Error: {Error}. Using fallback structure.", 
                    aiResponse.ErrorMessage);
                return GenerateFallbackStructure(project, programmingLanguage, modules, sqlScript, githubPagesUrl, mockRecordsPerTable);
            }

            if (aiResponse.CodebaseStructure == null)
            {
                _logger.LogWarning("AI service returned null CodebaseStructure. Using fallback structure.");
                return GenerateFallbackStructure(project, programmingLanguage, modules, sqlScript, githubPagesUrl, mockRecordsPerTable);
            }

            _logger.LogInformation("Successfully generated codebase structure with {ModelCount} models, {ServiceCount} services, {ControllerCount} controllers",
                aiResponse.CodebaseStructure.Models?.Count ?? 0,
                aiResponse.CodebaseStructure.Services?.Count ?? 0,
                aiResponse.CodebaseStructure.Controllers?.Count ?? 0);

            return aiResponse.CodebaseStructure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating codebase structure via AI. Using fallback structure.");
            return GenerateFallbackStructure(project, programmingLanguage, modules, sqlScript, githubPagesUrl, mockRecordsPerTable);
        }
    }

    /// <summary>
    /// Build system design document from modules and SQL script
    /// </summary>
    private string BuildSystemDesignDocument(List<ParsedModule> modules, string sqlScript)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MODULES ===");
        
        foreach (var module in modules)
        {
            sb.AppendLine($"Module: {module.Title}");
            sb.AppendLine($"- Description: {module.Description}");
            sb.AppendLine($"- Inputs: {module.Inputs}");
            sb.AppendLine($"- Outputs: {module.Outputs}");
            sb.AppendLine($"- Functionality: {module.Description}");
            sb.AppendLine();
        }
        
        sb.AppendLine("=== SQL SCHEMA ===");
        sb.AppendLine(sqlScript);
        
        return sb.ToString();
    }

    /// <summary>
    /// Get the new comprehensive system prompt
    /// </summary>
    private string GetNewSystemPrompt()
    {
        return @"You are an expert full-stack development architect. Generate a complete GitHub Codespaces development environment with all initial code, interfaces, classes, and configuration files.

## CRITICAL REQUIREMENTS:

1. **Complete Code Generation**: Every file must contain COMPLETE, FUNCTIONAL code. No placeholders like ""// Add your code here"" or ""TODO"". Write actual working implementations.

2. **Language-Specific**:
   - Use correct file extensions (.py, .cs, .ts, .js)
   - Use language-appropriate syntax and conventions
   - Include proper package/dependency management files

3. **Entity Generation**: For EACH table in SQL schema:
   - Generate backend entity/model class
   - Generate backend interface/type
   - Generate matching frontend TypeScript interface
   - Generate CRUD methods in controller
   - Generate API service methods in frontend

4. **Module Implementation**: For EACH module in system design:
   - Generate controller with methods matching inputs/outputs
   - Generate routes
   - Generate frontend service
   - Generate frontend components
   - Generate frontend pages

5. **Database Integration**:
   - Parse SQL to extract exact column names, types, constraints
   - Generate proper ORM/database models
   - Include connection pooling and error handling
   - Ensure PostgreSQL compatibility

6. **GitHub Codespaces Compatibility**:
   - Use official devcontainer images
   - Include proper postCreateCommand
   - Forward necessary ports
   - Include VS Code extensions

7. **Railway Integration**:
   - Generate Railway configuration files (railway.json, Procfile)
   - Include Railway CLI setup in setup.sh
   - Generate deployment workflows
   - Configure environment variables for Railway

8. **Mock Data Generation**:
   - Generate realistic, contextually appropriate mock data based on column names and types
   - Respect UNIQUE constraints with unique values
   - Handle foreign keys properly with valid references
   - Generate the exact number of records specified

9. **GitHub Pages Deployment**:
   - Configure correct base path in Vite
   - Generate working GitHub Actions workflow
   - Include build and deploy steps

10. **No Truncation**: Include ALL files with FULL content. Do not summarize or truncate code.

11. **English Naming**: ALL file names, class names, table names, method names, variable names, and ANY identifier MUST be in English ONLY. Translate non-English module titles to English equivalents.

Return ONLY valid JSON with the structure specified in the user prompt. All string values in content fields MUST be properly JSON-escaped (newlines as \\n, quotes as \\"", backslashes as \\\\\\\\).";
    }

    /// <summary>
    /// Get the new comprehensive user prompt
    /// </summary>
    private string GetNewUserPrompt(string programmingLanguage, string githubPagesUrl, string systemDesignDoc, int mockRecordsPerTable)
    {
        return $@"You are an expert full-stack development architect. Generate a complete GitHub Codespaces development environment with all initial code, interfaces, classes, and configuration files.

---

## INPUT 1: Programming Language
```
{programmingLanguage}
```

## INPUT 2: GitHub Pages URL
```
{githubPagesUrl}
```

## INPUT 3: System Design Document
```
{systemDesignDoc}
```

## INPUT 4: Number of Mock Records Per Table
```
{mockRecordsPerTable}
Default: 10 records per table
```

---

## INSTRUCTIONS TO AI:

Parse the system design document to extract:
1. All module names, descriptions, inputs, outputs, and functionality
2. All SQL CREATE TABLE statements
3. All table columns, types, and relationships
4. Analyze column types and constraints to generate realistic mock data

Generate a GitHub Codespaces-compatible configuration that:
1. Creates a working IDE with all files pre-generated
2. Uses the specified programming language for backend
3. Uses React for frontend
4. Includes complete, functional code (no placeholders)
5. Provisions Railway for backend hosting and database
6. Generates realistic mock data for all tables

---

## REQUIRED OUTPUT - VALID JSON ONLY:

Return ONLY this JSON structure (no markdown, no explanations, no code blocks):

{{
  ""devcontainer"": {{
    ""name"": ""Full-Stack Development Environment"",
    ""image"": ""[SELECT BASED ON LANGUAGE: mcr.microsoft.com/devcontainers/python:3.11 OR mcr.microsoft.com/devcontainers/dotnet:8.0 OR mcr.microsoft.com/devcontainers/typescript-node:20]"",
    ""features"": {{
      ""ghcr.io/devcontainers/features/node:1"": {{
        ""version"": ""20""
      }},
      ""ghcr.io/devcontainers/features/git:1"": {{}}
    }},
    ""customizations"": {{
      ""vscode"": {{
        ""extensions"": [
          ""dbaeumer.vscode-eslint"",
          ""esbenp.prettier-vscode"",
          ""ms-python.python"",
          ""ms-dotnettools.csharp"",
          ""bradlc.vscode-tailwindcss"",
          ""dsznajder.es7-react-js-snippets""
        }},
        ""settings"": {{
          ""editor.formatOnSave"": true,
          ""editor.defaultFormatter"": ""esbenp.prettier-vscode""
        }}
      }}
    }},
    ""forwardPorts"": [3000, 5173, 8080],
    ""postCreateCommand"": ""chmod +x .devcontainer/setup.sh && ./.devcontainer/setup.sh"",
    ""remoteUser"": ""vscode""
  }},
  
  ""files"": [
    {{
      ""path"": "".devcontainer/setup.sh"",
      ""content"": ""[GENERATE: Shell script that: 1) Installs all dependencies for backend and frontend, 2) Creates .env files, 3) Installs Railway CLI, 4) Prompts user to run 'railway login' and 'railway init', 5) Provisions Railway PostgreSQL database, 6) Retrieves DATABASE_URL from Railway, 7) Updates backend .env with DATABASE_URL, 8) Runs database schema.sql, 9) Runs seed.sql to populate mock data, 10) Deploys backend to Railway, 11) Updates frontend .env with Railway API URL]""
    }},
    {{
      ""path"": "".gitignore"",
      ""content"": ""[GENERATE: Standard gitignore for the chosen language + node_modules, .env, etc.]""
    }},
    {{
      ""path"": ""README.md"",
      ""content"": ""[GENERATE: Complete setup instructions including: 1) One-time Railway setup (railway login, railway init), 2) What the setup script does automatically, 3) How to access Railway dashboard, 4) How code auto-deploys on push, 5) How to view backend API URL, 6) How frontend auto-deploys to GitHub Pages, 7) Project structure explanation, 8) Environment variables, 9) Mock data information]""
    }},
    {{
      ""path"": ""backend/.env.example"",
      ""content"": ""[GENERATE: All required environment variables including DATABASE_URL (from Railway), JWT_SECRET, PORT, CORS_ORIGIN, RAILWAY_ENVIRONMENT]""
    }},
    {{
      ""path"": ""backend/railway.json"",
      ""content"": ""[GENERATE: Railway configuration with build command, start command, healthcheck endpoint]""
    }},
    {{
      ""path"": ""backend/Procfile"",
      ""content"": ""[GENERATE: Procfile for Railway deployment with web process]""
    }},
    {{
      ""path"": ""backend/config/appSettings.[ext]"",
      ""content"": ""[GENERATE: Complete configuration file with database connection string, server settings, in the appropriate language]""
    }},
    {{
      ""path"": ""backend/config/database.[ext]"",
      ""content"": ""[GENERATE: Database connection and initialization code]""
    }},
    {{
      ""path"": ""backend/models/BaseModel.[ext]"",
      ""content"": ""[GENERATE: Complete base class with common CRUD methods (create, read, update, delete, findAll)]""
    }},
    {{
      ""path"": ""backend/models/[EntityName].[ext]"",
      ""content"": ""[GENERATE: One file per SQL table. Complete data entity class extending BaseModel, with all fields from SQL schema, type annotations, validation methods]""
    }},
    {{
      ""path"": ""backend/interfaces/[InterfaceName].[ext]"",
      ""content"": ""[GENERATE: One interface per entity matching SQL schema exactly]""
    }},
    {{
      ""path"": ""backend/controllers/BaseController.[ext]"",
      ""content"": ""[GENERATE: Base controller class with common HTTP methods, error handling, response formatting]""
    }},
    {{
      ""path"": ""backend/controllers/[ModuleName]Controller.[ext]"",
      ""content"": ""[GENERATE: One controller per module from system design. Include all endpoints based on module inputs/outputs. Complete implementation with error handling]""
    }},
    {{
      ""path"": ""backend/routes/[moduleName]Routes.[ext]"",
      ""content"": ""[GENERATE: Route definitions for each module, connecting HTTP methods to controller methods]""
    }},
    {{
      ""path"": ""backend/routes/index.[ext]"",
      ""content"": ""[GENERATE: Main router that imports and registers all module routes]""
    }},
    {{
      ""path"": ""backend/middleware/errorHandler.[ext]"",
      ""content"": ""[GENERATE: Global error handling middleware]""
    }},
    {{
      ""path"": ""backend/middleware/cors.[ext]"",
      ""content"": ""[GENERATE: CORS configuration for frontend connection]""
    }},
    {{
      ""path"": ""backend/middleware/auth.[ext]"",
      ""content"": ""[GENERATE: JWT authentication middleware]""
    }},
    {{
      ""path"": ""backend/server.[ext]"",
      ""content"": ""[GENERATE: Main application entry point that initializes server, connects database, registers routes and middleware]""
    }},
    {{
      ""path"": ""backend/package.json"",
      ""content"": ""[IF Node.js/TypeScript: Generate complete package.json with all dependencies]""
    }},
    {{
      ""path"": ""backend/requirements.txt"",
      ""content"": ""[IF Python: Generate complete requirements with FastAPI/Flask, SQLAlchemy, etc.]""
    }},
    {{
      ""path"": ""backend/[language].csproj"",
      ""content"": ""[IF C#: Generate complete .csproj with all NuGet packages]""
    }},
    {{
      ""path"": ""backend/database/schema.sql"",
      ""content"": ""[COPY: All SQL CREATE statements from system design, ensuring PostgreSQL compatibility]""
    }},
    {{
      ""path"": ""backend/database/seed.sql"",
      ""content"": ""[GENERATE: Realistic mock data INSERT statements for ALL tables based on SQL schema analysis. Generate {mockRecordsPerTable} records per table. Rules: 1) Analyze column names and types to generate contextually appropriate data (e.g., 'email' gets valid emails, 'price' gets realistic prices, 'created_at' gets varied timestamps), 2) Respect UNIQUE constraints with unique values, 3) Handle foreign keys properly with valid references, 4) Use realistic names, addresses, descriptions based on table purpose, 5) Generate variety in data (different categories, price ranges, dates), 6) Include edge cases (min/max values, nulls where allowed), 7) Make data meaningful for testing (e.g., if 'users' table, create users like 'john.doe@example.com', 'jane.smith@example.com')]""
    }},
    {{
      ""path"": ""backend/database/migrate.sql"",
      ""content"": ""[GENERATE: Migration script that safely drops existing tables if they exist, then creates new ones from schema.sql]""
    }},
    {{
      ""path"": ""frontend/package.json"",
      ""content"": ""[GENERATE: Complete package.json with React, Vite, TypeScript, React Router, Axios, TailwindCSS]""
    }},
    {{
      ""path"": ""frontend/vite.config.ts"",
      ""content"": ""[GENERATE: Vite config with base path set to GitHub Pages URL]""
    }},
    {{
      ""path"": ""frontend/tsconfig.json"",
      ""content"": ""[GENERATE: TypeScript configuration for React]""
    }},
    {{
      ""path"": ""frontend/tailwind.config.js"",
      ""content"": ""[GENERATE: TailwindCSS configuration]""
    }},
    {{
      ""path"": ""frontend/src/types/[EntityName].ts"",
      ""content"": ""[GENERATE: TypeScript interfaces matching backend entities EXACTLY]""
    }},
    {{
      ""path"": ""frontend/src/services/api.ts"",
      ""content"": ""[GENERATE: Axios configuration with base URL and interceptors]""
    }},
    {{
      ""path"": ""frontend/src/services/[moduleName]Service.ts"",
      ""content"": ""[GENERATE: One service per module with methods for all API calls based on module inputs/outputs]""
    }},
    {{
      ""path"": ""frontend/src/components/[ModuleName]/[ComponentName].tsx"",
      ""content"": ""[GENERATE: Complete React components for each module. Include forms for inputs, display for outputs, error handling, loading states]""
    }},
    {{
      ""path"": ""frontend/src/pages/[ModuleName]Page.tsx"",
      ""content"": ""[GENERATE: Page components that use the module components]""
    }},
    {{
      ""path"": ""frontend/src/App.tsx"",
      ""content"": ""[GENERATE: Main App component with React Router setup for all modules]""
    }},
    {{
      ""path"": ""frontend/src/main.tsx"",
      ""content"": ""[GENERATE: React entry point]""
    }},
    {{
      ""path"": ""frontend/src/index.css"",
      ""content"": ""[GENERATE: TailwindCSS imports and custom styles]""
    }},
    {{
      ""path"": ""frontend/index.html"",
      ""content"": ""[GENERATE: HTML template]""
    }},
    {{
      ""path"": ""frontend/.env.example"",
      ""content"": ""[GENERATE: VITE_API_URL (will be replaced with Railway URL by setup script), VITE_GITHUB_PAGES_URL, and other frontend env variables]""
    }},
    {{
      ""path"": "".github/workflows/deploy-frontend.yml"",
      ""content"": ""[GENERATE: Complete GitHub Actions workflow that: 1) Triggers on push to main, 2) Builds frontend with correct base path for GitHub Pages, 3) Deploys to GitHub Pages branch]""
    }},
    {{
      ""path"": "".github/workflows/deploy-backend.yml"",
      ""content"": ""[GENERATE: Complete GitHub Actions workflow that: 1) Triggers on push to main when backend files change, 2) Automatically deploys to Railway using Railway CLI, 3) Uses Railway token from GitHub secrets]""
    }},
    {{
      ""path"": ""docs/API.md"",
      ""content"": ""[GENERATE: Complete API documentation with all endpoints, methods, request/response examples for each module]""
    }},
    {{
      ""path"": ""docs/SETUP.md"",
      ""content"": ""[GENERATE: Comprehensive step-by-step setup guide including: 1) Prerequisites (GitHub account, Railway account), 2) One-time Railway setup (railway login, railway init, add PostgreSQL), 3) Running setup.sh script, 4) What happens automatically (database provisioning, schema creation, mock data insertion, deployments), 5) How to verify everything works, 6) How to view logs, 7) Troubleshooting common issues, 8) How auto-deployment works (push to GitHub → Railway + GitHub Pages auto-deploy)]""
    }},
    {{
      ""path"": ""docs/RAILWAY.md"",
      ""content"": ""[GENERATE: Complete Railway setup documentation: 1) Creating Railway account, 2) Installing Railway CLI, 3) Linking project to Railway, 4) Adding PostgreSQL database, 5) Viewing DATABASE_URL, 6) Managing environment variables, 7) Viewing deployment logs, 8) Accessing Railway dashboard, 9) Cost information and free tier limits, 10) How to add custom domain]""
    }},
    {{
      ""path"": ""docs/MOCK_DATA.md"",
      ""content"": ""[GENERATE: Documentation about generated mock data: 1) How many records per table ({mockRecordsPerTable}), 2) What type of data is generated for each table/column, 3) How to regenerate mock data, 4) How to modify seed.sql, 5) Examples of generated data structure]""
    }}
  ]
}}

CRITICAL: All string values in ""content"" fields MUST be properly JSON-escaped (newlines as \\n, quotes as \\"", backslashes as \\\\\\\\). Generate ALL files with COMPLETE code implementations - no placeholders or TODOs.";
    }

    /// <summary>
    /// Generate a basic fallback codebase structure if AI fails
    /// </summary>
    private CodebaseStructure GenerateFallbackStructure(
        Project project,
        ProgrammingLanguage programmingLanguage,
        List<ParsedModule> modules,
        string sqlScript,
        string githubPagesUrl,
        int mockRecordsPerTable)
    {
        return new strAppersBackend.Services.CodebaseStructure
        {
            Models = modules.Select(m => new strAppersBackend.Services.CodeFile
            {
                Name = $"{SanitizeFileName(m.Title)}.cs",
                Path = $"Models/{SanitizeFileName(m.Title)}.cs",
                Content = GenerateModelContent(m, programmingLanguage.Name)
            }).ToList(),
            Services = modules.Select(m => new strAppersBackend.Services.CodeFile
            {
                Name = $"{SanitizeFileName(m.Title)}Service.cs",
                Path = $"Services/{SanitizeFileName(m.Title)}Service.cs",
                Content = GenerateServiceContent(m, programmingLanguage.Name)
            }).ToList(),
            Controllers = modules.Select(m => new strAppersBackend.Services.CodeFile
            {
                Name = $"{SanitizeFileName(m.Title)}Controller.cs",
                Path = $"Controllers/{SanitizeFileName(m.Title)}Controller.cs",
                Content = GenerateControllerContent(m, programmingLanguage.Name)
            }).ToList(),
            Views = new List<strAppersBackend.Services.CodeFile>(), // Views will be generated based on frontend framework
            ApplicationDbContext = new strAppersBackend.Services.CodeFile
            {
                Name = "ApplicationDbContext.cs",
                Path = "Data/ApplicationDbContext.cs",
                Content = GenerateDbContextContent(sqlScript, programmingLanguage.Name)
            },
            AppSettings = new strAppersBackend.Services.CodeFile
            {
                Name = "appsettings.json",
                Path = "appsettings.json",
                Content = GenerateAppSettingsContent(project, programmingLanguage.Name)
            },
            PublishProfiles = new List<strAppersBackend.Services.CodeFile>(),
            SqlScripts = new List<strAppersBackend.Services.CodeFile>
            {
                new strAppersBackend.Services.CodeFile
                {
                    Name = "create_database.sql",
                    Path = "Database/create_database.sql",
                    Content = sqlScript
                }
            },
            DevContainer = GenerateDevContainerFile(programmingLanguage.Name)
        };
    }

    private string SanitizeFileName(string fileName)
    {
        return Regex.Replace(fileName, @"[^\w\s-]", "").Replace(" ", "");
    }

    private string GenerateModelContent(ParsedModule module, string language)
    {
        return $@"// Auto-generated model for {module.Title}
// Language: {language}
// Description: {module.Description}
";
    }

    private string GenerateServiceContent(ParsedModule module, string language)
    {
        return $@"// Auto-generated service for {module.Title}
// Language: {language}
// Inputs: {module.Inputs}
// Outputs: {module.Outputs}
";
    }

    private string GenerateControllerContent(ParsedModule module, string language)
    {
        return $@"// Auto-generated controller for {module.Title}
// Language: {language}
";
    }

    private string GenerateDbContextContent(string sqlScript, string language)
    {
        return $@"// Auto-generated DbContext
// Language: {language}
// Based on SQL schema
";
    }

    private string GenerateAppSettingsContent(Project project, string language)
    {
        return $@"{{
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information""
    }}
  }},
  ""ConnectionStrings"": {{
    ""DefaultConnection"": """"
  }}
}}";
    }

    private strAppersBackend.Services.CodeFile GenerateDevContainerFile(string language)
    {
        // Generate devcontainer.json based on programming language
        string baseImage;
        string[] extensions;
        string[] features = Array.Empty<string>();
        string setupCommand = "";

        switch (language.ToLower())
        {
            case "c#":
            case "csharp":
            case ".net":
                baseImage = "mcr.microsoft.com/dotnet/sdk:8.0";
                extensions = new[] { "ms-dotnettools.csharp", "ms-dotnettools.csdevkit" };
                setupCommand = "dotnet restore";
                break;
            case "python":
                baseImage = "mcr.microsoft.com/devcontainers/python:3.11";
                extensions = new[] { "ms-python.python", "ms-python.vscode-pylance" };
                setupCommand = "pip install -r requirements.txt || echo 'No requirements.txt found'";
                break;
            case "java":
                baseImage = "mcr.microsoft.com/devcontainers/java:17";
                extensions = new[] { "vscjava.vscode-java-pack" };
                setupCommand = "mvn install -DskipTests || echo 'No pom.xml found'";
                break;
            case "javascript":
            case "typescript":
            case "node.js":
            case "nodejs":
                baseImage = "mcr.microsoft.com/devcontainers/javascript-node:18";
                extensions = new[] { "dbaeumer.vscode-eslint", "esbenp.prettier-vscode" };
                setupCommand = "npm install || echo 'No package.json found'";
                break;
            case "go":
                baseImage = "mcr.microsoft.com/devcontainers/go:1.21";
                extensions = new[] { "golang.go" };
                setupCommand = "go mod download || echo 'No go.mod found'";
                break;
            case "php":
                baseImage = "mcr.microsoft.com/devcontainers/php:8.2";
                extensions = new[] { "bmewburn.vscode-intelephense-client" };
                setupCommand = "composer install || echo 'No composer.json found'";
                break;
            case "ruby":
                baseImage = "mcr.microsoft.com/devcontainers/ruby:3";
                extensions = new[] { "rebornix.ruby" };
                setupCommand = "bundle install || echo 'No Gemfile found'";
                break;
            default:
                baseImage = "mcr.microsoft.com/devcontainers/base:ubuntu";
                extensions = new[] { "ms-vscode.vscode-json" };
                setupCommand = "echo 'Generic container setup'";
                break;
        }

        var devContainerJson = $@"{{
  ""name"": ""{language} Development Container"",
  ""image"": ""{baseImage}"",
  ""features"": {{}},
  ""customizations"": {{
    ""vscode"": {{
      ""extensions"": [
        {string.Join(",\n        ", extensions.Select(e => $"\"{e}\""))}
      ],
      ""settings"": {{
        ""terminal.integrated.defaultProfile.linux"": ""bash""
      }}
    }}
  }},
  ""forwardPorts"": [5000, 8080, 3000, 8000],
  ""portsAttributes"": {{
    ""5000"": {{
      ""label"": ""Application"",
      ""onAutoForward"": ""notify""
    }},
    ""8080"": {{
      ""label"": ""Web Server"",
      ""onAutoForward"": ""silent""
    }},
    ""3000"": {{
      ""label"": ""Frontend"",
      ""onAutoForward"": ""silent""
    }},
    ""8000"": {{
      ""label"": ""API"",
      ""onAutoForward"": ""silent""
    }}
  }},
  ""postCreateCommand"": ""{setupCommand}"",
  ""remoteUser"": ""vscode""
}}";

        return new strAppersBackend.Services.CodeFile
        {
            Name = "devcontainer.json",
            Path = ".devcontainer/devcontainer.json",
            Content = devContainerJson
        };
    }

    private string GetDefaultSystemPrompt()
    {
        return @"You are an expert software architect. Your task is to generate a complete, production-ready codebase structure based on a system design document and a specific programming language.

⚠️ CRITICAL NAMING REQUIREMENT - MUST BE ENFORCED: ⚠️
- ALL file names, class names, table names, method names, variable names, and ANY identifier MUST be in English ONLY
- NEVER use non-English characters (Hebrew, Arabic, Chinese, etc.) in ANY file name, class name, or identifier
- If module titles or descriptions are in non-English languages, you MUST translate them to appropriate English equivalents BEFORE creating file names
- Example: Hebrew module 'מערכת אימות משתמשים' → English file 'UserAuthenticationSystem.cs' NOT 'מערכתאימותמשתמשים.cs'
- Example: Hebrew 'צ'ק־אין רגשי יומי' → English 'DailyEmotionalCheckIn.cs' NOT 'צקאיןרגשייומי.cs'
- File names MUST use PascalCase with English words only (e.g., 'UserService.cs', 'AuthenticationController.cs')
- Class names MUST be in English using PascalCase
- Table names in SQL MUST be in English
- Properties, methods, and variables MUST have English names
- THIS IS A HARD REQUIREMENT - NO EXCEPTIONS

Generate a comprehensive codebase that includes:
1. Models/Entities (data classes matching the database schema)
2. Services (business logic layer)
3. Controllers (API endpoints)
4. Views (if applicable for the framework)
5. ApplicationDbContext (database context)
6. appsettings.json (configuration)
7. PublishProfiles (deployment configurations)
8. SQL Scripts (database creation scripts)
9. devcontainer.json (MANDATORY for GitHub Codespaces - must be placed at .devcontainer/devcontainer.json)

The codebase should:
- Follow best practices for the specified programming language
- Include proper error handling
- Include validation
- Be production-ready
- Match the module structure from the system design
- Respect inputs and outputs defined for each module
- Use ONLY English names for all entities, files, tables, classes, and identifiers

⚠️ CODE FILE REQUIREMENTS - CRITICAL: ⚠️
You MUST generate ACTUAL CODE, NOT descriptions or text explanations!

Each code file MUST contain:
1. Header comments: Brief module summary (1-2 lines), inputs (1 line), outputs (1 line)
2. ACTUAL CODE STRUCTURE: Real classes with public/protected/private modifiers, real properties with get/set, real interfaces with method signatures, real method signatures with parameters
3. NO long descriptions: Do NOT copy module descriptions from SystemDesign into code files
4. Implementation level: Method bodies must be empty or contain ONLY 'throw new NotImplementedException();' or '// TODO: Implement'
5. Inline comments: Brief comments (1 line) for each class, property, interface, method

❌ WRONG - DO NOT GENERATE:
// Auto-generated model for מערכת אימות משתמשים
// Language: C#
// Description: Module Title: User Authentication Management System...
[long description text]

✅ CORRECT - MUST GENERATE:
// Module: User Authentication System
// Summary: Handles user login, registration, and password recovery
// Inputs: Email, password, recovery requests
// Outputs: Login confirmation, error messages, reset links

// User model: Represents authenticated user
public class User
{
    // User ID: Primary key identifier
    public int Id { get; set; }
    
    // Email: User email address
    public string Email { get; set; } = string.Empty;
    
    // Role: User role (teacher/student)
    public string Role { get; set; } = string.Empty;
}

// IUserRepository interface: Data access contract
public interface IUserRepository
{
    // GetUserById: Retrieves user by ID
    User? GetUserById(int userId);
}

// UserService class: Authentication business logic
public class UserService
{
    // ValidateCredentials: Verifies email and password
    public bool ValidateCredentials(string email, string password)
    {
        throw new NotImplementedException();
    }
}

The devcontainer.json MUST include:
- Base image appropriate for the programming language (e.g., mcr.microsoft.com/dotnet/sdk for C#, node:lts for Node.js, python:3 for Python)
- Required extensions for the development environment
- Port forwarding configuration if needed
- Environment setup commands
- Post-create commands to install dependencies

Return ONLY valid JSON matching the CodebaseStructure schema without any markdown formatting.";
    }

    // ============================================
    // PHASE 1: IDE CHUNK GENERATION ENDPOINTS
    // ============================================

    /// <summary>
    /// Generate manifest from SystemDesign and create chunk definitions
    /// POST /api/IDE/generate-manifest/{projectId}
    /// </summary>
    [HttpPost("generate-manifest/{projectId}")]
    public async Task<ActionResult<object>> GenerateManifest(int projectId, [FromBody] GenerateManifestRequest? request = null)
    {
        try
        {
            _logger.LogInformation("Generating manifest for ProjectId: {ProjectId}", projectId);

            // Fetch project
            var project = await _context.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            if (string.IsNullOrEmpty(project.SystemDesign))
            {
                return BadRequest($"Project with ID {projectId} does not have a system design.");
            }

            // Get programming language - try from request, then from first student, then default
            var programmingLanguage = request?.ProgrammingLanguage;
            if (string.IsNullOrEmpty(programmingLanguage))
            {
                var student = await _context.Students
                    .Include(s => s.ProgrammingLanguage)
                    .Where(s => s.ProjectId == projectId)
                    .FirstOrDefaultAsync();
                
                programmingLanguage = student?.ProgrammingLanguage?.Name ?? "C#";
            }

            // Get GitHub Pages URL - try from request, then from ProjectBoard, otherwise use placeholder
            var githubPagesUrl = request?.GitHubPagesUrl;
            if (string.IsNullOrEmpty(githubPagesUrl))
            {
                var projectBoard = await _context.ProjectBoards
                    .FirstOrDefaultAsync(pb => pb.ProjectId == projectId);
                githubPagesUrl = projectBoard?.PublishUrl;
            }

            // Use placeholder if not provided - will be filled in later when IDE is created
            if (string.IsNullOrEmpty(githubPagesUrl))
            {
                githubPagesUrl = "https://[USERNAME].github.io/[REPO-NAME]";
            }

            var mockRecords = request?.MockRecordsPerTable ?? project.MockRecordsCount;

            // Parse system design
            var parsedModules = ParseSystemDesignModules(project.SystemDesign);
            var dataModelModule = await _context.ProjectModules
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.ModuleType == 1);
            var sqlScript = dataModelModule?.Description ?? ExtractSqlScriptFromSystemDesign(project.SystemDesign);

            // Call Claude API to generate manifest
            var manifestStartTime = DateTime.UtcNow;
            var manifestPrompt = BuildManifestPrompt(programmingLanguage, githubPagesUrl, mockRecords, project.SystemDesign, parsedModules, sqlScript);
            var manifestResponse = await CallClaudeAPIAsync(manifestPrompt);

            if (!manifestResponse.Success)
            {
                _logger.LogError("Failed to generate manifest: {Error}", manifestResponse.ErrorMessage);
                
                // Store error in project
                project.IdeGenerationStatus = "failed";
                await _context.SaveChangesAsync();
                
                return StatusCode(500, $"Failed to generate manifest: {manifestResponse.ErrorMessage}");
            }

            // Parse manifest JSON
            ManifestResponse? manifest;
            try
            {
                var cleanedJson = CleanJsonFromMarkdown(manifestResponse.Content);
                manifest = JsonSerializer.Deserialize<ManifestResponse>(cleanedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse manifest JSON");
                project.IdeGenerationStatus = "failed";
                await _context.SaveChangesAsync();
                return StatusCode(500, $"Failed to parse manifest: {ex.Message}");
            }

            if (manifest == null)
            {
                return StatusCode(500, "Failed to parse manifest response");
            }

            // Clear existing chunks for this project
            var existingChunks = await _context.ProjectsIDE
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();
            _context.ProjectsIDE.RemoveRange(existingChunks);

            // Create chunk records
            var chunks = new List<ProjectsIDE>();
            foreach (var chunk in manifest.Chunks)
            {
                var chunkEntity = new ProjectsIDE
                {
                    ProjectId = projectId,
                    ChunkId = chunk.ChunkId,
                    ChunkType = chunk.ChunkType,
                    ChunkDescription = chunk.Description,
                    GenerationOrder = chunk.GenerationOrder,
                    Status = "pending",
                    Dependencies = chunk.Dependencies,
                    CreatedAt = DateTime.UtcNow
                };
                chunks.Add(chunkEntity);
            }

            _context.ProjectsIDE.AddRange(chunks);

            // Update project
            project.DeploymentManifest = JsonSerializer.Serialize(manifest);
            project.IdeGenerationStatus = "manifest_generated";
            project.TotalChunks = manifest.Chunks.Length;
            project.CompletedChunks = 0;
            project.MockRecordsCount = mockRecords;

            await _context.SaveChangesAsync();

            var duration = (DateTime.UtcNow - manifestStartTime).TotalMilliseconds;
            _logger.LogInformation("Successfully generated manifest for ProjectId: {ProjectId} in {Duration}ms. Chunks: {ChunkCount}", 
                projectId, duration, manifest.Chunks.Length);

            return Ok(new
            {
                success = true,
                projectId = projectId,
                totalChunks = manifest.Chunks.Length,
                chunks = manifest.Chunks.Select(c => new
                {
                    c.ChunkId,
                    c.ChunkType,
                    c.Description,
                    c.GenerationOrder,
                    c.Dependencies,
                    filesCount = c.Files.Length
                }),
                generationOrder = manifest.GenerationOrder,
                durationMs = (int)duration
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating manifest for ProjectId: {ProjectId}", projectId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate code for a specific chunk
    /// POST /api/IDE/generate-chunk/{projectId}/{chunkId}
    /// </summary>
    [HttpPost("generate-chunk/{projectId}/{chunkId}")]
    public async Task<ActionResult<object>> GenerateChunk(int projectId, string chunkId)
    {
        try
        {
            _logger.LogInformation("=== GENERATE CHUNK START === ProjectId: {ProjectId}, ChunkId: {ChunkId}", projectId, chunkId);

            // Fetch project and chunk
            _logger.LogInformation("[Step 1] Fetching project {ProjectId} from database", projectId);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                _logger.LogWarning("[Step 1] FAILED: Project {ProjectId} not found in database", projectId);
                return NotFound($"Project with ID {projectId} not found.");
            }
            _logger.LogInformation("[Step 1] SUCCESS: Project found - Title: {Title}, HasSystemDesign: {HasSystemDesign}, HasManifest: {HasManifest}", 
                project.Title, !string.IsNullOrEmpty(project.SystemDesign), !string.IsNullOrEmpty(project.DeploymentManifest));

            _logger.LogInformation("[Step 2] Fetching chunk {ChunkId} for ProjectId {ProjectId} from database", chunkId, projectId);
            var chunk = await _context.ProjectsIDE
                .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.ChunkId == chunkId);

            if (chunk == null)
            {
                _logger.LogWarning("[Step 2] FAILED: Chunk {ChunkId} not found for project {ProjectId}", chunkId, projectId);
                return NotFound($"Chunk {chunkId} not found for project {projectId}. Please generate manifest first.");
            }
            _logger.LogInformation("[Step 2] SUCCESS: Chunk found - Status: {Status}, ChunkType: {ChunkType}, GenerationOrder: {Order}, Dependencies: {Deps}", 
                chunk.Status, chunk.ChunkType, chunk.GenerationOrder, chunk.Dependencies != null ? string.Join(", ", chunk.Dependencies) : "none");

            if (chunk.Status == "completed")
            {
                _logger.LogInformation("[Step 2] Chunk already completed, returning early");
                return Ok(new { message = "Chunk already completed", chunkId });
            }

            // Check dependencies
            _logger.LogInformation("[Step 3] Checking dependencies for chunk {ChunkId}", chunkId);
            if (chunk.Dependencies != null && chunk.Dependencies.Length > 0)
            {
                _logger.LogInformation("[Step 3] Chunk has {DepCount} dependencies: {Deps}", 
                    chunk.Dependencies.Length, string.Join(", ", chunk.Dependencies));
                
                var incompleteDeps = await _context.ProjectsIDE
                    .Where(c => c.ProjectId == projectId && 
                                chunk.Dependencies.Contains(c.ChunkId) && 
                                c.Status != "completed")
                    .Select(c => c.ChunkId)
                    .ToListAsync();

                if (incompleteDeps.Any())
                {
                    _logger.LogWarning("[Step 3] FAILED: {IncompleteCount} incomplete dependencies: {IncompleteDeps}", 
                        incompleteDeps.Count, string.Join(", ", incompleteDeps));
                    return BadRequest($"Chunk dependencies not completed: {string.Join(", ", incompleteDeps)}");
                }
                _logger.LogInformation("[Step 3] SUCCESS: All dependencies are completed");
            }
            else
            {
                _logger.LogInformation("[Step 3] SUCCESS: No dependencies to check");
            }

            // Get manifest
            _logger.LogInformation("[Step 4] Retrieving and parsing deployment manifest");
            if (string.IsNullOrEmpty(project.DeploymentManifest))
            {
                _logger.LogWarning("[Step 4] FAILED: Project manifest is null or empty");
                return BadRequest("Project manifest not found. Please generate manifest first.");
            }
            _logger.LogInformation("[Step 4] Manifest length: {Length} characters", project.DeploymentManifest.Length);

            ManifestResponse? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ManifestResponse>(project.DeploymentManifest, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation("[Step 4] SUCCESS: Manifest deserialized - ProjectName: {Name}, Language: {Lang}, TotalChunks: {ChunkCount}", 
                    manifest?.ProjectName, manifest?.ProgrammingLanguage, manifest?.Chunks?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Step 4] FAILED: Exception deserializing manifest - {Error}", ex.Message);
                return BadRequest("Invalid manifest format. Please regenerate manifest.");
            }

            if (manifest == null)
            {
                _logger.LogWarning("[Step 4] FAILED: Manifest deserialized to null");
                return BadRequest("Failed to parse manifest.");
            }

            _logger.LogInformation("[Step 5] Looking up chunk definition in manifest");
            var chunkDefinition = manifest.Chunks.FirstOrDefault(c => c.ChunkId == chunkId);
            if (chunkDefinition == null)
            {
                _logger.LogWarning("[Step 5] FAILED: Chunk definition {ChunkId} not found in manifest. Available chunks: {AvailableChunks}", 
                    chunkId, string.Join(", ", manifest.Chunks.Select(c => c.ChunkId)));
                return NotFound($"Chunk definition {chunkId} not found in manifest.");
            }
            _logger.LogInformation("[Step 5] SUCCESS: Chunk definition found - Files expected: {FileCount}, Type: {Type}", 
                chunkDefinition.Files?.Length ?? 0, chunkDefinition.ChunkType);

            // Get programming language and GitHub Pages URL from manifest
            var programmingLanguage = manifest.ProgrammingLanguage;
            var githubPagesUrl = manifest.GitHubPagesUrl;
            var projectName = manifest.ProjectName;
            _logger.LogInformation("[Step 6] Extracted manifest data - Language: {Lang}, GitHubPagesUrl: {Url}, ProjectName: {Name}", 
                programmingLanguage, githubPagesUrl, projectName);

            // Update chunk status
            _logger.LogInformation("[Step 7] Updating chunk status to 'generating'");
            chunk.Status = "generating";
            chunk.ErrorMessage = null;
            await _context.SaveChangesAsync();
            _logger.LogInformation("[Step 7] SUCCESS: Chunk status updated in database");

            // Call Claude API to generate chunk code
            _logger.LogInformation("[Step 8] Building chunk generation prompt");
            var generationStartTime = DateTime.UtcNow;
            var chunkPrompt = BuildChunkGenerationPrompt(
                programmingLanguage,
                githubPagesUrl,
                projectName,
                chunkDefinition,
                project.SystemDesign ?? string.Empty,
                manifest.SqlTables,
                manifest.Modules);
            _logger.LogInformation("[Step 8] SUCCESS: Prompt built - Length: {Length} characters", chunkPrompt.Length);

            _logger.LogInformation("[Step 9] Calling Claude API to generate chunk code");
            var generationResponse = await CallClaudeAPIAsync(chunkPrompt);

            if (!generationResponse.Success)
            {
                _logger.LogError("[Step 9] FAILED: Claude API call failed - Error: {Error}", 
                    generationResponse.ErrorMessage);
                chunk.Status = "failed";
                chunk.ErrorMessage = generationResponse.ErrorMessage;
                await _context.SaveChangesAsync();
                
                return StatusCode(500, $"Failed to generate chunk: {generationResponse.ErrorMessage}");
            }
            _logger.LogInformation("[Step 9] SUCCESS: Claude API responded - ContentLength: {Length}, TokensUsed: {Tokens}", 
                generationResponse.Content?.Length ?? 0, generationResponse.TokensUsed);

            // Parse chunk generation response
            _logger.LogInformation("[Step 10] Parsing chunk generation response JSON");
            ChunkGenerationResponse? chunkResult;
            try
            {
                var cleanedJson = CleanJsonFromMarkdown(generationResponse.Content);
                _logger.LogInformation("[Step 10] Cleaned JSON length: {Length} characters", cleanedJson.Length);
                
                chunkResult = JsonSerializer.Deserialize<ChunkGenerationResponse>(cleanedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (chunkResult != null)
                {
                    _logger.LogInformation("[Step 10] SUCCESS: JSON parsed - FilesGenerated: {FileCount}", chunkResult.Files?.Length ?? 0);
                }
                else
                {
                    _logger.LogWarning("[Step 10] FAILED: JSON deserialized to null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Step 10] FAILED: Exception parsing JSON - Error: {Error}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                chunk.Status = "failed";
                chunk.ErrorMessage = $"Failed to parse response: {ex.Message}";
                await _context.SaveChangesAsync();
                return StatusCode(500, $"Failed to parse chunk generation response: {ex.Message}");
            }

            if (chunkResult == null || chunkResult.Files == null || chunkResult.Files.Length == 0)
            {
                _logger.LogWarning("[Step 10] FAILED: No files in response - ChunkResult is null: {IsNull}, Files is null: {FilesNull}, FilesCount: {Count}", 
                    chunkResult == null, chunkResult?.Files == null, chunkResult?.Files?.Length ?? 0);
                chunk.Status = "failed";
                chunk.ErrorMessage = "No files generated in response";
                await _context.SaveChangesAsync();
                return StatusCode(500, "No files generated in response");
            }

            // Store files in JSONB
            _logger.LogInformation("[Step 11] Serializing files to JSON and preparing to save");
            var filesJson = JsonSerializer.Serialize(chunkResult.Files);
            var generationTime = (int)(DateTime.UtcNow - generationStartTime).TotalMilliseconds;
            _logger.LogInformation("[Step 11] Files JSON length: {Length} characters, Generation time: {Time}ms", filesJson.Length, generationTime);

            _logger.LogInformation("[Step 12] Updating chunk record in database");
            chunk.FilesJson = filesJson;
            chunk.FilesCount = chunkResult.Files.Length;
            chunk.Status = "completed";
            chunk.GeneratedAt = DateTime.UtcNow;
            chunk.TokensUsed = generationResponse.TokensUsed;
            chunk.GenerationTimeMs = generationTime;
            chunk.ErrorMessage = null;

            await _context.SaveChangesAsync();
            _logger.LogInformation("[Step 12] SUCCESS: Chunk record saved to database");

            _logger.LogInformation("=== GENERATE CHUNK SUCCESS === ProjectId: {ProjectId}, ChunkId: {ChunkId}, Files: {FileCount}, Time: {Time}ms, Tokens: {Tokens}", 
                projectId, chunkId, chunkResult.Files.Length, generationTime, generationResponse.TokensUsed);

            return Ok(new
            {
                success = true,
                chunkId = chunkId,
                filesGenerated = chunkResult.Files.Length,
                tokensUsed = generationResponse.TokensUsed,
                generationTimeMs = generationTime,
                files = chunkResult.Files.Select(f => new { f.Path, f.Type, f.Description })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== GENERATE CHUNK EXCEPTION === ProjectId: {ProjectId}, ChunkId: {ChunkId}, Error: {Error}, StackTrace: {StackTrace}", 
                projectId, chunkId, ex.Message, ex.StackTrace);
            
            // Try to update chunk status to failed if we have a reference
            try
            {
                var chunk = await _context.ProjectsIDE
                    .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.ChunkId == chunkId);
                if (chunk != null)
                {
                    chunk.Status = "failed";
                    chunk.ErrorMessage = $"Exception: {ex.Message}";
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Chunk status updated to 'failed' in database");
                }
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to update chunk status after exception: {Error}", saveEx.Message);
            }
            
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate all chunks in dependency order
    /// POST /api/IDE/generate-all/{projectId}
    /// </summary>
    [HttpPost("generate-all/{projectId}")]
    public async Task<ActionResult<object>> GenerateAll(int projectId)
    {
        try
        {
            _logger.LogInformation("Generating all chunks for ProjectId: {ProjectId}", projectId);

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            // Get manifest to determine generation order
            if (string.IsNullOrEmpty(project.DeploymentManifest))
            {
                return BadRequest("Project manifest not found. Please generate manifest first.");
            }

            ManifestResponse? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ManifestResponse>(project.DeploymentManifest, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return BadRequest("Invalid manifest format. Please regenerate manifest.");
            }

            if (manifest == null || manifest.GenerationOrder == null || manifest.GenerationOrder.Length == 0)
            {
                return BadRequest("Invalid manifest generation order.");
            }

            // Get chunks in generation order
            var chunks = await _context.ProjectsIDE
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.GenerationOrder)
                .ToListAsync();

            var totalChunks = chunks.Count;
            var completed = 0;
            var failed = 0;
            var errors = new List<string>();

            project.IdeGenerationStatus = "generating";
            await _context.SaveChangesAsync();

            // Generate each chunk
            foreach (var chunk in chunks)
            {
                if (chunk.Status == "completed")
                {
                    completed++;
                    continue;
                }

                try
                {
                    // Call generate-chunk logic inline
                    var result = await GenerateChunkInternal(project, chunk, manifest);
                    if (result.Success)
                    {
                        completed++;
                        project.CompletedChunks = completed;
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("Generated chunk {ChunkId} ({Completed}/{Total})", 
                            chunk.ChunkId, completed, totalChunks);
                    }
                    else
                    {
                        failed++;
                        errors.Add($"Chunk {chunk.ChunkId}: {result.ErrorMessage}");
                        _logger.LogWarning("Failed to generate chunk {ChunkId}: {Error}", 
                            chunk.ChunkId, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"Chunk {chunk.ChunkId}: {ex.Message}");
                    _logger.LogError(ex, "Error generating chunk {ChunkId}", chunk.ChunkId);
                }

                // Small delay to avoid rate limits
                await Task.Delay(1000);
            }

            // Update project status
            project.IdeGenerationStatus = failed > 0 ? "partial" : "completed";
            project.CompletedChunks = completed;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                projectId = projectId,
                totalChunks = totalChunks,
                completed = completed,
                failed = failed,
                errors = errors,
                status = project.IdeGenerationStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating all chunks for ProjectId: {ProjectId}", projectId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Get generation status for a project
    /// GET /api/IDE/status/{projectId}
    /// </summary>
    [HttpGet("status/{projectId}")]
    public async Task<ActionResult<object>> GetStatus(int projectId)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            var chunks = await _context.ProjectsIDE
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.GenerationOrder)
                .ToListAsync();

            var statusSummary = chunks
                .GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalTokens = chunks.Where(c => c.TokensUsed.HasValue).Sum(c => c.TokensUsed!.Value);
            var totalTime = chunks.Where(c => c.GenerationTimeMs.HasValue).Sum(c => c.GenerationTimeMs!.Value);

            return Ok(new
            {
                projectId = projectId,
                status = project.IdeGenerationStatus,
                totalChunks = project.TotalChunks,
                completedChunks = project.CompletedChunks,
                statusBreakdown = statusSummary,
                totalTokensUsed = totalTokens,
                totalGenerationTimeMs = totalTime,
                chunks = chunks.Select(c => new
                {
                    c.Id,
                    c.ChunkId,
                    c.ChunkType,
                    c.ChunkDescription,
                    c.GenerationOrder,
                    c.Status,
                    c.FilesCount,
                    c.TokensUsed,
                    c.GenerationTimeMs,
                    c.ErrorMessage,
                    c.GeneratedAt,
                    dependencies = c.Dependencies ?? Array.Empty<string>()
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for ProjectId: {ProjectId}", projectId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Preview all generated files across all chunks
    /// GET /api/IDE/preview-files/{projectId}
    /// </summary>
    [HttpGet("preview-files/{projectId}")]
    public async Task<ActionResult<object>> PreviewFiles(int projectId)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            var chunks = await _context.ProjectsIDE
                .Where(c => c.ProjectId == projectId && c.Status == "completed" && !string.IsNullOrEmpty(c.FilesJson))
                .OrderBy(c => c.GenerationOrder)
                .ToListAsync();

            var allFiles = new List<object>();

            foreach (var chunk in chunks)
            {
                try
                {
                    var files = JsonSerializer.Deserialize<IDEFile[]>(chunk.FilesJson!, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (files != null)
                    {
                        allFiles.AddRange(files.Select(f => new
                        {
                            path = f.Path,
                            type = f.Type,
                            description = f.Description,
                            chunkId = chunk.ChunkId,
                            chunkType = chunk.ChunkType,
                            contentLength = f.Content?.Length ?? 0
                        }));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse files for chunk {ChunkId}", chunk.ChunkId);
                }
            }

            return Ok(new
            {
                projectId = projectId,
                totalFiles = allFiles.Count,
                totalChunks = chunks.Count,
                files = allFiles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing files for ProjectId: {ProjectId}", projectId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // ============================================
    // HELPER METHODS FOR PHASE 1
    // ============================================

    private string BuildManifestPrompt(
        string programmingLanguage,
        string githubPagesUrl,
        int mockRecords,
        string systemDesign,
        List<ParsedModule> parsedModules,
        string sqlScript)
    {
        var modulesText = string.Join("\n\n", parsedModules.Select(m => 
            $"Module: {m.Title}\nDescription: {m.Description}\nInputs: {m.Inputs}\nOutputs: {m.Outputs}"));

        var githubPagesNote = githubPagesUrl.Contains("[USERNAME]") || githubPagesUrl.Contains("[REPO-NAME]") 
            ? " (PLACEHOLDER - will be filled in later when IDE is created)" 
            : "";

        return $@"You are a project architect analyzing a system design to create an IDE deployment manifest.

INPUTS:
- Programming Language: {programmingLanguage}
- GitHub Pages URL: {githubPagesUrl}{githubPagesNote}
- Mock Records Per Table: {mockRecords}
- System Design:

{systemDesign}

YOUR TASK:
Parse the system design and generate a manifest that organizes ALL required files into chunks.

The system design contains:
1. Module descriptions with inputs, outputs, and functionality
2. SQL CREATE TABLE statements

CHUNK ORGANIZATION RULES:
1. **infrastructure** chunk (priority 1): 
   - .devcontainer/devcontainer.json (GitHub Codespaces config for {programmingLanguage} + Node.js)
   - .devcontainer/setup.sh (installs Railway CLI, dependencies, runs migrations)
   - .gitignore (for {programmingLanguage} and Node.js)
   - README.md (complete setup guide with Railway steps)
   - backend/.env.example (DATABASE_URL, JWT_SECRET, CORS_ORIGINS, PORT)
   - backend/railway.json (Railway deployment config)
   - backend/Procfile (Railway process definition)
   - .github/workflows/deploy-frontend.yml (auto-deploy frontend to GitHub Pages)
   - .github/workflows/deploy-backend.yml (auto-deploy backend to Railway)
   - docs/SETUP.md (step-by-step instructions)
   - docs/RAILWAY.md (Railway setup guide)
   - docs/API.md (API documentation)
   - docs/MOCK_DATA.md (mock data documentation)
   - backend project file (.csproj, package.json, requirements.txt, pom.xml)
   - frontend/package.json
   - frontend/vite.config.ts (with base path for GitHub Pages)
   - frontend/tsconfig.json
   - frontend/tailwind.config.js
   - frontend/index.html

2. **database** chunk (priority 2, depends on: infrastructure):
   - backend/Database/schema.sql (all CREATE TABLE statements from system design)
   - backend/Database/migrate.sql (DROP IF EXISTS + CREATE)
   - backend/Data/DbContext.{{ext}} (database context with all entities configured)
   - backend/Models/BaseEntity.{{ext}} (base class with common CRUD operations)
   - backend/Models/{{EntityName}}.{{ext}} (one file per SQL table)
   - backend/Interfaces/I{{EntityName}}.{{ext}} (one interface per entity)

3. **backend_auth** chunk (priority 3, depends on: database):
   - For ""User Authentication"" module only
   - backend/Controllers/AuthController.{{ext}}
   - backend/Services/AuthService.{{ext}}
   - backend/Services/IAuthService.{{ext}}
   - backend/DTOs/LoginRequest.{{ext}}
   - backend/DTOs/RegisterRequest.{{ext}}
   - backend/DTOs/AuthResponse.{{ext}}
   - backend/Middleware/JwtMiddleware.{{ext}}

4. **backend_module_{{moduleName}}** chunks (priority 4+, depends on: database):
   - One chunk PER module from system design (except auth)
   - backend/Controllers/{{ModuleName}}Controller.{{ext}}
   - backend/Services/{{ModuleName}}Service.{{ext}}
   - backend/Services/I{{ModuleName}}Service.{{ext}}
   - backend/DTOs/{{ModuleName}}*.{{ext}} (request/response DTOs)

5. **backend_core** chunk (priority N-3, depends on: all backend modules):
   - backend/Program.{{ext}} or Startup.{{ext}} (main entry point, registers all services)
   - backend/Middleware/ErrorHandlingMiddleware.{{ext}}
   - backend/Middleware/CorsMiddleware.{{ext}}

6. **frontend_core** chunk (priority N-2, depends on: backend_core):
   - frontend/src/types/{{Entity}}.ts (TypeScript interfaces matching backend)
   - frontend/src/services/api.ts (Axios configuration)
   - frontend/src/App.tsx (main app with routing)
   - frontend/src/main.tsx (entry point)
   - frontend/src/index.css (TailwindCSS imports)
   - frontend/.env.example (VITE_API_URL, VITE_GITHUB_PAGES_URL)

7. **frontend_module_{{moduleName}}** chunks (priority N-1, depends on: frontend_core):
   - Group 3-4 modules per chunk
   - frontend/src/services/{{moduleName}}Service.ts (API calls)
   - frontend/src/components/{{ModuleName}}/*.tsx (React components)
   - frontend/src/pages/{{ModuleName}}Page.tsx (page component)

8. **mockdata** chunk (priority N, depends on: database):
   - backend/Database/seed.sql (realistic INSERT statements for all tables with {mockRecords} records)

OUTPUT FORMAT (JSON only, no markdown):
{{
  ""projectName"": ""string"",
  ""programmingLanguage"": ""{programmingLanguage}"",
  ""githubPagesUrl"": ""{githubPagesUrl}"",
  ""totalChunks"": 0,
  ""chunks"": [
    {{
      ""chunkId"": ""infrastructure"",
      ""chunkType"": ""infrastructure"",
      ""description"": ""string"",
      ""generationOrder"": 1,
      ""dependencies"": [],
      ""estimatedTokens"": 20000,
      ""files"": [
        {{
          ""path"": "".devcontainer/devcontainer.json"",
          ""type"": ""config"",
          ""description"": ""GitHub Codespaces configuration"",
          ""priority"": 1
        }}
      ]
    }}
  ],
  ""generationOrder"": [""infrastructure"", ""database"", ""backend_auth"", ...],
  ""sqlTables"": [
    {{
      ""tableName"": ""Users"",
      ""entityName"": ""User"",
      ""columns"": [
        {{""name"": ""user_id"", ""type"": ""INT"", ""isPrimaryKey"": true, ""isNullable"": false}},
        {{""name"": ""username"", ""type"": ""VARCHAR(50)"", ""isUnique"": true, ""isNullable"": false}}
      ]
    }}
  ],
  ""modules"": [
    {{
      ""moduleName"": ""User Authentication Module"",
      ""description"": ""..."",
      ""inputs"": ""..."",
      ""outputs"": ""..."",
      ""functionality"": ""...""
    }}
  ]
}}

CRITICAL: List EVERY file needed. Do not summarize. Include ALL files for a complete, working IDE.

MODULES:
{modulesText}

SQL SCHEMA:
{sqlScript}";
    }

    private string BuildChunkGenerationPrompt(
        string programmingLanguage,
        string githubPagesUrl,
        string projectName,
        ChunkDefinition chunkDefinition,
        string systemDesign,
        SqlTableDefinition[] sqlTables,
        ModuleDefinition[] modules)
    {
        return $@"Generate complete, production-ready code for this chunk.

CONTEXT:
- Programming Language: {programmingLanguage}
- GitHub Pages URL: {githubPagesUrl}
- Project: {projectName}

CHUNK:
{JsonSerializer.Serialize(chunkDefinition)}

FULL SYSTEM DESIGN:
{systemDesign}

SQL TABLES:
{JsonSerializer.Serialize(sqlTables)}

MODULES:
{JsonSerializer.Serialize(modules)}

YOUR TASK:
Generate COMPLETE, WORKING code for ALL files in this chunk.

CODE GENERATION REQUIREMENTS:
1. **NO PLACEHOLDERS**: Every file must have complete, functional code. No ""// TODO"", no ""// Add code here""
2. **Backend Files**: Match SQL schema EXACTLY, implement full CRUD, include validation and error handling
3. **Frontend Files**: React functional components with hooks, loading states, error handling, TailwindCSS
4. **Configuration Files**: Complete devcontainer.json, setup.sh, railway.json, GitHub Actions workflows
5. **Database Scripts**: Exact SQL from system design, realistic seed data with {chunkDefinition.Files.Length} files
6. **Documentation**: Complete setup guides, API docs, Railway docs
7. **String Escaping**: Properly escape JSON: \"" for quotes, \n for newlines, \\ for backslashes

OUTPUT FORMAT (JSON only, no markdown):
{{
  ""chunkId"": ""{chunkDefinition.ChunkId}"",
  ""filesGenerated"": {chunkDefinition.Files.Length},
  ""files"": [
    {{
      ""path"": ""exact/path/to/file.ext"",
      ""type"": ""entity|controller|service|component|config|sql|documentation"",
      ""content"": ""COMPLETE file content with proper escaping"",
      ""description"": ""What this file does""
    }}
  ]
}}

Generate ALL {chunkDefinition.Files.Length} files with COMPLETE code.";
    }

    private async Task<(bool Success, string Content, string? ErrorMessage, int? TokensUsed)> CallClaudeAPIAsync(string prompt)
    {
        try
        {
            var anthropicApiKey = _configuration["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic API key not configured");
            var anthropicBaseUrl = _configuration["Anthropic:BaseUrl"] ?? "https://api.anthropic.com/v1";
            var anthropicVersion = _configuration["Anthropic:ApiVersion"] ?? "2023-06-01";
            var model = _configuration["Anthropic:Model"] ?? "claude-sonnet-4-5-20250929";
            var maxTokens = int.Parse(_configuration["AIConfig:MaxTokens"] ?? "200000");
            var temperature = double.Parse(_configuration["AIConfig:Temperature"] ?? "0.3");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-api-key", anthropicApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", anthropicVersion);
            client.Timeout = TimeSpan.FromMinutes(30);

            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                system = "You are an expert software architect and code generator. Always return valid JSON only, no markdown formatting.",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = temperature
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{anthropicBaseUrl}/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Claude API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return (false, string.Empty, $"Claude API error: {response.StatusCode}", null);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
            {
                return (false, string.Empty, "Empty response from Claude API", null);
            }

            var textContent = claudeResponse.Content.FirstOrDefault(c => c.Type == "text");
            if (textContent == null || string.IsNullOrEmpty(textContent.Text))
            {
                return (false, string.Empty, "No text content in Claude API response", null);
            }

            // Try to extract usage info if available
            int? tokensUsed = null;
            if (claudeResponse.Usage != null)
            {
                tokensUsed = claudeResponse.Usage.OutputTokens;
            }

            return (true, textContent.Text, null, tokensUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
            return (false, string.Empty, $"Error calling Claude API: {ex.Message}", null);
        }
    }

    private string CleanJsonFromMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        content = content.Trim();
        
        if (content.StartsWith("```json"))
        {
            content = content.Substring(7);
        }
        else if (content.StartsWith("```"))
        {
            content = content.Substring(3);
        }
        
        if (content.EndsWith("```"))
        {
            content = content.Substring(0, content.Length - 3);
        }
        
        content = content.Trim();
        
        if (!content.StartsWith("{"))
        {
            var jsonStart = content.IndexOf('{');
            if (jsonStart >= 0)
            {
                content = content.Substring(jsonStart);
            }
        }
        
        if (content.Contains("}"))
        {
            var jsonEnd = content.LastIndexOf('}');
            if (jsonEnd >= 0)
            {
                content = content.Substring(0, jsonEnd + 1);
            }
        }
        
        return content;
    }

    private async Task<(bool Success, string? ErrorMessage)> GenerateChunkInternal(Project project, ProjectsIDE chunk, ManifestResponse manifest)
    {
        var chunkDefinition = manifest.Chunks.FirstOrDefault(c => c.ChunkId == chunk.ChunkId);
        if (chunkDefinition == null)
        {
            return (false, "Chunk definition not found in manifest");
        }

        chunk.Status = "generating";
        chunk.ErrorMessage = null;
        await _context.SaveChangesAsync();

        var generationStartTime = DateTime.UtcNow;
        var chunkPrompt = BuildChunkGenerationPrompt(
            manifest.ProgrammingLanguage,
            manifest.GitHubPagesUrl,
            manifest.ProjectName,
            chunkDefinition,
            project.SystemDesign ?? string.Empty,
            manifest.SqlTables,
            manifest.Modules);
        
        var generationResponse = await CallClaudeAPIAsync(chunkPrompt);

        if (!generationResponse.Success)
        {
            chunk.Status = "failed";
            chunk.ErrorMessage = generationResponse.ErrorMessage;
            await _context.SaveChangesAsync();
            return (false, generationResponse.ErrorMessage ?? "Unknown error");
        }

        ChunkGenerationResponse? chunkResult;
        try
        {
            var cleanedJson = CleanJsonFromMarkdown(generationResponse.Content);
            chunkResult = JsonSerializer.Deserialize<ChunkGenerationResponse>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            chunk.Status = "failed";
            chunk.ErrorMessage = $"Failed to parse response: {ex.Message}";
            await _context.SaveChangesAsync();
            return (false, $"Failed to parse response: {ex.Message}");
        }

        if (chunkResult == null || chunkResult.Files == null || chunkResult.Files.Length == 0)
        {
            chunk.Status = "failed";
            chunk.ErrorMessage = "No files generated in response";
            await _context.SaveChangesAsync();
            return (false, "No files generated in response");
        }

        var filesJson = JsonSerializer.Serialize(chunkResult.Files);
        var generationTime = (int)(DateTime.UtcNow - generationStartTime).TotalMilliseconds;

        chunk.FilesJson = filesJson;
        chunk.FilesCount = chunkResult.Files.Length;
        chunk.Status = "completed";
        chunk.GeneratedAt = DateTime.UtcNow;
        chunk.TokensUsed = generationResponse.TokensUsed;
        chunk.GenerationTimeMs = generationTime;
        chunk.ErrorMessage = null;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    private string GetDefaultUserPromptTemplate()
    {
        return @"Generate a complete codebase structure for the following project:

Project Title: {0}
Project Description: {1}
Programming Language: {2}

System Design Modules:
{3}

Database SQL Script:
{4}

IMPORTANT REQUIREMENTS:
1. All file names, table names, class names, method names, variable names, and database identifiers MUST be in English. If any names in the input are in another language, translate them to appropriate English equivalents.

2. ⚠️ CODE FILE STRUCTURE - CRITICAL: ⚠️
   You MUST generate ACTUAL CODE, NOT descriptions!
   - Header: Brief summary (1-2 lines), inputs (1 line), outputs (1 line)
   - ACTUAL CODE: Real classes, properties, interfaces, method signatures with proper syntax
   - NO descriptions: Do NOT copy SystemDesign module descriptions into code
   - Implementation: Method bodies empty or 'throw new NotImplementedException();'
   - Comments: Brief 1-line comments for each element

❌ WRONG - DO NOT GENERATE:
// Auto-generated model for [Module Name]
// Language: C#
// Description: [Long description from SystemDesign copied here...]

✅ CORRECT - MUST GENERATE:
```csharp
// Module: User Authentication
// Summary: Handles login and registration
// Inputs: Email, password
// Outputs: Auth result, errors

// User class: User entity
public class User
{
    // Id: Primary key
    public int Id { get; set; }
    
    // Email: User email
    public string Email { get; set; } = string.Empty;
}

// IAuthService interface: Authentication contract
public interface IAuthService
{
    // Login: Authenticates user
    bool Login(string email, string password);
}

// AuthService class: Authentication logic
public class AuthService : IAuthService
{
    // Login: Validates credentials
    public bool Login(string email, string password)
    {
        throw new NotImplementedException();
    }
}
```

Generate a complete codebase structure that implements all modules with proper Models, Services, Controllers, and other necessary files. The code should be production-ready and follow best practices for {2}. Use English naming for all entities and include comprehensive comments as specified above.

CRITICAL: Return ONLY valid JSON matching this EXACT structure (use lowercase property names):
{
  ""models"": [{""name"": ""..."", ""path"": ""..."", ""content"": ""...""}],
  ""services"": [{""name"": ""..."", ""path"": ""..."", ""content"": ""...""}],
  ""controllers"": [{""name"": ""..."", ""path"": ""..."", ""content"": ""...""}],
  ""views"": [],
  ""applicationDbContext"": {""name"": ""..."", ""path"": ""..."", ""content"": ""...""},
  ""appSettings"": {""name"": ""..."", ""path"": ""..."", ""content"": ""...""},
  ""publishProfiles"": [],
  ""sqlScripts"": [{""name"": ""..."", ""path"": ""..."", ""content"": ""...""}],
  ""devContainer"": {""name"": ""..."", ""path"": ""..."", ""content"": ""...""}
}

DO NOT use ""projectName"", ""programmingLanguage"", or ""files"" array. Use the structure above.

⚠️ CRITICAL JSON ESCAPING REQUIREMENTS: ⚠️
All string values in ""content"" fields MUST be properly JSON-escaped:
- Newlines: Use \\n (backslash followed by n)
- Double quotes: Use \\"" (backslash followed by quote)
- Backslashes: Use \\\\ (double backslash)
- Tabs: Use \\t
- NEVER include actual newlines or unescaped quotes inside JSON strings
- The JSON must be valid and parseable by a standard JSON parser
Example: ""content"": ""// Module: User\\npublic class User {\\n    public int Id { get; set; }\\n}""";
    }
}

// Response models
public class CodebaseStructureResponse
{
    public int StudentId { get; set; }
    public int ProjectId { get; set; }
    public string ProgrammingLanguage { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public strAppersBackend.Services.CodebaseStructure CodebaseStructure { get; set; } = new();
}

internal class ParsedModule
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Inputs { get; set; } = string.Empty;
    public string Outputs { get; set; } = string.Empty;
}

public class GenerateManifestRequest
{
    public string? ProgrammingLanguage { get; set; }
    public string? GitHubPagesUrl { get; set; }
    public int? MockRecordsPerTable { get; set; }
}

