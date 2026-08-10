---
id: skill-list-invoices
version: 2.0.0
kind: skill-description
boundTo: list-invoices
classification: Internal
---

Lists invoices for the current tenant, most recent first.

Use this when the user asks which invoices exist, or asks about invoices
matching a status or a date range. It returns invoice identifiers, status,
issue date and amount — it does not return payment history, remittance
detail, or anything about a payer.

This skill only ever reads. It cannot change an invoice. If the user asks
you to alter one, use the appropriate write skill instead; do not describe
this skill as though it could.
