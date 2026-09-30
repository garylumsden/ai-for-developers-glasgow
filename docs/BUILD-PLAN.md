# Build Plan

## Session

* Repository: `garylumsden/ai-for-developers-glasgow`
* Template: `garylumsden/azure-agent-council-template`
* Template commit: `90ed5195f1e73e418ecdc5d52c7a3a80f65ed6c3`
* Branch: `build/full-demo`
* Local model candidate: LM Studio `ibm/granite-4-h-tiny`
* Cloud model: `gpt-5.4`

## Decisions

* Keep the repository private until the final secret scan passes.
* Run the Scenario Architect before moving the council.
* Select the final Act 1 task only after a timed local model benchmark.
* Use LM Studio `ibm/granite-4-h-tiny` because no current NPU catalogue variant meets Copilot's context gate.
* Use the Local MAF runtime as the stage path.
* Document the optional Container Apps MCP design. Do not deploy it unless the local path cannot meet the brief.
* Use public package feeds for package operations.

## Planned Work

1. Preserve the build brief and verify the untouched template.
2. Generate the Glasgow Developer Council with the Scenario Architect.
3. Move the configured application under `act-2-council/`.
4. Build and rehearse Act 1.
5. Add knowledge sources and the `council-tools` MCP server.
6. Add the opt-in runtime and UI integration.
7. Prepare and rehearse Act 3.
8. Write the presenter pack and reset workflow.
9. Validate, review, merge to `main`, and push.

## Build Log

