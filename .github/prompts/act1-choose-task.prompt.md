# Re-select the Act 1 task

Profile the presenter's selected local model before changing the task.

1. Search the current provider catalogues, not only downloaded models.
2. Prefer an NPU variant only when its exact variant supports native tool calls and at least 32,768
   context tokens. Prefer 131,072 tokens or more.
3. Update the local runtime when the installed version cannot read the current catalogue.
4. Verify native tool calls through Copilocal and measure three direct generation samples.
5. Design a visible, dependency-free task with an output-file check. Completion measures execution
   and non-empty output, not exact content or markup. The audience judges quality.
6. Keep the task under 5 minutes. Target 3.5 minutes.
7. Update `act-1-pull-the-plug/TASK.md`, `task.json`, `starter/`, and the check contract. Store the
   local provider and model, but never store Copilocal's discovery index.
8. Run three isolated local rehearsals and one cloud rehearsal.
9. Update `results/results.sample.json` with measured results. Never invent token counts.

Preserve PowerShell and Bash wrappers. Keep all demo data fictional.
