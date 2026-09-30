# Act 1: Pull the Plug

Act 1 compares the same small coding task on a local model and a cloud model.

The local run uses Copilocal 0.1.4, LM Studio, and `ibm/granite-4-h-tiny`. Copilocal sets
`COPILOT_OFFLINE=true`. The harness also gives the child process an unreachable loopback proxy while
it keeps the LM Studio endpoint on `NO_PROXY`.

## Prerequisites

* GitHub Copilot CLI is installed and signed in.
* Copilocal 0.1.4 is installed from `garylumsden/copilocal`.
* LM Studio has `ibm/granite-4-h-tiny` downloaded. `run-local` starts the local server and loads the
  model with 131,072 context tokens.
* The configured cloud model is available to the presenter's Copilot account.
* .NET 10 is installed.

## Task

Read [`TASK.md`](TASK.md) for the prompt, output check, selected models, and measured times.
The task uses fictional demo data.

`RUN COMPLETED` means Copilot exited successfully within the time box and produced a non-empty
`index.html`. Content, capitalisation, design, and markup do not determine that status. The audience
judges output quality with its vote. The harness does not edit the generated page.

## Run

From this folder:

```powershell
.\prepare.ps1
.\run-local.ps1 --label local-live
.\run-cloud.ps1 --label cloud-live
.\record-vote.ps1 --label local-live --vote 30
.\record-vote.ps1 --label cloud-live --vote 24
.\show-results.ps1
```

```bash
./prepare.sh
./run-local.sh --label local-live
./run-cloud.sh --label cloud-live
./record-vote.sh --label local-live --vote 30
./record-vote.sh --label cloud-live --vote 24
./show-results.sh
```

`prepare` recreates the isolated `runs/local/` and `runs/cloud/` workspaces. Live workspaces and
`results/results.json` are ignored by Git.

## Parallel runs and result pages

Run `prepare` once before starting either model. It builds the harness and resets both workspaces.
Do not run `prepare` or `reset-all` while a comparison is running.

You can run local and cloud in two terminals with distinct labels. Their workspaces are separate,
and result and vote updates use a shared file lock and atomic replacement.
Run only one local process and one cloud process at a time.

Keep the network connected for parallel runs. The local child process uses Copilocal offline mode
and a blocked external proxy, while the cloud process uses the network. This is not a physical
network-disconnect demonstration. Run sequentially when you disconnect the cable or Wi-Fi.

Each completed run opens its own `index.html` in the default browser. Use `--no-open` to suppress it:

```powershell
.\run-local.ps1 --label local-live --no-open
```

To reopen an existing result without rerunning the model:

```powershell
dotnet run --project .\harness\Act1Harness.csproj --no-build -- open-local
dotnet run --project .\harness\Act1Harness.csproj --no-build -- open-cloud
```

## Terminal output

The scripts use Copilot's non-interactive prompt mode, not its full interactive chat UI. Readable
text is the default. It shows the agent's activity and response without raw JSON events.

Use `--json` only when you need machine-readable event output:

```powershell
.\run-cloud.ps1 --label cloud-live --json
```

Token statistics and `results.json` remain JSON in both modes. The harness reads token statistics
from `--usage-output-file`, not the terminal event stream.

## Stage sequence

1. Run `prepare`.
2. Disconnect the laptop from the network.
3. Run the local model through Copilocal.
4. Reconnect the laptop.
5. Run the cloud model through Copilot CLI.
6. Record the audience vote and show the comparison.

After showing both pages, ask: "Which result would you prefer to use: local or cloud?"
Record the hands-up count for each result. This optional poll records audience preference, not an
objective score. Missing votes stay unknown. Votes do not change completion and are not council-member votes.

If a live run fails, use the recorded run video and
[`results/results.sample.json`](results/results.sample.json):

```powershell
.\show-results.ps1 --sample
..\act-2-council\scripts\act1-to-dossier.ps1 --sample
```
