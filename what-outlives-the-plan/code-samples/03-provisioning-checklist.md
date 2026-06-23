# Day-one provisioning checklist — paid external accounts

This is a copy-paste checklist for a project that's about to integrate
*anything* that requires a paid external account with a multi-day
approval clock. The version below is filled in for a mobile game with
in-app purchases (the §4 lesson in the article); the template is in the
last section.

The rule it codifies: **a sentence in the plan is not a date on a
calendar.** Anything that takes multi-day external approval has to live
on a shelf the code can't drift away from. The plan can describe the
dependency, but the calendar — or whatever you actually look at every
Monday — is what makes it survive.

## Filled-in example: a mobile game with IAP

| Account / resource | Approval clock | Depends on | Status | Start by |
|---|---|---|---|---|
| Apple Developer Program | 24–48 hrs (D-U-N-S can take 2 weeks for orgs) | — | **TODO** | day 1 |
| Mac (physical or MacInCloud) for iOS build | minutes (MacInCloud) / weeks (purchase) | — | **TODO** | day 1 |
| App Store Connect app record + bundle ID | hours | Apple Dev approved | blocked | after Apple Dev |
| Google Play Console | 48–72 hrs (identity verification) | — | **TODO** | day 1 |
| Play Console app record + signing key | hours | Play Console approved | blocked | after Play Console |
| Google Cloud project + Pub/Sub topic (Play Billing notifications) | hours | Play Console approved | blocked | after Play Console |
| Stripe account (live mode + payouts) | 24 hrs to weeks (business verification) | — | **TODO** | day 1 |
| Sandbox test accounts (Apple + Google) | minutes | each store approved | blocked | after each store |

## Why each column matters

- **Approval clock** — the *minimum* wall-clock time, not the active work
  time. Apple's D-U-N-S lookup for an org can take two weeks. Treat the
  high estimate as the planning number, not the low one.
- **Depends on** — serial chains hide. The Pub/Sub topic looks like it
  should be parallel-startable, but you cannot create it inside a Google
  Cloud project linked to a Play Console app you do not yet own. Walk
  every dependency back to its day-one trigger.
- **Status** — kept on whatever shelf you actually look at. Issue
  tracker, calendar, Notion, sticky note on your monitor. Just not in
  `plan.md`, because `plan.md` is going to get reshuffled and this row
  will get reshuffled with it.
- **Start by** — the date you should have *kicked off* this approval to
  not be the critical path. Backsolve from the feature's target date.
  Multi-day approvals that you "start when you're ready to integrate"
  are how you discover the feature is done before it can be tested.

## The template

```markdown
| Account / resource | Approval clock | Depends on | Status | Start by |
|---|---|---|---|---|
| <paid external account #1>     | <high estimate of wall-clock time> | — | TODO | day 1 |
| <dependent resource>           | <time>                              | <#1> | blocked | after <#1> |
| <sandbox / test environment>   | <time>                              | <#1> | blocked | after <#1> |
```

Fill it in *before* the first commit that depends on any row. Pin it
somewhere the code can't reach to silently change it.
