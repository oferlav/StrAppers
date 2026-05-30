# Provisioned Codebases — TODOs

## TODO #1 — Enable informational startup logging (all 7 codebases)

**Problem:** All provisioned backends suppress startup output (logging set to `Warning` level).  
When a student runs the server locally, the terminal goes silent after the build — Kestrel / the HTTP server never prints `Now listening on: http://localhost:PORT`.  
Students cannot tell whether the server is running or has crashed.

**Fix per language:** Lower the default logging level to `Information` (or equivalent) so the framework prints its startup/listening message. Only affects local development experience — the deployed app is unaffected since Railway injects `PORT` and the app binds immediately.

| Language | File to change | Change |
|---|---|---|
| **C# (ASP.NET Core)** | `appsettings.json` | `"Default": "Warning"` → `"Default": "Information"` |
| **Node.js (Express)** | Startup file (`index.js` / `app.js`) | Add `console.log(\`Server running on port ${PORT}\`)` after `app.listen(...)` |
| **Python (FastAPI / Flask)** | Startup / `uvicorn` / `app.run()` call | Enable `INFO` log level in uvicorn/Flask startup config |
| **Ruby (Rails / Sinatra)** | `config/environments/development.rb` or startup | Set `config.log_level = :info` (Rails) or add startup `puts` (Sinatra) |
| **Java (Spring Boot)** | `application.properties` | `logging.level.root=INFO` (default is already INFO in Spring Boot — verify not overridden) |
| **PHP (Laravel / Slim)** | `.env` or startup | `APP_LOG_LEVEL=info` (Laravel) or add startup echo (Slim) |
| **Go** | `main.go` | Add `log.Printf("Server listening on :%s", port)` before `http.ListenAndServe(...)` |

**Test criteria:** Running the server locally prints a clear `listening on http://localhost:XXXX` line within 5 seconds of startup.

**Note:** The C# `ObjectDisposedException` crash-handler bug (catch block calling `app.Services` after host disposal) was already fixed in the generated project by the code agent on 2026-05-30.
