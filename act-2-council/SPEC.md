# SPEC — Agent Council (template)

## Purpose

A reusable template for a **multi-agent deliberation** demo on Microsoft Foundry + Azure. A council of
expert agents reviews a document and produces a structured, defensible decision, then discovers how
decisions interconnect over time. The template is **scenario-neutral**; a concrete demo is produced by
**configuration** (the Scenario Architect agent), not by editing the engine.

## Functional requirements

### Configuration (scenario)
- The scenario (branding, grounding domains, council composition) is read from `config/scenario.json`
  at startup. Absent ⇒ neutral defaults (generic branding, empty council).
- Persona system prompts are read from `config/prompts/*.md` on disk (no rebuild to change a persona).
- A guided **Scenario Architect** Copilot agent (`.github/agents/`) interviews the user and generates
  the config, prompts, branding, optional sample dossiers, and refreshed docs.

### Deliberation
- A user ingests a **Dossier** (Markdown) into the Dossier Library and submits it for **Deliberation**.
- The council runs a **live debate**: independent initial positions → moderated multi-round debate
  (hand-raise bids, speaker selection) → **Chair** synthesis.
- The Chair produces a structured **Assessment**: overall recommendation, chair summary, per-member
  deliberation summary + votes, conditions, dissent, and risks. Member set is taken from the active
  scenario roster; a `responded` flag prevents fabrication for silent members.

### Nexus
- After each assessment, the **Nexus Analyst** discovers **Nexuses** (interconnections) against prior
  assessments, classified as Implication / Contradiction / Dependency / Supersession / Reinforcement /
  Tension, with evidencing excerpts and a confidence level.
- Candidate retrieval is efficient: each assessment's summary embedding is computed once and persisted;
  Top-K nearest priors are found via **Cosmos DB NoSQL vector search** (no corpus re-embedding).

### Runtimes & models
- Two interchangeable runtimes (`ICouncilRuntime`): **Foundry Agents** (provisioned Prompt Agents) and
  **Local MAF Agents** (Microsoft Agent Framework over Foundry chat models, in-process).
- Per-tier model selection via `CouncilModels` profiles: `Frontier` / `Balanced` / `Fast` / `Grok`.
- Per-tier reasoning effort (GPT-5/o-series): minimal (bids) / low (members) / medium (synthesis).
- Grounding (Web IQ / Foundry IQ), scoped to the scenario's authoritative domains; empty ⇒ ungrounded.
- Optional MCP servers and exact per-member tool allow-lists. Absent MCP configuration preserves the
  original grounded-only behavior.
- Local MAF connects to HTTP MCP servers directly. Foundry Prompt Agents use only a reachable
  `foundryUrl`; localhost and unsupported authentication paths are skipped with a warning.
- Local MAF MCP calls use an enforced per-member, per-turn cap and timeout. The local
  `council-tools` path uses committed snapshots when a live call fails. Foundry Prompt Agents receive
  the same cap as an instruction only until a hosted boundary can enforce it.

### UI
- Blazor Server app with SignalR live updates: dashboard, Dossier Library, Assessment detail, Nexus
  Explorer (graph) + Alerts, and the live debate chamber. Branding (name, org, tagline, emblem) is
  config-driven; the dark theme is shared by default.
- Use a consistent navigation shell, page headers, toolbars, and empty states across all pages.
- The live debate offers **Live desk** and **Focus stage**. Switching layouts does not restart the
  debate or discard responses.
- Place initial-position cards above a chronological chat. Keep full initial assessments accessible.
- Show raised hands on member avatars. Expose the supplied reason on hover, focus, and member inspection.
- Show the known selected speaker's avatar with bouncing typing dots until its complete reply arrives.
  Do not add token streaming. Support reduced motion and an animation pause control.
- Put response length requirements in phase-specific user messages, not persona system prompts.
- Request one paragraph of at most three sentences in debate prompts.
- Display returned debate replies regardless of their length or format. Preserve paragraphs and Markdown.
  Do not retry or discard a returned reply for these reasons. Show an explicit notice for an empty response.
  Keep content-safety blocks unchanged.
