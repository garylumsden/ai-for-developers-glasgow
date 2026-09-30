# The Glasgow Developer Council

**The Glasgow Developer Council** is a live multi-agent deliberation demo for the Glasgow Azure User
Group. It runs on **Microsoft Foundry** and Azure. Four debating members review a software-team
**Proposal**, challenge each other, and recommend one decision: **approve**, **approve with
conditions**, or **reject**, with reasons.

The council focuses on developer tooling and engineering choices. **The Convener** delivers the
verdict, **The Referee** keeps the debate brief, and **The Archivist** connects the ruling to earlier
assessments.

---

## Quickstart

### 1. Sign in and provision Azure (Bicep via `azd`)

Use the same Microsoft Entra user for provisioning and for running the app. The local process uses
`DefaultAzureCredential`, which discovers this signed-in user through the Azure CLI. The user must
have the RBAC roles that `azd up` assigns to the deploying principal.

```bash
az login
azd auth login

# (Optional) supply a Microsoft Web IQ key so the Web IQ grounding tool is provisioned.
# Web IQ is limited-access (preview) — request a key from the Web IQ team. Skip this to run
# ungrounded, or switch grounding to Foundry IQ in the UI.
azd env set WEBIQ_API_KEY <your-web-iq-key>

azd up
```

Provisions Foundry (AI Services + project), model deployments, Cosmos DB, Blob Storage, AI Search, and
App Insights — all **identity-based (zero keys)**. The Web IQ key (if set) flows into Bicep
(`main.parameters.json` reads `${WEBIQ_API_KEY}`) → Key Vault + a CustomKeys connection. Post-provision
hooks write a local `.env`; for **local MAF mode** also add `WEBIQ_API_KEY=<key>` to
`src/GovernanceCouncil.Web/.env`. (Keyless alternative: bind the project MI in the Web IQ portal and use
AAD, scope `https://api.microsoft.ai/.default`.)

### 2. Run locally

```bash
dotnet run --project src/GovernanceCouncil.Web
```

The app **runs only on the local workstation** and reads `config/scenario.json` at startup. It is not
a web-hosted, container-hosted, or multi-user service. The server rejects non-loopback requests with
HTTP 403. Do not expose it through a tunnel, reverse proxy, port-forward, or remote host.

The UI does not ask the user to sign in. The local server process authenticates to Foundry, Cosmos DB,
Blob Storage, AI Search, and Azure control-plane APIs as the **signed-in Microsoft Entra user** through
`DefaultAzureCredential`. Run `az login` with the same user that ran `azd up`. No API keys or app
credentials are used. The Foundry project managed identity is separate: Foundry uses it for its own
service-to-service access, but it is not the identity of the local web app.

Upload a Markdown Proposal, choose a deliberation, and watch the council debate.

Two sample Proposals are ready in `data/policies/`:

- `run-coding-agents-on-local-models.md` compares local, cloud, and hybrid coding agents.
- `rehearsal-irn-bru-debugging.md` provides a short, silly rehearsal debate.

> [!IMPORTANT]
> `ALLOW_REMOTE_ACCESS=true` disables the loopback guard for troubleshooting. It does not add
> authentication. Do not set it for normal use.