| Date | Phase | Check or decision | Result |
|------|-------|-------------------|--------|
| 2026-09-24 | Phase 0 | Repository created from the template | Passed |
| 2026-09-24 | Phase 0 | `build/full-demo` created and pushed | Passed |
| 2026-09-24 | Phase 0 | Required template files verified | Passed |
| 2026-09-24 | Phase 0 | Untouched web project build | Passed with 0 warnings and 0 errors in 9.65 seconds |
| 2026-09-24 | Phase 1 | Scenario Architect separate Copilot process | Passed in 3 minutes 2 seconds |
| 2026-09-24 | Phase 1 | Appendix A prompt copy | Passed exact line comparison |
| 2026-09-24 | Phase 1 | Roster, branding, domains, prompts, dossiers, and allowed paths | Passed |
| 2026-09-24 | Phase 1 | Configured council build | Passed with 0 warnings and 0 errors in 3.13 seconds |
| 2026-09-24 | Phase 2 | Git relocation and root path repair | Passed |
| 2026-09-24 | Phase 2 | Relocated web project build | Passed with 0 warnings and 0 errors |
| 2026-09-24 | Phase 2 | Scenario resolution from the repository root and `act-2-council/` | Passed |
| 2026-09-24 | Phase 2 | Bicep and `azure.yaml` validation | Passed |
| 2026-09-24 | Phase 2 | Azure deployment preview | Passed |
| 2026-09-24 | Phase 2 | Azure provision | Passed after an Azure AI Search capacity fallback |
| 2026-09-24 | Phase 2 | Web startup from both launch locations | Passed with HTTP 200 and Glasgow branding |
| 2026-09-24 | Act 1 model search | Foundry Local NPU catalogue | No eligible model: all tool-capable NPU variants had 4,224 context tokens |
| 2026-09-24 | Act 1 model search | Foundry Local update check | 0.10.3 was current |
| 2026-09-24 | Act 1 model profile | Granite direct generation | 2.88 to 4.66 seconds for 143 output tokens |
| 2026-09-24 | Act 1 model profile | Granite native tool call | Passed |
| 2026-09-24 | Act 1 model profile | Copilot file and shell tool probe | Passed in 222.72 seconds |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-1` | Timed out at 300.24 seconds because configured MCP servers retried while isolated |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-1b` | Failed in 74.42 seconds; returned code without editing |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-1c` | Failed in 71.56 seconds; left the starter throw |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-1d` | Failed in 75.21 seconds; created nested methods |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-1e` | Failed in 79.28 seconds; malformed a return edit |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-html-1` | Passed in 75.30 seconds after switching to one-file creation |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-html-2` | Failed in 70.61 seconds; strict style-block check rejected valid inline CSS |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-html-2b` | Passed in 71.14 seconds |
| 2026-09-24 | Act 1 attempt | `local-rehearsal-html-3` | Failed in 74.99 seconds; optional font-size detail was unreliable |
| 2026-09-24 | Act 1 final | `local-final-1` | Passed in 155.36 seconds |
| 2026-09-24 | Act 1 final | `local-final-2` | Passed in 92.74 seconds |
| 2026-09-24 | Act 1 final | `local-final-3` | Passed in 81.71 seconds |
| 2026-09-24 | Act 1 final | `cloud-final-1` | Passed in 56.76 seconds |
| 2026-09-24 | Act 2 knowledge | Curated pack upload | Passed idempotently with 3 files |
| 2026-09-24 | Act 2 knowledge | Blob knowledge-source ingestion | Passed with 3 processed and 0 failed items |
| 2026-09-24 | Act 2 knowledge | Foundry IQ readiness probe | Passed as usable in 6.50 seconds |
| 2026-09-24 | Act 2 tools | `council-tools` build | Passed with 0 warnings and 0 errors |
| 2026-09-24 | Act 2 tools | Live and cached self-test | Passed |
| 2026-09-24 | Act 2 tools | Health and MCP client smoke test | Passed; telemetry configured and 5 tools listed and invoked |
| 2026-09-24 | Act 2 tools | Dependency vulnerability audit | Passed with no vulnerable packages |
| 2026-09-24 | Act 2 runtime | Scenario MCP validation | Passed; no-MCP behavior preserved and wildcard rejected |
| 2026-09-24 | Act 2 runtime | Local MAF server discovery | Passed with 5 tools and exact persona allow-lists |
| 2026-09-24 | Act 2 runtime | Optional GitHub MCP without token | Skipped with a clear log |
| 2026-09-24 | Act 2 runtime | Foundry Prompt Agent integration | Agents provisioned; localhost and env-token MCP paths skipped clearly |
| 2026-09-24 | Act 2 rehearsal | Local MAF full dossier | Passed in about 226 seconds |
| 2026-09-24 | Act 2 rehearsal | Tool restrictions | Every member used at least one allowed tool; only exact allow-listed tools were exposed |
| 2026-09-24 | Act 2 rehearsal | Cached fallback | Passed; cached sources were visible and the debate completed |
| 2026-09-24 | Act 2 UI | Assessment tool evidence | Passed with 8 records grouped under all 4 members |
| 2026-09-24 | Act 2 telemetry | Application Insights query | Passed with member-tagged MCP spans |
| 2026-09-24 | Act 3 rehearsal | Create and clone private template repo | Passed in 13.82 seconds |
| 2026-09-24 | Act 3 rehearsal | Copy shared `.env` and fallback | Passed in 0.14 seconds; `.env` remained ignored |
| 2026-09-24 | Act 3 rehearsal | Fresh template fallback build | Passed with 0 warnings and 0 errors in 5.28 seconds |
| 2026-09-24 | Act 3 rehearsal | Local MAF startup | Passed in about 13 seconds |
| 2026-09-24 | Act 3 rehearsal | Fallback dossier deliberation | Passed in about 155 seconds |
| 2026-09-24 | Presenter pack | Presenter guide | Completed with measured commands, timings, talk tracks, and fallbacks |
| 2026-09-24 | Presenter pack | Readiness prompt | Completed with green, amber, and red status contract |
| 2026-09-24 | Reset | PowerShell and Bash what-if | Passed without data changes |
| 2026-09-24 | Reset | Scoped council purge preview | Passed; targeted assessments, nexuses, and deliberations only |
| 2026-09-24 | Reset | Dossier evidence clear | Passed while preserving markers and later sections |
| 2026-09-24 | Final validation | Release solution and Act 1 builds | Passed with 0 warnings and 0 errors |
| 2026-09-24 | Final validation | Bicep and azd deployment preview | Passed |
| 2026-09-24 | Final validation | Release Local MAF and Foundry runtime health | Passed |
| 2026-09-24 | Final validation | Approved reset | Passed; rehearsal records purged and workspaces pristine |
| 2026-09-24 | Security | Gitleaks full history and staged scans | Passed with no leaks |
| 2026-09-24 | Security | Vulnerable and deprecated dependencies | Passed with none reported |
| 2026-09-24 | Security | GitHub Action pinning | Passed; full commit SHAs |
| 2026-09-24 | Final Review | Container-destroying reset | Fixed with targeted document deletion; vector policy restored and preserved |
| 2026-09-24 | Final Review | Cosmos CLI exit handling | Fixed with explicit exit-code checks |
| 2026-09-24 | Final Review | Snapshot argument mismatch | Fixed and covered by an outage mismatch test |
| 2026-09-24 | Final Review | Failed-live fallback selection | Fixed with explicit `--sample` and `--results` options |
| 2026-09-24 | Final Review | MCP authentication | Authenticated HTTP and unimplemented Entra modes now fail startup |
| 2026-09-24 | Final Review | Foundry call cap and content-filter docs | Clarified and corrected |
| 2026-09-24 | Final Review | Correction matrix | Passed; final parent outcome Complete |

