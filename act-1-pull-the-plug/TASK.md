# Glasgow Subway Service Board

## Exact prompt

```text
Create the fictional Glasgow Subway service board described in brief.txt.

Use the file and shell tools. Do not only return a code sample.
Work only in the current folder.
Read brief.txt and create one self-contained index.html file.
Use no external resource or network call.
Choose the HTML structure and visual design. Make the board dark and high contrast.

Run: pwsh -NoProfile -File ../../check.ps1 --workspace .
The check only confirms that index.html exists and is not empty.
The audience will judge the content and design. Do not change the output just to match exact text or markup.
```

## Starting files

* [`starter/brief.txt`](starter/brief.txt)

## Output check

```powershell
.\check.ps1 --workspace .\runs\local
```

```bash
./check.sh --workspace ./runs/local
```

The check confirms that `index.html` exists and is not empty. It does not enforce exact text,
capitalisation, styling, station data, or HTML structure.

A run completes when Copilot exits with code `0` within the time box and produces non-empty output.
The audience judges content and design quality. `succeeded` in the result JSON records completion,
not quality.

## Time box and models

* Time box: 5 minutes.
* Rehearsal target: 3.5 minutes.
* Local provider: LM Studio through Copilocal 0.1.4.
* Local model: `ibm/granite-4-h-tiny`, Q4_K_M, 7B parameters, 131,072 loaded context tokens.
* Cloud model: `gpt-5.4`.

## Why this task suits Granite

The current Foundry Local catalogue has tool-capable NPU models, but each exact NPU variant has only
4,224 compiled context tokens. Copilot needs at least 32,768 tokens. Granite is the first eligible
fallback. It advertises native tool use and supports up to 1,048,576 tokens.

The task asks Granite to read one short brief, create one file, and run one output-file check. The
presenter can increase browser zoom for the venue. A
pre-task Copilot file-and-shell tool-loop probe passed in 222.72 seconds. Direct LM Studio generation
completed in 2.88 to 4.66 seconds for 143 output tokens.

Measured rehearsals before the output checks were relaxed on 30 September 2026:

| Run | Duration | Result |
|-----|---------:|:------:|
| Local 1 | 155.36 seconds | PASS |
| Local 2 | 92.74 seconds | PASS |
| Local 3 | 81.71 seconds | PASS |
| Cloud | 56.76 seconds | PASS |

All three local runs passed Copilocal offline mode and the child-process external connectivity block.
The historical timings above are not measurements of the revised prompt.
