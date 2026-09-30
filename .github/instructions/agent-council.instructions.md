---
applyTo: 'act-2-council/**'
---
# Copilot Instructions — The Glasgow Developer Council

This repository contains **The Glasgow Developer Council**, a live multi-agent deliberation demo for
the Glasgow Azure User Group. It runs on **Microsoft Foundry** and Azure. The council reviews a
software-team **Proposal** about developer tooling or engineering practice. It returns **approve**,
**approve with conditions**, or **reject**, with reasons.

The framework remains scenario-neutral. The Glasgow scenario exists only in configuration and prompt
files.

## Golden rules

1. **The Glasgow scenario lives in config, not code.** Branding, grounding domains, and the council
   composition come from `act-2-council/config/scenario.json`. Persona prompts come from
   `act-2-council/config/prompts/*.md`.
   Never hard-code the scenario, a persona, the organisation, or a domain into the C#/Razor.
2. **Keep the engine scenario-agnostic.** Do not reintroduce a specific customer/sector into
   `act-2-council/src/**`. Member rosters, ids, and counts are taken from the active scenario at runtime
   (`CouncilMembers` projects `Scenario.Current`). The Chair structured-output schema
   (`DebateSchemas`) is built from the live roster — keep it that way.
3. **Identity-based auth, zero keys.** The local web process uses the signed-in Microsoft Entra user
   through `DefaultAzureCredential` for Foundry, Cosmos, Blob, Search, and Azure control-plane calls.
   The user is the same deploying principal that receives RBAC roles from `azd up`. The Foundry project
   managed identity is separate and is used only for Foundry service-to-service access. Keep
   `disableLocalAuth: true` on AI Services. No keys, connection strings, or secrets in code.
4. **100% Infrastructure as Code.** All Azure resources via Bicep (`act-2-council/infra/`), provisioned
   with `azd` from `act-2-council/`.
   Verify resource schemas and use the latest stable API versions.
5. **.NET 10.** C# 13 idioms — records, primary constructors, file-scoped namespaces, async throughout
   (no `.Result`/`.Wait()`). DI via `Microsoft.Extensions.DependencyInjection`.
6. **Runs locally only.** Run the Blazor Server app with
   `dotnet run --project act-2-council/src/GovernanceCouncil.Web`. It reads
   `act-2-council/config/scenario.json` at startup.
   Keep the loopback-only request guard. Do not add a container, hosted-agent, App Service, tunnel,
   reverse-proxy, port-forward, or remote-server path. `ALLOW_REMOTE_ACCESS` is troubleshooting-only
   and does not add authentication.
7. **Keep docs in sync.** Update `act-2-council/README.md`, `act-2-council/ARCHITECTURE.md`,
   `act-2-council/SPEC.md`, and this file when the
   framework, config model, model deployments, or key technical decisions change.

## Scenario configuration model

- `act-2-council/config/scenario.json` → `ScenarioConfig` (Core): `branding`, `groundingDomains`,
  `mcpServers`, `mcpPolicy`, and `council.{chair,moderator,nexusAnalyst,members[]}`. Loaded once at
  startup by `Scenario.Initialise()`.
  The active file configures the Glasgow Azure User Group branding and council.
- `PersonaConfig`: `id` (lowercase kebab), `name`, `role`, `description`, `tier`
  (`Reasoning`/`Synthesis`/`Fast`), `knowledgeDomains?`, `promptFile`, and optional exact
  `mcpTools` allow-lists. The Moderator and Fast bid client never receive MCP tools.
- Prompts: `act-2-council/config/prompts/<promptFile>`. Framework roles (chair/moderator/nexus-analyst) fall back to
  embedded scenario-neutral defaults; members without a file get a minimal identity prompt.
- Branding: `Brand` (Web) projects `Scenario.Current.Branding`; emblem is an emoji or a path under
  `act-2-council/src/GovernanceCouncil.Web/wwwroot/branding/`. Theme tokens live in
  `act-2-council/src/GovernanceCouncil.Web/wwwroot/app.css` — shared by default.

## Glasgow scenario

Branding:

- Organisation: `Glasgow Azure User Group`
- App name: `The Glasgow Developer Council`
- Tagline: `Tech decisions, deliberated.`
- Emblem: `🏛️`

Framework roles:

| ID | Name | Role |
|---|---|---|
| `chair` | The Convener | Fair, a touch theatrical; delivers a clear verdict with reasons |
| `moderator` | The Referee | Brisk; cuts off waffle; keeps rounds short |
| `nexus-analyst` | The Archivist | Connects today's ruling to earlier ones |

Debating members:

| ID | Name | Role | Knowledge domains |
|---|---|---|---|
| `vibe-coder` | The Vibe Coder | Developer velocity | `docs.github.com`, `github.blog` |
| `dr-no` | Dr No | Security and data protection | `ncsc.gov.uk`, `ico.org.uk`, `learn.microsoft.com` |
| `bean-counter` | The Bean Counter | FinOps | `azure.microsoft.com`, `learn.microsoft.com` |
| `greybeard` | The Greybeard | Principal engineer | `learn.microsoft.com` |

