# Copilot Instructions

This repository supports the **AI for Developers** Glasgow Azure User Group session.

## Layout

* `act-1-pull-the-plug/` contains the offline local-model and online cloud-model comparison.
* `act-2-council/` contains the configured Agent Council application, Azure IaC, data, and council scripts.
* `act-3-audience-council/` contains preparation for the audience-designed council.
* `docs/` contains session-level build and presenter guidance.
* `scripts/` contains whole-session automation.

## Rules

* Keep the three acts isolated. Do not place council engine code outside `act-2-council/`.
* Keep scenario details in `act-2-council/config/`, not in the council engine.
* Use identity-based Azure authentication and Bicep IaC.
* Never commit secrets, `.env` files, Azure environment state, live run workspaces, or live results.
* Keep content-safety thresholds at `Medium`.
* Use .NET 10 and C# 13 for council code.
* Preserve the council dependency pins.
* Provide PowerShell and Bash versions of presenter-run scripts.
* Keep every live network dependency usable through a cached or recorded fallback.
* Use public package feeds. Do not add organisation-internal package feeds to the repository.
* Keep customer documentation location-neutral. Require the customer or presenter to select an Azure location.
* Run the relevant clean validation before each phase commit.
* Keep `docs/PRESENTER-GUIDE.md`, `.github/prompts/demo-readiness-check.prompt.md`, and
  `scripts/reset-all.*` synchronized with measured demo behavior.