## Files Added or Changed

* `.github/prompts/build-demo.prompt.md`
* `.github/prompts/glasgow-council-scenario.prompt.md`
* `.github/instructions/agent-council.instructions.md`
* `BUILD-PLAN.md`
* `README.md`
* `act-2-council/config/scenario.json`
* `act-2-council/config/prompts/bean-counter.md`
* `act-2-council/config/prompts/dr-no.md`
* `act-2-council/config/prompts/greybeard.md`
* `act-2-council/config/prompts/vibe-coder.md`
* `act-2-council/data/policies/rehearsal-irn-bru-debugging.md`
* `act-2-council/data/policies/run-coding-agents-on-local-models.md`
* `.github/prompts/act1-choose-task.prompt.md`
* `act-1-pull-the-plug/README.md`
* `act-1-pull-the-plug/TASK.md`
* `act-1-pull-the-plug/task.json`
* `act-1-pull-the-plug/starter/brief.txt`
* `act-1-pull-the-plug/harness/Act1Harness.csproj`
* `act-1-pull-the-plug/harness/Program.cs`
* `act-1-pull-the-plug/prepare.ps1`
* `act-1-pull-the-plug/prepare.sh`
* `act-1-pull-the-plug/check.ps1`
* `act-1-pull-the-plug/check.sh`
* `act-1-pull-the-plug/run-local.ps1`
* `act-1-pull-the-plug/run-local.sh`
* `act-1-pull-the-plug/run-cloud.ps1`
* `act-1-pull-the-plug/run-cloud.sh`
* `act-1-pull-the-plug/record-vote.ps1`
* `act-1-pull-the-plug/record-vote.sh`
* `act-1-pull-the-plug/show-results.ps1`
* `act-1-pull-the-plug/show-results.sh`
* `act-1-pull-the-plug/results/results.sample.json`
* `act-2-council/scripts/act1-to-dossier.ps1`
* `act-2-council/scripts/act1-to-dossier.sh`
* `act-2-council/config/github-mcp.json`
* `act-2-council/config/model-register.json`
* `act-2-council/config/tools.json`
* `act-2-council/data/knowledge/ai-usage-policy-sample.md`
* `act-2-council/data/knowledge/engineering-principles.md`
* `act-2-council/data/knowledge/local-vs-cloud-models-primer.md`
* `act-2-council/scripts/load-knowledge.ps1`
* `act-2-council/scripts/load-knowledge.sh`
* `act-2-council/src/CouncilTools.Mcp/`
* `act-2-council/src/GovernanceCouncil.Agents/Knowledge/GroundingReadinessProbe.cs`
* `act-2-council/src/GovernanceCouncil.Core/Models/ScenarioConfigValidator.cs`
* `act-2-council/src/GovernanceCouncil.Core/Models/ToolCallRecord.cs`
* `act-2-council/src/GovernanceCouncil.Agents/Runtime/McpToolProvider.cs`
* `act-3-audience-council/README.md`
* `act-3-audience-council/QUESTION-CARD.md`
* `act-3-audience-council/STARTER-PROMPT.md`
* `act-3-audience-council/fallback/`
* `act-3-audience-council/rehearsal-repositories.json`
* `docs/PRESENTER-GUIDE.md`
* `.github/prompts/demo-readiness-check.prompt.md`
* `scripts/reset-all.ps1`
* `scripts/reset-all.sh`
* `.gitleaks.toml`
* `act-2-council/config/rehearsal-data.json`
* `act-2-council/src/CouncilMaintenance/`

