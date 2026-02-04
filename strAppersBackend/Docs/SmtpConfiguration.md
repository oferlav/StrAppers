# SMTP Email Configuration

Meeting invitations and welcome emails are sent via SMTP when `Smtp:UseSmtp` is `true`. Board creation sends meeting invite emails to students; if SMTP credentials are missing, sends fail with **5.7.0 Authentication Required**.

## Required settings in production

In **appsettings.Production.json** (or via environment variables), set:

- **Smtp:User** – SMTP login (e.g. Gmail/Google Workspace email).
- **Smtp:Pass** – SMTP password. For Gmail/Google Workspace use an [App Password](https://support.google.com/accounts/answer/185833), not the normal account password.

If either is empty, the app will throw a clear error on first send: *"SMTP credentials are not configured. Set Smtp:User and Smtp:Pass."*

## Gmail (smtp.gmail.com)

- **Host:** `smtp.gmail.com`, **Port:** `587`, **Security:** `StartTls`.
- **User:** full Gmail or Google Workspace address (e.g. `admin@skill-in.com` if that is the Workspace account).
- **Pass:** App Password from [Google App Passwords](https://support.google.com/accounts/answer/185833).
- **FromEmail:** Should match the authenticated account or a verified alias in that account.

## Environment variables (optional)

You can override without editing JSON:

- `Smtp__User`
- `Smtp__Pass`

Use double underscore `__` for nested keys in .NET Configuration.
