# Remote routine failure reporting

Status: idea (short spec, nothing implemented). Deferred from `runtime-variable-substitution.md`, open question 3.

Last updated: 2026-10-22 20:24 (-04:00). Code read at that time, not run.

## Problem

When a routine is sent to another grid (`@grid cmd; cmd`), the sender never learns what happened. If a step fails on the remote grid, the failure message prints on the remote grid's terminal only. The player who typed the command sees nothing.

The runtime variable substitution work adds a clear failure message when a step cannot run (see `runtime-variable-substitution.md`, design step 8). This idea would also show that message on the sending grid.

## Current behaviour (read in code)

| Point | Location | Behaviour |
|---|---|---|
| Send | `IntergridMessageService.SendRequestFromRoutine` | Builds a `command` Request and calls `SendUnicastRequest(targetId, request, null)`. The response callback is `null`, so any Response is ignored. |
| Receive | `CommandBus.HandleExternalCommandRequest` / `HandleIncomingCommandRequest` | The relay receives the request, then either runs it or forwards it to the owning construct instance with a `localcmd` request. |
| Respond | `CommandBus.ExecuteAndRespond` | Calls `RunTerminalCommand`, which only **schedules** coroutines and returns `true`, then immediately returns `COMMAND_EXECUTED` (201). |
| Forward | `IntergridMessageService.SendConstructCommand` | Sends `localcmd` with only the command text. The original sender's identity is not carried. |

Consequences:

- `COMMAND_EXECUTED` means "accepted and scheduled", not "completed". It is sent before the first step runs, so a later step failure cannot be part of that Response.
- A report needs a **second, later message** from the remote grid to the sender.
- When the relay forwards to another construct instance, the sender identity is lost, so it must be carried explicitly.

## Idea

When a routine that came from a remote sender stops because of a failed step, the grid where it failed sends a short report back. The sending grid prints it to its terminal, in the same format as a local failure, with the remote grid named.

```
Remote routine stopped on Zagadka: group 2 of 3, step 4 of 6 (Recharge > ZGDGUIWorking)
  Step:     screen/print "$LCD" "$MSG"
  Reason:   duplicate option --level
  Not run:  2 later steps in this group
  Other groups are not affected and keep running.
```

`Resolved` is left out: it is the remote grid's variables resolved on the remote grid, and may expose values the sender should not see (see open questions).

## Sketch

1. **Carry the sender.** The `command` request already has `OriginId` and `OriginName` in its header. Copy them into the `localcmd` forward as `ReplyToId` and `ReplyToName`, and keep them in the routine's state (the `RoutineState` introduced by the failure handling work).
2. **Report route.** Register a new route on the sender, for example `commandreport`. The remote grid sends it as a unicast Request to the sender, with the status `ERROR` (500) and the message fields in the body: grid name, group, step, chain, written step, reason, steps not run.
3. **Show it.** The route handler prints the message with `Mother.Print` and writes it to the log. It returns no Response, or a bare `OK`.
4. **Only on failure.** Success is not reported (see open questions). One message per failure, so a routine with 3 failing groups sends 3.

## Open questions

1. **Information exposure.** The report includes the written step and reason. It should not include resolved values, which could expose the remote grid's variables. Is the written step safe to send? Probably, since the sender wrote it. A step inside a remote config command body was written on the remote grid. Option: send only the config command name for those.
2. **Authorisation.** The remote grid should only report to a sender it accepted a command from. Check that the report goes through the same channel and passcode as the original request.
3. **Success reports.** Do we want a "routine finished" message, so that `COMMAND_EXECUTED` becomes useful? That needs a completion signal for each coroutine and is a larger change. Out of scope for the first version.
4. **Fire-and-forget bookkeeping.** `SendUnicastRequest` stores the `null` callback in `activeRequests` under the request Id and never removes it. A report feature that adds more request and response traffic should check that this dictionary does not grow without limit.
5. **Broadcast (`@*`).** One request goes to every grid, so one failure message could arrive from many grids. Each message names its grid. Consider rate limiting.
6. **Sender identity across the relay.** The relay forwards with `localcmd`, so check that `OriginId` (`Mother.Id`) and the unicast address (`IGC.Me`) are the same thing, or send both.

## Not in scope

- Returning results (data) of remote commands.
- Making `COMMAND_EXECUTED` mean "completed".
- Retry or acknowledgement of the report.
- Reporting failures that are not caused by variable substitution (`Command not found`, exceptions in a module's `Execute`).

## Depends on

- Failure handling and the message format in `runtime-variable-substitution.md`, design step 8.
