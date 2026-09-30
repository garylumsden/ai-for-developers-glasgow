# council-tools MCP server

`CouncilTools.Mcp` is a .NET 10 Model Context Protocol server. It uses Streamable HTTP at
`http://127.0.0.1:5199/mcp`.

## Start

From the repository root:

```powershell
dotnet run --project act-2-council/src/CouncilTools.Mcp
```

```bash
dotnet run --project act-2-council/src/CouncilTools.Mcp
```

Health check:

```text
http://127.0.0.1:5199/health
```

## Tools

| Tool | Source |
|------|--------|
| `get_azure_model_price` | Azure Retail Prices API |
| `compare_run_costs` | Act 1 results, Azure prices, and fictional local assumptions |
| `check_model_register` | Fictional `act-2-council/config/model-register.json` |
| `get_product_lifecycle` | endoflife.date |
| `get_repo_activity` | Public GitHub REST API |

Every response includes `source`, `retrievedAt`, and `data`. Live upstream failures use committed
snapshots and return `source: cached`.

## Validate and refresh snapshots

```powershell
dotnet run --project act-2-council/src/CouncilTools.Mcp -- --self-test
```

The self-test uses real public APIs, refreshes snapshots, verifies cached fallback, rejects invalid
input, and checks deterministic cost arithmetic.

Application Insights uses `APPINSIGHTS_CONNECTION_STRING` when it is present. No key is stored in
the repository.

The optional hosted GitHub MCP server is separate from `council-tools`. Its verified read-only
metadata is in `act-2-council/config/github-mcp.json`. The council runtime skips it when
`GITHUB_MCP_TOKEN` is absent.