The scenario default grounding domain is `learn.microsoft.com`. Per-member domains override it.
Member prompts must keep these house rules:

- Put response length requirements in each phase's user message, not in persona system prompts.
- Initial positions remain full assessments. Round prompts request one paragraph of at most three sentences.
- Preserve returned round replies regardless of their length or format.
- Quote the Proposal.
- Use the grounding tool if available and cite sources.
- Label an uncited tool result plainly.
- Use a tool only when it changes the argument.
- Challenge other members by name.
- Keep the debate sharp and good-humoured, with a light Scottish flavour.
- Never be mean about real people, companies, or places.

## Architecture (framework)

- **Runtimes** behind `ICouncilRuntime`: **Foundry Agents** (one tooled Prompt Agent per member,
  provisioned via `AgentProvisioner`) and **Local MAF Agents** (`MafCouncilRuntime`, Microsoft Agent
  Framework over Foundry chat models, in-process). Switchable at runtime.
- **Models**: per-tier deployments selected by `CouncilModels` profiles (`Frontier`/`Balanced`/
  `Fast`/`Grok`). Per-tier reasoning effort via `ReasoningEffortChatClient` (MAF) / agent
  `reasoning:{effort}` (Foundry). Sampling left at model defaults (no custom temperature).
- **Grounding**: `GroundingTools` (MAF) / MCP tool (Foundry), scoped to the member's effective
  domains (`CouncilMembers.DomainsFor`); empty ⇒ ungrounded. Providers: Web IQ / Foundry IQ.
- **Read-only MCP**: `McpToolProvider` connects Local MAF to configured HTTP servers, filters exact
  allow-lists, applies per-turn budgets and timeouts, and uses committed snapshots for
  `council-tools` failures. Foundry provisioning attaches only reachable unauthenticated endpoints
  and logs each skipped localhost or unsupported-auth server.
- **Storage**: Cosmos DB (assessments, nexuses, dossiers, deliberations) + Blob (dossier markdown),
  identity-based. Nexus Top-K uses **Cosmos NoSQL vector search** over a persisted `/embedding`.
- **UI**: Blazor Server with shared task-first page headers, toolbars, and navigation.
  The live debate offers **Live desk** and **Focus stage**, with initial-position cards above a
  chronological chat and a moderator event timeline.
  Preserve chat history when switching layouts. Follow new messages only while the reader follows live.
  Show the selected member's avatar and bouncing typing dots while its complete response is pending.
  Do not imply token streaming. Respect reduced motion and the animation pause control.
  Show raised hands on avatars. Make supplied reasons available on hover, focus, and member inspection.
  Keep `📚` citations distinct from `🛠️` tool activity. Persist recorded tool calls on the Assessment.
  Initial assessments retain their full text. Debate prompts request the `{summary, detail}` text-field contract.
  Set phase-specific response length requirements in user messages, never persona system prompts.
  Extract returned reply text without length or format validation. Preserve paragraphs, lists, and plain text.
  Do not retry or discard a returned reply because of its length or format.
  Keep content-safety blocks and explicit notices for empty model responses.
  Keep the Chair's structured synthesis unchanged.
  Record calls for hands, bids, selections, and supplied selection reasons from SignalR events;
  never invent moderator reasoning.
  Timeline receipt times are local. Entries last only while the view is open.
  Rejoin the debate group after reconnecting and disclose gaps; the hub does not replay events.

## Dependency pins (do not drift)

`Microsoft.Agents.AI.Foundry 1.5.0` needs `OpenAI 2.10.0` (a ctor removed in 2.11.0 breaks the Foundry
bridge). `OpenAI 2.10.0` + `Microsoft.Extensions.AI.OpenAI 10.6.0` are pinned in
`GovernanceCouncil.Agents.csproj` — keep them.

## Domain terminology

| Term | Meaning |
|---|---|
| **Dossier** | A source document submitted for deliberation. |
| **Deliberation** | One council session reviewing a dossier. |
| **Assessment** | The structured output: recommendation, summary, votes, conditions, dissent, risks. |
| **Nexus** | A discovered interconnection between two assessments. |
| **Council Member** | A debating persona (an agent). |
| **Chair / Moderator / Nexus Analyst** | Framework roles: synthesise / route / link. |

Nexus types: `Implication` · `Contradiction` · `Dependency` · `Supersession` · `Reinforcement` ·
`Tension`.

## Build & verify

```pwsh
dotnet build act-2-council/src/GovernanceCouncil.Web/GovernanceCouncil.Web.csproj -v minimal   # 0 errors
cd act-2-council/infra; az bicep build --file main.bicep                                       # exit 0
```
