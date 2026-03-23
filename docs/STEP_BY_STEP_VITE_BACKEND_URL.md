# Step by step: `VITE_STRAPPERS_BACKEND_URL` + board chat (beginner-friendly)

This turns on **direct** calls from your React app to **StrAppersBackend** (for features we’ve wired, e.g. mentor **model list** in `ChatSidebar`). If the variable is **missing**, the app keeps using **base44** like before.

---

## Part A — What you’re doing (in plain English)

1. Your **frontend** runs in the browser (e.g. `http://localhost:5173` from Vite).
2. Your **backend** is another address (e.g. `https://api.yourcompany.com` or `http://localhost:5xxx`).
3. **`VITE_STRAPPERS_BACKEND_URL`** tells the frontend: “when using the direct client, talk to **this** base address.”
4. **Vite** only reads `.env` when it **starts**. So after changing `.env`, you must **stop** and **start** `npm run dev` again.

---

## Part B — Step-by-step

### 1. Know your backend “base URL”

This is the address **without** `/api/...` at the end.

**Examples:**

| Situation | Example base URL |
|-----------|------------------|
| Backend on your PC (typical dev) | `http://localhost:5000` or whatever port Visual Studio / `dotnet run` shows (could be `https://localhost:7xxx`) |
| Backend deployed on the internet | `https://your-api.azurewebsites.net` |

**How to find local port:** run the backend project, look at the console for “Now listening on: `http://...`”.

Write that down exactly (including `http` vs `https`).

---

### 2. Open your frontend folder

Path you’ve been using:

`C:\StrAppers\strAppersFrontend`

---

### 3. Create or edit the `.env` file

Same folder as `package.json`: `C:\StrAppers\strAppersFrontend`.

**Windows gotcha:** Explorer / Save As may say you must provide a file name if you try to name a file only **`.env`** (it treats it as “extension only”). Use one of these:

| Method | What to do |
|--------|------------|
| **Cursor / VS Code** | New file → Save as `.env` in `strAppersFrontend`. |
| **PowerShell** | `cd C:\StrAppers\strAppersFrontend` then `Set-Content -Path .env -Value "VITE_STRAPPERS_BACKEND_URL=http://localhost:5000"` (edit URL). |
| **Cmd + Notepad** | `cd` to folder, run `notepad .env` → Yes to create new file. |
| **Explorer** | New text file, name it **`.env.`** (dot at end) → Windows saves as **`.env`**. |

- Or copy **`.env.example`** to **`.env`** in the editor (Save As `.env`).

Add **one** line (use **your** real URL):

```env
VITE_STRAPPERS_BACKEND_URL=http://localhost:5000
```

Replace `http://localhost:5000` with whatever you wrote in step 1.  
**No** trailing slash required (both work with our client).

Save the file.

---

### 4. Restart the dev server

1. In the terminal where the frontend is running, press **Ctrl+C** to stop it.
2. Start again:

```bash
cd C:\StrAppers\strAppersFrontend
npm run dev
```

Wait until it prints a local URL (often `http://localhost:5173`).

---

### 5. Open the app and board chat

1. In the browser, open the URL Vite printed (e.g. `http://localhost:5173`).
2. Log in / navigate the app the **same way you usually do** to reach a **board** where the **mentor chat sidebar** appears (Board room / board page — depends on your app routes).
3. Open the UI where the **model dropdown** loads (mentor models). That triggers `fetchModels`.

**If `VITE_STRAPPERS_BACKEND_URL` is set:** the browser will request:

`{your base URL}/api/Mentor/use/get-models`

---

## Part C — If the model list fails: check the browser

### 1. Open developer tools

- Press **F12** (or right‑click → **Inspect**).

### 2. Open the **Network** tab

- Reload the page or trigger the chat/models again.
- Find a request named **`get-models`** or whose URL ends with **`/api/Mentor/use/get-models`**.

### 3. Read the result

| What you see | Meaning |
|--------------|--------|
| **Status 200** | Backend answered OK; if UI still empty, we debug response shape next. |
| **Status 401 / 404 / 500** | URL wrong, backend not running, or server error — read response body / backend logs. |
| **(failed)** or **CORS error** in **Console** tab | Browser blocked the response because backend didn’t allow your **frontend origin**. |

---

## Part D — What “CORS” means (short)

Browsers only allow your page at **`http://localhost:5173`** to read responses from **another** origin (your API) if the **API** says “this origin is allowed.”

That’s configured on **StrAppersBackend** (ASP.NET), not in `.env`.

**Typical dev fix:** in backend startup, CORS policy must include:

- `http://localhost:5173` (or whatever port Vite uses)

**If you use HTTPS for API** with a dev certificate, sometimes you also need to trust the cert — separate from CORS.

---

## Part E — Turn off direct backend (go back to base44 only)

- Remove the line from `.env`, or empty the value, or delete `.env`.
- Restart `npm run dev` again.

---

## Quick checklist

- [ ] Backend running and you know its base URL  
- [ ] `.env` in `strAppersFrontend` with `VITE_STRAPPERS_BACKEND_URL=...`  
- [ ] **Restarted** `npm run dev` after saving `.env`  
- [ ] Opened board chat; checked **Network** for `get-models`  
- [ ] If CORS: add frontend origin to backend CORS policy  

---

*Related: `docs/STRAPPERS_BACKEND_CLIENT.md`, `strAppersFrontend/.env.example`.*