- Keep the Chair's structured synthesis and response length unchanged.
- Retry an invalid initial assessment once to repair its format. Show any remaining failure on the member's card.
- Keep the moderator announcement outside the chat scroll area.
  Readers can inspect earlier messages and return to the latest message.
- Follow the latest message by default. Pause following only after manual chat scrolling, not after opening an inspector.
- Show moderator activity chronologically: calls for hands, member bids, speaker selections, and
  the supplied selection reasons. Show the local receipt time and round.
- The moderator timeline contains events received while the view is open. Reloading clears it.
  Reconnecting rejoins the debate group but does not replay missed events.
- Keep dossier and member details within the debate workspace. Preserve initial positions,
  content-safety notices, citations, Assessment links, and Nexus status.
- Render knowledge citations as `📚` evidence and recorded tool calls as distinct `🛠️` message badges.
  Include grounding and MCP calls, with source, summary, and expandable JSON.
  Preserve `🔧` tool evidence on the Assessment.

## Non-functional requirements

- **Identity-based auth, zero keys** — the local server process uses the signed-in Microsoft Entra
  user through `DefaultAzureCredential` for Foundry and all Azure data/control-plane calls. The same
  user runs `azd up` and receives the required RBAC roles. The Foundry project managed identity is a
  separate service-to-service principal. Keep `disableLocalAuth: true`; do not add keys.
- **100% IaC** — all Azure resources via Bicep / `azd up`.
- **Local-only runtime** — run `dotnet run --project src/GovernanceCouncil.Web`. The app accepts
  loopback requests only. It has no container, hosted-agent, App Service, tunnel, proxy, port-forward,
  remote-server, or multi-user path. `ALLOW_REMOTE_ACCESS` is for troubleshooting only and adds no
  authentication.
- **.NET 10**, C# 13 idioms, async throughout.
- **Dependency pins** — `OpenAI 2.10.0` + `Microsoft.Extensions.AI.OpenAI 10.6.0` (Foundry bridge).

## Configuration contract

`ScenarioConfig` (`src/GovernanceCouncil.Core/Models/ScenarioConfig.cs`):

| Field | Notes |
|---|---|
| `branding.organisation` | Optional eyebrow. |
| `branding.appName` | App + nav title. |
| `branding.tagline` | One line under the title. |
| `branding.emblem` | Emoji, or a path under `wwwroot/branding/` (e.g. `branding/logo.svg`). |
| `groundingDomains` | Default authoritative hostnames. `[]` ⇒ ungrounded. |
| `mcpServers[]` | Optional HTTP servers: `id`, `url`, `foundryUrl?`, `auth`, `tokenEnv?`, `optional?`. |
| `mcpPolicy.maxCallsPerTurn` | MCP call cap. Default `2`; range `1` to `10`. |
| `mcpPolicy.timeoutSeconds` | Per-call timeout. Default `8`; range `1` to `60`. |
| `council.chair/moderator/nexusAnalyst` | Framework roles (built-in neutral default prompts). |
| `council.members[]` | Debating personas plus optional `mcpTools[]` entries with `server` and a non-empty exact `allow` list. |

`tier ∈ { Reasoning, Synthesis, Fast }`. See `config/scenario.example.json` for a complete example.
MCP wildcard allow-lists, unknown servers, and known-server tool names fail startup validation. The
Moderator and Fast bid client never receive MCP tools.
`auth` is `none` or `env-token`. Authenticated servers require HTTPS. Plain HTTP is allowed only for
an unauthenticated loopback server. `entra` is rejected until its audience and token flow are
implemented.

## Out of scope (by design)

- No container, hosted-agent, App Service, tunnel, reverse-proxy, port-forward, remote-server, or
  multi-user deployment. The browser UI has no sign-in because the application is loopback-only.
- No application identity. The local process uses the signed-in developer's Microsoft Entra user
  identity through `DefaultAzureCredential`.
- No automated test suite (the framework is validated by build + manual runs).
- Framework vocabulary (Dossier / Deliberation / Assessment / Nexus / Council) is fixed in code; use
  your own words in prompts and sample documents.
