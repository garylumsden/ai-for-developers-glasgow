# Presenter Guide: AI for Developers

This guide is for Gary Lumsden. The session is 45 minutes.

## Run of show

| Time | Section | Notes |
|---|---|---|
| 0–3 | Cold open | Pull the network cable. Ask: "How much of your AI still works right now?" |
| 3–15 | Act 1: Pull the Plug | Run the same task locally and in the cloud. Record the hands-up vote. |
| 15–18 | Act 2: Meet the council | Introduce the personas. Say: "The council reads; the tools act." |
| 18–25 | Act 2: The debate | Bridge Act 1 results, submit the Proposal, and show `📚` versus `🔧`. |
| 25–28 | Act 3: Audience designs a council | Use the question card. |
| 28–34 | Act 3: Scenario Architect builds it live | Show the generated files while the build runs. |
| 34–40 | Act 3: The audience's council debates | Use Local MAF and the `Fast` profile. |
| 40–45 | Wrap and Q&A | State the takeaways and show the project links. |

If an act runs long, reduce Q&A. Do not remove another act.

## Terminal plan

| Terminal | Folder | Use |
|---|---|---|
| A | Repository root | Act 1 and whole-session scripts |
| B | `act-2-council/` | Council web app |
| C | Repository root | `council-tools` MCP server |
| D | Fresh Act 3 clone | Audience council |

Use a terminal font that is readable from the back row. Use browser zoom if an HTML result or tool
record is too small.
In the council app, select **Hide menu** above the navigation links to release space for the content.
Select the menu button to restore the links. Narrow browser windows use **Menu** and **Close**.

## Before the audience arrives

1. Select an Azure location in the local azd environment.
2. Run `azd provision` from `act-2-council/` only if the environment needs repair.
3. Run the readiness prompt at [`.github/prompts/demo-readiness-check.prompt.md`](../.github/prompts/demo-readiness-check.prompt.md).
4. Run `scripts/reset-all.ps1 -WhatIf`.
5. Record fallback videos for each act. Store them outside the Git repository.

## Act 1: Pull the Plug

### Folder

Use Terminal A in `act-1-pull-the-plug/`.

### Selected task

Granite creates a self-contained fictional Glasgow Subway service board from `brief.txt`.

The task suits `ibm/granite-4-h-tiny` because it uses one short input file, one created HTML file,
and one output-file check. The current Foundry Local NPU catalogue had tool-capable variants, but
their exact compiled context was below Copilot's minimum. Granite loaded with 131,072 context tokens.

Measured final local times were 155.36, 92.74, and 81.71 seconds. The cloud run took 56.76 seconds.
Those measurements used the earlier prompt. The current prompt allows the model to choose its HTML
structure and design. Completion measures execution and non-empty output, not exact text or markup.

### Commands

Prepare both clean workspaces:

```powershell
.\prepare.ps1
```

Disconnect the network. Run the local model:

```powershell
.\run-local.ps1 --label local-live
```

Reconnect the network. Run the cloud model:

```powershell
.\run-cloud.ps1 --label cloud-live
```

Each completed run opens its generated page in the default browser. Use `--no-open` to prevent this.
The scripts display readable Copilot activity in non-interactive prompt mode. They do not show the
full interactive chat UI. Use `--json` only when you need raw events.

For a parallel rehearsal, keep the network connected and run local and cloud in separate terminals.
Run `prepare` once beforehand. Do not prepare or reset while either run is active. Result and vote
updates are safe to write concurrently. Parallel timings include shared laptop load.
Run only one local process and one cloud process at a time.

For the on-stage cable-disconnect demonstration, run local first and reconnect before cloud.

After showing both pages, ask: "Which result would you prefer to use: local or cloud?"
Record the hands-up count for each result. This optional poll records audience preference, not an
objective score. Missing votes stay unknown in the result table and council dossier.
Votes do not change run completion. These are not the Act 2 council-member votes.

Record the counts:

```powershell
.\record-vote.ps1 --label local-live --vote <hands>
.\record-vote.ps1 --label cloud-live --vote <hands>
.\show-results.ps1
```

Bash equivalents have the same file names with `.sh`.

### What appears

* A large run heading and a ten-second timer.
* `Local provider check: PASS`.
* `External connectivity probe: BLOCKED`.
* Copilot tool activity.
* A final `RUN COMPLETED` or `RUN FAILED` for execution and file generation.
* The generated page opens in the browser after completion.
* A side-by-side result table after the vote.