For Microsoft guidance, see [Foundry tools authentication and authorization using .NET](https://learn.microsoft.com/dotnet/ai/azure-ai-services-authentication).

---

## The configured council

`config/scenario.json` contains the Glasgow branding, roster, grounding domains, MCP servers, and
exact per-member tool allow-lists. The app reads it at startup. Member prompts are plain Markdown
files under `config/prompts/`, so prompt changes need only an app restart.

| Member | Role | 📚 Knowledge | 🔧 Tools |
|---|---|---|---|
| **The Vibe Coder** | Developer velocity | `docs.github.com`, `github.blog` | `get_repo_activity`; optional read-only GitHub MCP |
| **Dr No** | Security and data protection | `ncsc.gov.uk`, `ico.org.uk`, `learn.microsoft.com` | `check_model_register` |
| **The Bean Counter** | FinOps | `azure.microsoft.com`, `learn.microsoft.com` | `get_azure_model_price`, `compare_run_costs` |
| **The Greybeard** | Principal engineer | `learn.microsoft.com` | `get_product_lifecycle` |

The default grounding domain is `learn.microsoft.com`. Each member uses the domain list in the
table. The Chair can use all configured domains. The debate is sharp but good-humoured.
Initial assessments can contain full explanations. Debate prompts request one evidence-led point
in one paragraph of at most three sentences. These requests belong to phase-specific user messages,
not persona system prompts. The engine displays returned replies regardless of their length or format.
It does not retry or discard a reply for these reasons. Content-safety blocks remain unchanged.
Members must quote the Proposal.

The framework roles use their built-in prompts:

| Framework role | Name | Purpose |
|---|---|---|
| Chair | **The Convener** | Delivers a clear verdict and reasons. |
| Moderator | **The Referee** | Cuts off waffle and keeps rounds short. |
| Nexus Analyst | **The Archivist** | Connects the current ruling to earlier rulings. |

---

## How it works

- **Two runtimes** behind one seam (`ICouncilRuntime`), switchable in the UI:
  - **Foundry Agents** (default) — one tooled **Prompt Agent** per member, provisioned in the project.
  - **Local MAF Agents** — Microsoft Agent Framework agents over the Foundry **chat models**,
    in-process; bids & speaker-selection run on a fast model.
- **Model profiles** (`COUNCIL_MODEL_PROFILE`, default `Fast`): `Frontier` / `Balanced` / `Fast` /
  `Grok` — a quality↔speed ladder selecting the model set across tiers.
- **Per-tier reasoning effort** (GPT-5 / o-series, and xAI **grok-4.3**): `minimal`/`none` bids ·
  `low` members · `medium` synthesis. Override with `COUNCIL_REASONING_EFFORT`. (The `Grok` profile
  uses grok-4.3, a single tunable reasoning model that honours `reasoning_effort`; older grok-4.1-fast
  ignores it.)
- **Grounding** (toggle in UI): **Web IQ** or **Foundry IQ**, each scoped to the scenario's
  authoritative domains (the Chair gets them all). No domains ⇒ ungrounded. In MAF mode the grounding
  tool is bounded per turn (3-iteration cap + a hard 2-call search budget) so a tool-eager model can't
  fire dozens of search calls per turn.
- **Read-only MCP tools** use exact per-member allow-lists. Start the local server with
  `dotnet run --project src/CouncilTools.Mcp`. Local MAF attaches the allowed tools. Foundry Agents
  skip localhost until `COUNCIL_TOOLS_PUBLIC_URL` identifies a reachable hosted endpoint.
- **Venue-safe fallbacks**: `council-tools` returns committed snapshots when public APIs fail. The
  optional GitHub server is skipped with a clear log when `GITHUB_MCP_TOKEN` is absent.
- **Visible evidence**: `📚` identifies cited knowledge. A message's `🛠️` badge expands recorded
  tool activity, including grounding and read-only MCP calls. Tool-call JSON is persisted on the Assessment.
- **Nexus retrieval is efficient**: each assessment's summary is embedded once at creation and stored;
  Top-K candidate retrieval uses **Cosmos DB NoSQL vector search** (no corpus re-embedding).
- **Live debate** over SignalR: initial-position cards sit above a chronological chat.
  Select a card to read the full initial position. Raised hands appear on avatars; hover or focus
  shows the supplied reason. Selecting the member also shows its reason.
  The selected speaker shows bouncing typing dots until its complete message arrives.
  Messages do not stream token by token. Pause the animation or use reduced-motion settings.
  Debate prompts request short replies. The app preserves longer replies, multiple paragraphs,
  and other returned formats without format-repair calls. The Chair's output remains unchanged.
  Choose **Live desk** or **Focus stage** without restarting the debate or losing chat history.
  Read earlier messages without automatic scrolling; select **Return to live** to follow new messages.
  A moderator timeline shows calls for hands, bids, speaker selections, and the supplied reasons.
  Entries show the local receipt time and round. They last only while the view remains open;
  reconnecting does not recover missed events.
- **Initial-position recovery**: an invalid initial assessment receives one format-repair attempt.
  A remaining failure shows **Initial position unavailable**, with an explanation in its member details.
- **Consistent app UI**: shared navigation, page headers, action groups, and readable empty states
  across dossiers, assessments, and Nexus pages.
- **Collapsible navigation**: select **Hide menu** above the desktop links to release space for content.
  Select the menu button to restore the links. Narrow windows use the existing **Menu** and **Close** buttons.

See `ARCHITECTURE.md` for the full picture and `docs/DESIGN.md` for the design system.
See [`../docs/PRESENTER-GUIDE.md`](../docs/PRESENTER-GUIDE.md) for the measured stage sequence,
commands, and fallbacks.

## Interactive debate wireframes

The [Debate design lab](docs/wireframes/debate/README.md) preserves the five original wireframe ideas.
The live app now uses chat bubbles and top initial-position cards in **Live desk** and **Focus stage**.
The design lab remains a historical prototype. It uses synthetic data and does not connect to the live app.

```powershell
node docs\wireframes\debate\serve.mjs
```

Open <http://127.0.0.1:4317>.
Switch layouts, advance the simulated debate, inspect raised hands, and compare responses.

Run the package-free regression checks from `act-2-council/`:

```powershell
dotnet run --project tests\CouncilChat.Checks\CouncilChat.Checks.csproj -c Release
```

---

## Project structure

```
config/                     # scenario.json (+ prompts/) — the only scenario-specific config
  scenario.example.json     # a complete, commented example (not loaded)
  prompts/                  # config/prompts/<id>.md — one per persona
data/policies/              # sample Proposals (Markdown)
infra/                      # Bicep IaC (azd) — identity-based, zero keys
src/
  GovernanceCouncil.Core/   # domain models + scenario config (ScenarioConfig, Scenario, CouncilMembers)
  GovernanceCouncil.Data/   # Cosmos + Blob (identity auth)
  GovernanceCouncil.Agents/ # debate engine, runtimes, nexus, provisioning, grounding
  GovernanceCouncil.Web/    # Blazor Server UI + SignalR
```

> The code namespace stays `GovernanceCouncil.*` as the framework's internal name — it is not shown to
> end users (branding is fully configurable).

---

## Customising further

- **Branding / look & feel**: the dark "council chamber" theme lives in `wwwroot/app.css` (CSS custom
  properties). Branding strings + emblem come from `config/scenario.json`; the theme is shared by
  default. Drop an SVG/PNG in `wwwroot/branding/` and point `emblem` at it.
- **Avatars**: every persona shows an avatar in the chamber (in place of initials). Four role defaults
  ship in `wwwroot/branding/avatars/` (chair / moderator / nexus / member); override any persona by
  setting its `avatar` in `config/scenario.json` to a `wwwroot/` path or URL (see
  `wwwroot/branding/README.md`).
- **Vocabulary**: the framework's domain language (Dossier / Deliberation / Assessment / Nexus /
  Council) is fixed in code; use your scenario's own words in prompts and sample documents.

