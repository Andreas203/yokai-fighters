# Goal agent notes

`tools/goal_agent/writer.py` (`MAP_FILE`) appends one entry here for each feature the goal agent writes, under the heading below. Each entry is a note for the designer's review, followed by a table row for every new script under `game/scripts/` (`writer.map_row`, at most 600 characters) so the branch passes the docs check. When the branch merges, move the rows into the area file that holds the code and delete the entry; a script with a row in both is reported as listed twice. Index: [README.md](README.md).

## Built by the goal agent (`tools/goal_agent/`)
Features written by the goal agent and accepted by its gates (build, tests, re-scan, code-critic, test-breaker). Each still needs the designer's review before it merges.
