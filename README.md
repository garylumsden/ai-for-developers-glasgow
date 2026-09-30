# AI for Developers

This repository contains the complete demonstration for **AI for Developers**, presented by Gary
Lumsden at the Glasgow Azure User Group on 30 September 2026.

## The three acts

| Act | Folder | Purpose |
|-----|--------|---------|
| 1. Pull the Plug | [`act-1-pull-the-plug/`](act-1-pull-the-plug/) | Compare the same coding task on a local model with the network disabled and on a cloud model. |
| 2. The Glasgow Developer Council | [`act-2-council/`](act-2-council/) | Debate the Act 1 evidence with a configured multi-agent council, cited knowledge, and MCP tools. |
| 3. The Audience's Council | [`act-3-audience-council/`](act-3-audience-council/) | Prepare a second council that the audience designs live. |

## Repository layout

```text
.
├── .github/                    # Copilot agents, instructions, workflows, and reusable prompts
├── act-1-pull-the-plug/         # Local and cloud coding comparison
├── act-2-council/               # Configured Agent Council application and Azure IaC
├── act-3-audience-council/      # Live audience-council preparation
├── docs/                       # Session build record and presenter guide
└── scripts/                    # Whole-session reset automation
```

## Prerequisites

* GitHub CLI and GitHub Copilot CLI
* .NET 10 SDK
* Azure CLI and Azure Developer CLI
* Copilocal 0.1.4 or the pinned version stated in the Act 1 guide
* LM Studio with the selected local model downloaded before the event
* Access to the selected cloud model

## Guides

* [Presenter guide](docs/PRESENTER-GUIDE.md)
* [Council guide](act-2-council/README.md)
* [Act 1 guide](act-1-pull-the-plug/README.md)
* [Act 3 guide](act-3-audience-council/README.md)
* [Build record](docs/BUILD-PLAN.md)

## Readiness and reset

Run the read-only readiness prompt from
`.github/prompts/demo-readiness-check.prompt.md`.

Preview the whole-session reset:

```powershell
.\scripts\reset-all.ps1 -WhatIf
```

```bash
./scripts/reset-all.sh --what-if
```

Start every local Act 2 process with one command:

```powershell
.\scripts\start-act2.ps1
```

```bash
./scripts/start-act2.sh
```

The repository remains private until the final secret scan passes. The presenter makes it public
before the talk.

## License

This repository is licensed under the [MIT License](LICENSE).
