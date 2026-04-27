# THBIM AutoUpdate — Privacy Notice

**Last updated:** 2026-04-27 · **Version:** 1.0

This document explains what personal data the THBIM AutoUpdate desktop app
collects, how it is used, and the rights you have over it. We follow GDPR
(EU) Art. 13/14 and Vietnam Decree 13/2023 on personal data protection.

---

## 1. Data Controller

THBIM (the publisher of THBIM AutoUpdate). Contact via the publisher's
website or the email address on the product page.

---

## 2. Data we collect

When you sign in to THBIM AutoUpdate, the following is collected and
processed:

| Data point | Source | Purpose |
|------------|--------|---------|
| Email address | You enter it | Account identifier; license binding |
| Full name | Your account profile | Display in the account view |
| Password | You enter it | Authentication only — never logged or stored locally |
| Machine ID (SHA-256 hash of UUID + motherboard + BIOS + disk + Windows MachineGuid) | Computed locally | Bind one license seat to one device |
| Computer host name | `Environment.MachineName` | Sent as a user-agent label |
| License tier and expiration | Returned by the licensing server | Gate premium features |
| Authentication token | Returned by the licensing server | Keep you signed in between sessions |

Additionally, the app makes **unauthenticated GitHub API calls** to fetch
release manifests for THBIM addins. These calls do not include any user
identifier; only the GitHub server logs the request IP per its own privacy
policy.

**We do NOT collect:**
- Revit project files, sheet content, family data, or model geometry.
- Excel spreadsheet contents.
- Analytics, telemetry, or usage tracking events.
- Browsing history, clipboard contents, or files outside the addin folder.

---

## 3. Where data is stored

### Locally

| Location | Contents | Encryption |
|----------|----------|------------|
| `%APPDATA%\THBIM\AutoUpdate\` | App settings (auto-update preferences, intervals) | Plain JSON (no PII) |
| `%LOCALAPPDATA%\THBIM\Licensing\session_v2_5.lic` | Auth token, email, full name, tier, expiration | DPAPI (LocalMachine scope) |
| `%LOCALAPPDATA%\THBIM\Licensing\verify.stamp` | UTC date of last server verification | Plain text (no PII) |
| `%LOCALAPPDATA%\THBIM\Licensing\consent.stamp` | UTC timestamp of privacy acceptance | Plain text (no PII) |

You can erase all of the above at any time using the **Delete My Data**
button in the account view (see §6 below).

### On our server

Stored on a Google Apps Script-backed Google Sheet:

- Email, full name, machine ID, license tier, expiration date.
- Activation history (which keys were applied to which machine).
- Server-side log entries when you sign in or activate a key.

Data is transmitted over HTTPS and is not shared with third parties for
marketing.

---

## 4. Legal basis (GDPR Art. 6)

- **Contract performance** (Art. 6(1)(b)): we need your email and machine
  ID to validate and enforce your license terms.
- **Consent** (Art. 6(1)(a)): we ask for explicit acceptance of this
  privacy notice on the first sign-in. You can withdraw consent at any
  time by signing out and using **Delete My Data**.

---

## 5. Retention

| Data | Retention |
|------|-----------|
| Local session cache | Until you sign out or call **Delete My Data** |
| Server-side account record | Indefinite, until you request deletion |
| Server-side activation log | 3 years |
| Consent stamp | Until you call **Delete My Data** |

---

## 6. Your rights

You have the right to:

| Right | How to exercise |
|-------|-----------------|
| **Access** — get a copy of your data | Email us with subject "Data access request" |
| **Rectification** — correct inaccurate data | Update your account on https://thbim.pages.dev |
| **Erasure** — delete your data | (a) **Delete My Data** button removes local data; (b) for server data, email us with subject "Delete my account" |
| **Restriction / Objection** — limit processing | Email us |
| **Data portability** — receive data in machine-readable form | Email us with subject "Data export request" |
| **Withdraw consent** | Sign out + **Delete My Data**, then do not sign back in |
| **Lodge a complaint** | Contact your local data-protection authority |

We respond to verified rights requests within 30 days.

---

## 7. International transfers

Our licensing server is hosted on Google's infrastructure (Google Apps
Script + Google Sheets). Depending on Google's routing this may involve
transfer outside your country of residence. Google maintains
GDPR-adequate safeguards under the EU–US Data Privacy Framework.

---

## 8. Security

- All client-server traffic uses HTTPS (TLS 1.2+).
- Local session cache is encrypted with Windows DPAPI (machine-bound).
- We do not log or store passwords. Authentication uses a one-shot token
  exchange.
- The auto-update mechanism verifies downloads are served from
  `github.com` over HTTPS.

If we discover a personal-data breach we will notify affected users
within 72 hours of becoming aware.

---

## 9. Auto-update behaviour

THBIM AutoUpdate periodically calls the GitHub Releases API
(`api.github.com`) to check for new versions of installed addins. These
API calls are anonymous from the app's perspective — no user identifier
is included. GitHub may log the request IP per their own privacy
policy.

---

## 10. Children

THBIM AutoUpdate is professional engineering software for use by adults.
We do not knowingly collect data from anyone under 16.

---

## 11. Changes to this notice

If we materially change what data we collect, we will:
1. Update the `Last updated` date and bump the **Version** number.
2. Re-prompt you for consent the next time you sign in.

The current version is recorded in `consent.stamp` so we can detect
version drift.

---

## 12. Contact

For privacy-related questions, please email the publisher (see the
product page for the current contact email). We acknowledge within
7 days and resolve within 30 days.
