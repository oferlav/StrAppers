# Connecting Your Greenhouse Account to Skill-in

**Takes about 15 minutes. No developer needed.**

Skill-in plugs directly into your existing Greenhouse jobs and interview pipeline. Once connected, your job postings appear on our platform, and assessment results flow back into Greenhouse automatically — so your team never leaves the tools they already use.

There are two parts to the setup:
- **Part A** — Connect your job board (so Skill-in can display your open roles)
- **Part B** — Set up the Skill-in assessment stage (so Greenhouse sends candidates to us and receives their scores back)

You can do Part A on its own if you only want to list jobs. Part B requires Skill-in to be approved as a Greenhouse Assessment Partner — we'll let you know when that's live.

---

## Part A — Connect Your Job Board

### Step 1 — Find Your Board Token

Your Board Token tells Skill-in which Greenhouse job board to connect to.

1. Log into Greenhouse → **Configure** (gear icon, top right) → **Job Board**
2. Find your public job board and look at the **Board URL**:
   ```
   https://boards.greenhouse.io/acme-corp
   ```
3. Your Board Token is the last segment — in this example: **`acme-corp`**

> Copy this. You'll paste it into Skill-in when connecting.

---

### Step 2 — Create a Job Board API Key

This key lets Skill-in forward candidate applications directly into your Greenhouse pipeline.

1. In Greenhouse → **Configure → Dev Center → API Credential Management**
2. Click **Create New API Key**
3. API Type: **Job Board**
4. Name it `Skill-in Integration`
5. Click **Create** and copy the key immediately — Greenhouse only shows it once

> Paste this key into Skill-in during setup. We store it encrypted and only use it to route applications.

---

### Step 3 — Add Four Custom Fields to Your Jobs

These fields let you add challenge descriptions, resources, expectations, and Q&A to your job postings — the details that matter most to candidates on Skill-in.

**How to create each field:**
1. Greenhouse → **Configure → Custom Fields**
2. Click **Add Custom Field** (top right)
3. Object type: **Job**
4. Fill in the details from the table below → **Save**

| Field Name | Field Type |
|------------|-----------|
| **Challenge** | Long Text |
| **Resource** | Short Text |
| **Expectations** | Long Text |
| **Q&A** | Long Text |

> Field names must match exactly, including capitalisation.

---

### Step 4 — Expose Custom Fields to the Job Board API

By default, custom fields are internal. This makes them visible to Skill-in.

For each of the four fields:
1. **Configure → Custom Fields** → click the field name
2. Check **"Expose in Job Board API"**
3. Save

---

### Step 5 — Fill in the Custom Fields on Your Jobs

1. Go to a job → **Job Setup → Job Posts**
2. Scroll to the custom fields section
3. Fill in **Challenge**, **Resource**, **Expectations**, and **Q&A**
4. Save

> You can also fill these in directly on Skill-in — whatever's in Greenhouse will pre-populate, and you can edit from there.

---

### Step 6 — Make Sure Your Jobs Are Published

Skill-in only sees **Live** jobs. Check each job you want to feature:

1. Job → **Job Posts**
2. Status next to your public board should show **Live**

---

## Part B — Enable Skill-in Assessments in Greenhouse

> **Prerequisite:** This section only applies once Skill-in is listed as a Greenhouse Assessment Partner. We'll notify you when this is available.

This is what makes Skill-in appear as an assessment option inside your Greenhouse pipeline — so your team can send candidates a Skill-in challenge directly from a candidate's profile, and scores come back automatically.

---

### Step 7 — Add a Skill-in Stage to Your Interview Plan

1. Open a job in Greenhouse → **Job Setup → Interview Plan**
2. Click **+ Add Stage**
3. Select **Take Home Test**
4. In the stage settings, under **Assessment Partner**, select **Skill-in**
5. Choose which challenge to assign (e.g. "Full-Stack Challenge", "Frontend Challenge")
6. Save the interview plan

Once this is set up, any candidate who reaches this stage will have a **"Send Test"** button on their profile.

---

### Step 8 — Send a Skill-in Challenge to a Candidate

When a candidate reaches the Take Home Test stage:

1. Open the candidate's profile in Greenhouse
2. Go to the interview stage with Skill-in configured
3. Click **Send Test**
4. Greenhouse will automatically send the candidate an invitation email to complete their Skill-in challenge

No further action needed. The candidate registers on Skill-in, completes the challenge, and their score appears back in Greenhouse on their profile — usually within minutes of completion.

---

### Step 9 — View Results in Greenhouse

Once a candidate completes their challenge:

- Their **score** appears on the candidate's profile in Greenhouse
- Click the **"View on Skill-in"** link to see the full results breakdown
- You can filter and bulk-advance candidates by score within Greenhouse

---

## Quick Reference

| What | Where in Greenhouse | What to copy/do |
|------|---------------------|-----------------|
| Board Token | Configure → Job Board → Board URL | Last segment of URL |
| Job Board API Key | Configure → Dev Center → API Credential Management | Generated key string |
| Custom Fields | Configure → Custom Fields | Create 4 fields; check "Expose in Job Board API" |
| Assessment Stage | Job Setup → Interview Plan → Add Stage → Take Home Test | Select "Skill-in" |

---

## Need Help?

Contact us at **support@skill-in.com** and we'll get you set up quickly.