## Content safety (RAI policy)

Every chat model deployment is bound to a custom content-safety policy
(`Microsoft.CognitiveServices/accounts/raiPolicies`, `infra/`). Its thresholds **default to `Medium`
for every harm category — identical to `Microsoft.Default`** — so the repository uses standard,
safe filtering and behaves exactly as the platform default out of the box.

A scenario that produces legitimate content the default filter blocks at `medium` severity (e.g. a
fictional or adversarial debate) can **relax a category per repo with `azd env set` only — no Bicep
edits** — then re-provision:

```bash
azd env set COUNCIL_CONTENT_VIOLENCE_THRESHOLD High   # only block at High; allow Low/Medium
azd provision
```

Overridable env vars (each `Low` | `Medium` | `High`, default `Medium`):
`COUNCIL_CONTENT_HATE_THRESHOLD`, `COUNCIL_CONTENT_SEXUAL_THRESHOLD`,
`COUNCIL_CONTENT_VIOLENCE_THRESHOLD`, `COUNCIL_CONTENT_SELFHARM_THRESHOLD`
(plus `COUNCIL_CONTENT_POLICY_NAME` to rename the policy). A higher threshold blocks **less** (only at
that severity and above). The policy applies to the Foundry agents automatically because they run on
the bound deployments; its name is surfaced to the app as `COUNCIL_RAI_POLICY_NAME`.

Microsoft supports `Low`, `Medium`, and `High` severity thresholds. Limited Access approval applies
to disabling completion filtering or using annotate-only mode, neither of which this repository
uses. See [Configure content filters](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/content-filters).

### Live per-deliberation toggle (single-presenter demo)

The dossier page has a **Content filter — Violence** control (`Unchanged` / `Low` / `Medium` / `High`)
that sets the Violence threshold applied to the **next** deliberation, so you can demo a block (strict)
then a pass (relaxed) on the same dossier without re-provisioning. Leaving it `Unchanged` makes no API
call. Because Azure content filters live on the model **deployment**, the app applies the choice by
updating the account's RAI policy at runtime (control plane), then waits a few seconds for it to
propagate before the council debates.

> ⚠️ This is a **single-presenter demo feature**. The update is **account-global** (it changes the
> policy for every bound deployment) and takes a few seconds to take effect — **do not run concurrent
> deliberations** while using it. It needs the runtime identity to have `raiPolicies/write` on the AI
> Services account (granted in `infra/` via Cognitive Services Contributor on the deploying user) and
> the `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AI_SERVICES_RESOURCE_NAME`,
> `COUNCIL_RAI_POLICY_NAME` env vars (all set by `azd`); it no-ops cleanly if any are missing. Setting
> `Low`/`Medium`/`High` needs no approval. Turning completion filtering off or using annotate-only mode
needs Limited Access.

## Conventions

- **.NET 10**, C# 13 idioms (records, primary constructors, file-scoped namespaces).
- **Local-only process** — loopback access only; no container, hosted-agent, tunnel, proxy, or remote-server path.
- **User identity for Azure** — the local process uses the signed-in Microsoft Entra user through `DefaultAzureCredential`; no keys or connection strings.
- **100% IaC** — all Azure resources via Bicep / `azd`.
