# Proposal: run our coding agents on local models

## Context

The team uses coding agents for code generation, refactoring, tests, and repository questions.
Current cloud-hosted models provide strong results, but the team wants more control over data flow,
cost, latency, and availability.

## Proposal

Run the team's coding agents on local models hosted on developer workstations or shared on-premises
hardware. Start with a time-boxed pilot on selected repositories before any wider adoption.

## Options

### Local

Run models and coding agents entirely on team-controlled hardware.

### Cloud

Continue to use approved cloud-hosted models and existing developer tooling.

### Hybrid

Route sensitive or routine tasks to local models. Use approved cloud models for tasks that need
greater capability or larger context.

## Evidence
<!-- ACT1_TASK -->

<!-- ACT1_RESULTS -->

## Risks

Local models can reduce data movement and network dependency, but quality, tool use, context limits,
hardware support, maintenance, and total cost can vary by model and workstation.

## Decision requested

Decide whether to approve, approve with conditions, or reject a time-boxed local-model pilot for
selected coding-agent tasks.
