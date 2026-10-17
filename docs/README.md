# Design docs

These files are the source of truth for Undercurrent's design. Change them in the same commit as the
code that follows from a decision, so design and code never drift apart.

| Doc | What's in it |
| --- | --- |
| [concept.md](concept.md) | Pitch, pillars, world and factions, governments, history of the Current, story structure, endings, towns, perma-death, art direction, decisions |
| [combat-rules.md](combat-rules.md) | Turns, stats, movement, resources, hit and damage, counters, vision and Hidden, statuses, classes, unlocks, switching, all abilities |
| [vertical-slice.md](vertical-slice.md) | The first playable target: story scope, battles, systems in and out, milestones, done-when |
| [prototype/ashfield-skirmish.html](prototype/ashfield-skirmish.html) | The browser combat prototype. Open it in any browser |

Live tuning numbers are in [`../data/rules.json`](../data/rules.json); where a doc and the data disagree,
the data wins and the doc should be updated.

The original claude.ai docs (moved here on 17 Oct 2026) are a read-only archive:
[concept](https://claude.ai/code/artifact/0d86cb17-dd81-4383-8a8b-394b0154c1dd),
[combat rules](https://claude.ai/code/artifact/c5643660-46fd-4760-ae7c-1362ff2edb91),
[vertical slice](https://claude.ai/code/artifact/7a6f08ad-33b0-4493-8ba1-535974344bea).
