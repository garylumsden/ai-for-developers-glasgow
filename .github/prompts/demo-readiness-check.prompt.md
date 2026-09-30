# Demo readiness check

Run a read-only readiness check for the complete **AI for Developers** session.

Do not deploy, delete, purge, upload, create a repository, or change configuration.
Do not print secrets or `.env` values.
Use public package feeds for package restore or audit operations.

## Output

Print one table with these columns:

| Act | Check | Status | Evidence | Fix |
|---|---|---|---|---|

Use:

* `GREEN` when the check passes.
* `AMBER` when a presenter action or live dependency remains.
* `RED` when the demo path is broken.

End with:

1. The first blocking fix.
2. The commands the presenter must run.
3. The fallbacks that are ready.

## Repository layout

Verify these paths:

* `act-1-pull-the-plug/`
* `act-2-council/`
* `act-3-audience-council/`
* `docs/PRESENTER-GUIDE.md`
* `scripts/reset-all.ps1`
* `scripts/reset-all.sh`

Verify the template application remains under `act-2-council/`.
Verify root Copilot agents, instructions, workflows, and prompts use `act-2-council/` paths.

## Act 1

1. Verify Copilocal and GitHub Copilot CLI are installed.
2. Read `act-1-pull-the-plug/task.json`.
3. Verify the local model in `task.json` is installed.
4. Verify the local model has the configured context and native tool use.
5. If the local model changed, return `AMBER` and instruct the presenter to run
   `.github/prompts/act1-choose-task.prompt.md`.
6. Run `act-1-pull-the-plug/prepare.ps1`.
7. Check that each completed workspace has a non-empty `index.html`. Do not fail for content,
   capitalisation, design, or HTML structure. The audience judges output quality.
8. Verify the cloud model is available without starting a comparison run.
9. Ask the presenter to disable Wi-Fi or disconnect the cable before the final local-provider and
   external-connectivity check. Do not change the operating-system network setting.
10. Verify `results/results.sample.json` is valid and marked as rehearsal data.
11. Verify completed runs open their output pages. Use `--no-open` when testing without a browser.
12. Verify the terminal uses readable text by default. JSON output is optional through `--json`.
13. For parallel rehearsals, keep the network connected. Do not prepare or reset during active runs.

## Act 2

1. Build `act-2-council/src/GovernanceCouncil.slnx`.
2. Validate `act-2-council/infra/main.bicep`.
3. Run `azd show` from `act-2-council/`.
4. Verify the generated ignored `.env` exists without printing it.
5. Run the `council-tools` self-test.
6. Start `council-tools` temporarily. Verify `/health` and the MCP smoke test.
7. Verify Foundry IQ startup readiness.
8. Verify Web IQ is usable only when its key is intentionally configured.
9. Verify all five `council-tools` tools have live and committed cached paths.
10. Verify each council member's exact allow-list and confirm the Moderator has no MCP tools.
11. Verify the optional GitHub server is read-only. Report `AMBER` when its token is intentionally
    absent.
12. Run `act-2-council/scripts/act1-to-dossier.ps1` with a temporary sample-data path. Confirm both markers
    remain and rerunning is idempotent. Restore the marker-only file afterward.
13. Verify Local MAF uses the `Fast` profile and the rehearsal timing is at most six minutes.
14. Verify the Assessment UI contains distinct `📚` and `🔧` evidence.
15. Verify content-safety thresholds remain `Medium`.
16. At presenter browser zoom, verify **Hide menu** releases content space and **Show menu** restores navigation.
    In narrow windows, verify **Menu**, **Close**, and `Escape` keep navigation usable.
17. Verify initial-position cards stay above the chat and expose the full assessment on selection.
18. Verify longer debate replies, multiple paragraphs, and plain text remain complete without format-repair calls or rejection notices.
    Keep content-safety blocks and explicit empty-response notices. Keep the Chair's output unchanged.
    Verify phase-specific user messages contain response length requirements; persona system prompts must not contain them.
19. Verify the selected speaker's avatar shows typing dots before its complete reply arrives.
    Verify reduced motion and **Pause animation** stop the dots.
20. Verify avatar hand indicators expose the supplied reason on hover, focus, and member selection.
21. Verify `🛠️` message badges expand recorded tool activity and remain separate from `📚` citations.
22. Verify scrolling upward preserves the reading position. Verify **Return to live** resumes following.
    Opening and closing a member inspector must not pause following by itself.
23. Reload open debate pages after a reset to clear view-only chat, hands, and typing state.

## Act 3

1. Verify `gh auth status`.
2. Verify `garylumsden/azure-agent-council-template` is reachable.
3. Validate the question card, starter prompt, fallback scenario, persona prompts, and dossier.
4. Verify the environment-reuse steps copy only the ignored generated `.env`.
5. Verify the retained rehearsal repository in `rehearsal-repositories.json` still exists and is
   private.
6. Build the ignored local rehearsal clone when it exists.
7. Verify the fallback scenario loads on Local MAF with the `Fast` profile.

## Whole repository

1. Run `git status` and identify uncommitted files.
2. Run the clean solution build.
3. Run dependency vulnerability checks.
4. Run a secret scan over the working tree and full Git history.
5. Verify live results, `.env`, `.azure`, run workspaces, logs, and rehearsal clones are ignored.
6. Run `scripts/reset-all.ps1 -WhatIf`. Confirm it preserves Cosmos containers and vector indexes.
7. Confirm `act-2-council/config/rehearsal-data.json` contains every rehearsal dossier title or ID that
   should be cleaned.
8. Confirm the presenter guide contains commands, timings, talk tracks, and fallbacks for all acts.
9. Confirm fallback videos exist at the presenter's chosen local media location. The videos do not
   need to be committed.
10. Report the repository visibility. Return `AMBER` while it remains private after the final scan.
