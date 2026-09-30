# Proposal: standardise code indentation

## Context

The fictional team loses review time to repeated tabs-versus-spaces comments.

## Proposal

Choose one standard for new code: tabs, four spaces, or an automatic formatter that owns the
decision.

## Evidence

The team uses several editors and works across C#, JavaScript, Markdown, and shell scripts.

## Risks

Changing old files can create noisy diffs. A formatter can also create friction if local and CI
versions differ.

## Decision requested

Approve tabs, approve spaces, or require an automatic formatter, with rollout conditions.