## Phase 2 Inventory

* `act-2-council/azure.yaml` keeps `infra` and post-provision hook paths relative to the council folder.
* The post-provision hooks use the azd working directory and write `act-2-council/src/GovernanceCouncil.Web/.env`.
* `act-2-council/infra/main.parameters.json` reads azd environment values and keeps the content-safety defaults at `Medium`.
* `.github/workflows/ci.yml` now runs build, package audit, and Bicep commands from `act-2-council/`.
* `.github/agents/scenario-architect.agent.md` remains at the root and writes only council configuration, branding, dossiers, and council documentation.
* `.github/instructions/agent-council.instructions.md` remains at the root and applies to `act-2-council/**`.
* `act-2-council/src/GovernanceCouncil.slnx` retains project paths relative to its location.
* Scenario resolution remains `COUNCIL_SCENARIO_PATH` first, then the nearest `config/scenario.json`.
* Azure AI Search uses the main deployment location by default. A local `AZURE_SEARCH_LOCATION` override handles regional SKU capacity limits.
* The successful provision generated the ignored `act-2-council/src/GovernanceCouncil.Web/.env`.
* The committed documentation does not contain the selected deployment locations or live Azure identifiers.

## Act 1 implementation notes

* Local and cloud can run concurrently after one `prepare`. Result and vote updates are locked and saved through atomic file replacement.
* Run scripts reuse the prepared harness build to avoid concurrent builds. Each completed run opens its output page unless `--no-open` is set.
* Readable terminal text is the default. `--json` is an optional event format; usage statistics stay in a separate JSON file.
* A parallel rehearsal needs a connected network. The physical unplug demonstration remains sequential.
* Run `dotnet run --project act-1-pull-the-plug\tests\Act1Harness.Tests.csproj` to test result and vote persistence with real concurrent processes.
* On 30 September 2026, completion changed to successful execution within the time box and a non-empty `index.html`.
* Content, capitalisation, and HTML structure no longer determine completion. The audience judges quality.
* The shared prompt and brief now permit the model to choose its structure and design. Historical rehearsal times remain unchanged.
* Run `act-1-pull-the-plug/tests/check-output.ps1` to check completion handling without changing live results or generated pages.
* The harness disables every configured MCP server for the comparison run. This avoids offline retry delays and keeps the model focused on file and shell tools.
* The local child process receives an unreachable loopback proxy. `NO_PROXY` permits only the LM Studio loopback endpoint.
* The first source-edit task was replaced after repeated malformed patches. The final one-file creation task matches Granite's measured capability.
* `act-2-council/data/policies/run-coding-agents-on-local-models.md` was hand-corrected to restore its required Risks and Decision requested sections. The Scenario Architect output had ended at the Evidence markers.
* The curated notes use a dedicated Blob knowledge source. Search uses managed identity and has Storage Blob Data Reader.
* `council-tools` reads the existing ignored `.env` file without overwriting host environment variables, so it reuses the Application Insights connection.
* Foundry Agents do not execute localhost MCP tools. The documented optional hosted design uses Container Apps, managed identity, Entra authentication, and a default-off azd parameter.

## Incomplete or Blocked Work

* None.
