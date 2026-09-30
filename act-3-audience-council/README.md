# Act 3: The Audience's Council

Act 3 creates a second council live from the unmodified
`garylumsden/azure-agent-council-template`. The audience supplies the scenario. The new council
reuses Act 2's Azure environment and does not run `azd up`.

## Live steps

Use a new private repository name for the live session.

```powershell
gh auth status
gh repo create <owner>/<new-repo> `
  --template garylumsden/azure-agent-council-template `
  --private --clone
Set-Location <new-repo>
```

```bash
gh auth status
gh repo create <owner>/<new-repo> \
  --template garylumsden/azure-agent-council-template \
  --private --clone
cd <new-repo>
```

Copy the generated Act 2 environment file. It is ignored by Git and must not be committed.

```powershell
Copy-Item <main-repo>\act-2-council\src\GovernanceCouncil.Web\.env `
  .\src\GovernanceCouncil.Web\.env
```

```bash
cp <main-repo>/act-2-council/src/GovernanceCouncil.Web/.env \
  ./src/GovernanceCouncil.Web/.env
```

Run the Scenario Architect with the completed [`STARTER-PROMPT.md`](STARTER-PROMPT.md). Confirm the
full design once. Do not interview topic by topic.

Use Local MAF and the `Fast` profile:

```powershell
$env:COUNCIL_AGENT_RUNTIME = "maf"
$env:COUNCIL_MODEL_PROFILE = "Fast"
dotnet run --project src\GovernanceCouncil.Web
```

```bash
export COUNCIL_AGENT_RUNTIME=maf
export COUNCIL_MODEL_PROFILE=Fast
dotnet run --project src/GovernanceCouncil.Web
```

Upload the generated dossier and start the deliberation.

## Data sharing

The copied `.env` points at the same Cosmos DB, Blob Storage, Foundry project, and model deployments
as Act 2. Assessments and Nexuses therefore share the same backing stores. The Nexus Analyst can link
an audience Assessment to Act 2 evidence, but this is a bonus and is not a scripted stage moment.

## Rehearsal timings

| Step | Measured time |
|------|--------------:|
| Create and clone private template repo | 13.82 seconds |
| Copy environment and fallback | 0.14 seconds |
| Clean build | 5.28 seconds |
| Start Local MAF app | About 13 seconds |
| Fallback deliberation | About 155 seconds |

The Scenario Architect time depends on the audience design. If it approaches three minutes, use the
fallback rather than taking time from the debate.

## Fallback

If the Scenario Architect stalls, copy the tested fallback into the fresh template:

```powershell
Copy-Item <main-repo>\act-3-audience-council\fallback\config\* .\config -Recurse -Force
Copy-Item <main-repo>\act-3-audience-council\fallback\data\* .\data -Recurse -Force
```

```bash
cp -R <main-repo>/act-3-audience-council/fallback/config/. ./config/
cp -R <main-repo>/act-3-audience-council/fallback/data/. ./data/
```

The fallback asks whether a fictional team should standardise on tabs, spaces, or an automatic
formatter. Use the recorded fallback video if repository creation, the Architect, or the live debate
cannot finish inside the stage time box.

The rehearsal repository is private and intentionally retained for cleanup. It is listed in
[`rehearsal-repositories.json`](rehearsal-repositories.json). The reset workflow lists it but never
deletes it automatically.