The audience judges the generated page. Missing headings, different capitalisation, and other content
or design choices do not fail the run. Timeouts, unsuccessful process exits, and missing or empty
output still fail.

### Talk track

* "The task was designed for this model, not the other way round."
* "Local does not mean free. It moves cost into hardware, power, and support."
* "Offline is a capability test, not a quality claim."
* "Tokens are observed. Unknown values stay unknown."

### Fallback

Play the recorded local and cloud run videos. Then run `show-results.ps1 --sample`.

## Act 2: The Glasgow Developer Council

### Folders

Use Terminal C at the repository root. Use Terminal B in `act-2-council/`.

### Start the services

From the repository root, the recommended command is:

```powershell
.\scripts\start-act2.ps1
```

```bash
./scripts/start-act2.sh
```

The script builds the Release solution, starts `council-tools`, starts the council with Local MAF and
the `Fast` profile, verifies both endpoints, and keeps both processes attached. Press `Ctrl+C` to
stop the processes it started.

Use `-SkipBuild` in PowerShell or `--skip-build` in Bash after a successful readiness build.

Check `http://127.0.0.1:5199/health`.

### Bridge Act 1 evidence

From Terminal B:

```powershell
.\scripts\act1-to-dossier.ps1
```

If a live run failed after creating `results.json`, run:

```powershell
.\scripts\act1-to-dossier.ps1 --sample
```

The explicit flag prevents failed live data from overriding the fallback.

Open the Dossier Library. Upload
`data/policies/run-coding-agents-on-local-models.md`. Start the deliberation.

The earlier Local MAF rehearsal completed in about 226 seconds.
This timing predates the chat layout and phase-specific response limits. Measure the revised run before presenting.

### What appears

* Four initial-position cards above the chat. Select a card to read its full assessment.
* Short debate message bubbles, with the pending speaker's avatar and bouncing typing dots.
* Moderator calls and selected speakers. Raised hands appear on avatars; hover or focus shows the reason.
* `📚` knowledge evidence from Foundry IQ.
* `🛠️` message badges for recorded tool activity. The Assessment retains `🔧` tool evidence.
* An Assessment with expandable tool-call JSON.

Messages arrive complete, not token by token. Debate prompts request at most three sentences in one paragraph.
The app displays longer replies, multiple paragraphs, and other returned formats without a format-repair call.
Content-safety blocks remain unchanged. The app reports an empty model response explicitly.
The phase's user message sets the requested response length. Initial positions retain their full assessment length.
If an initial assessment still fails after one format repair, its card says **Initial position unavailable**.
The Chair's output remains unchanged. Scroll upward to read earlier messages; select **Return to live** to resume following.
Inspecting an initial position does not pause automatic following.
After resetting rehearsal data, reload any open debate page to clear its view-only chat and typing state.

### Persona and tool map

| Member | Question | Tool |
|---|---|---|
| The Vibe Coder | Is the project active and usable? | `get_repo_activity` |
| Dr No | Is the model approved? | `check_model_register` |
| The Bean Counter | What does the comparison cost? | `compare_run_costs` |
| The Greybeard | What is the support horizon? | `get_product_lifecycle` |

### Talk track

* "The council reads; the tools act."
* "MCP is a protocol, not a service, like HTTP."
* "An allow-list decides which persona can call which tool."
* "Cached evidence is visible. The system does not pretend it is live."
* "The presenter still owns the decision."

### Fallback

Use the rehearsal dossier and the recorded debate video. The committed MCP snapshots let Local MAF
continue when the public APIs fail.

## Act 3: The Audience's Council

### Files

Use [`../act-3-audience-council/QUESTION-CARD.md`](../act-3-audience-council/QUESTION-CARD.md) and
[`../act-3-audience-council/STARTER-PROMPT.md`](../act-3-audience-council/STARTER-PROMPT.md).

### Live sequence

1. Ask the audience for the premise, name, emoji, document type, three or four members, and tone.
2. Create a fresh private repository from `garylumsden/azure-agent-council-template`.
3. Copy Act 2's ignored `.env` into `src/GovernanceCouncil.Web/.env`.
4. Run the Scenario Architect with the completed starter prompt.
5. Start Local MAF with the `Fast` profile.
6. Upload the generated dossier.
7. Start the deliberation.

Measured fallback path:

| Step | Time |
|---|---:|
| Create and clone | 13.82 seconds |
| Copy environment and fallback | 0.14 seconds |
| Build | 5.28 seconds |
| Start app | About 13 seconds |
| Deliberation | About 155 seconds |

The fresh council shares Act 2's Azure storage and Cosmos DB. A cross-assessment Nexus is possible,
but do not promise it.

### Talk track

* "You are designing the conflict, not writing agent code."
* "Three strong viewpoints beat ten polite duplicates."
* "The configuration is the scenario. The engine stays reusable."

### Fallback

Copy `act-3-audience-council/fallback/config/` and `fallback/data/` into the fresh template. The
tested fallback is **The Whitespace Jury**. It debates tabs, spaces, or an automatic formatter.

Use the recorded fallback video if the fresh repository or Scenario Architect stalls.

## Wrap

Use these points:

* Choose the model for the task.
* Keep knowledge separate from tools.
* Use exact tool allow-lists.
* Make live and cached evidence visible.
* Keep the human decision owner.

Links:

* Copilocal: `garylumsden/copilocal`
* Agent Council template: `garylumsden/azure-agent-council-template`
* This repository: `garylumsden/ai-for-developers-glasgow`

## Pre-flight checklists

### T-24h

**Act 1**

- [ ] Confirm Copilocal 0.1.4 or the pinned replacement.
- [ ] Confirm `ibm/granite-4-h-tiny` is downloaded and loads with 131,072 context tokens.
- [ ] Run three clean local rehearsals and one cloud rehearsal.
- [ ] Record local and cloud fallback videos.

**Act 2**

- [ ] Run the clean solution build.
- [ ] Confirm the Azure resources and model deployments are healthy.
- [ ] Upload the curated knowledge pack.
- [ ] Confirm Foundry IQ reports `usable`.
- [ ] Run the `council-tools` self-test.
- [ ] Record the fallback debate video.

**Act 3**

- [ ] Confirm `gh` can create a private repository under the selected owner.
- [ ] Confirm the template is reachable.
- [ ] Confirm the retained rehearsal repository is still private.
- [ ] Review the question card and fallback copy commands.

**General**

- [ ] Run the secret scan.
- [ ] Keep the repository private until the final scan passes.
- [ ] Make the repository public before the talk.

### T-1h

**Act 1**

- [ ] Confirm Copilot CLI is signed in.
- [ ] Run `prepare`.
- [ ] Confirm the local provider responds.

**Act 2**

- [ ] Start `council-tools`.
- [ ] Confirm health reports `telemetryConfigured: true`.
- [ ] Start the council on Local MAF with `Fast`.
- [ ] Confirm the grounding probe reports `usable`.
- [ ] Confirm GitHub MCP is present or intentionally skipped.
- [ ] Confirm content-safety thresholds remain `Medium`.
- [ ] Delete the configured rehearsal Assessments, Nexuses, and deliberations with `reset-all`.

**Act 3**

- [ ] Confirm the fresh repository name is free.
- [ ] Keep the Act 2 `.env` path ready for copy.

**General**

- [ ] Set terminal and browser fonts for the back row.
- [ ] Open the fallback videos.

### T-10min

**Act 1**

- [ ] Confirm the cable or network control is reachable.
- [ ] Confirm `runs/local/` and `runs/cloud/` are pristine.
- [ ] Keep the audience vote commands ready.

**Act 2**

- [ ] Confirm `council-tools` health.
- [ ] Confirm the web app home page.
- [ ] Confirm the Act 1 dossier markers contain the current evidence.

**Act 3**

- [ ] Open the question card.
- [ ] Open the starter prompt.
- [ ] Keep the fallback folder visible.

**General**

- [ ] Close notifications.
- [ ] Keep the first terminal command ready.

## Open presenter risks

* The presenter must make the repository public after the final secret scan.
* A live physical network-disconnect rehearsal remains a presenter action.
* GitHub and Azure service availability can affect live runs. Use the recorded fallbacks.
* The optional hosted `council-tools` endpoint is documented, not built. Foundry Prompt Agents skip
  localhost. Use Local MAF for the stage path.

## Rehearsal cleanup targeting

`scripts/reset-all.*` deletes individual rehearsal records. It preserves Cosmos containers, vector
indexes, dossiers, and blobs. Before a new rehearsal, add its exact dossier title or ID to
`act-2-council/config/rehearsal-data.json`.
