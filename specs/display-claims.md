# Display claims

Status: implemented in Core, MotherAutopilotSystem, MotherGUI and MotherOS (2026-10-31); item 5 (log surfaces) left as is. Size not yet measured with the build. Notes: `MapModule` keeps a slim `MapView` callback so `view/go` can hand it a surface (show claims, hide releases); `ViewModule.NavigateBack` on a display it no longer holds reclaims it instead of navigating back.

## Problem

Several scripts draw on the same text surface. Each decides alone whether it may draw, and none tells the others when it takes a surface.

- `view/go`, `autolcd/print`, `autolcd/clear` and the Map view fight for one surface. The result is stutter and flicker between `TEXT_AND_IMAGE` and `SCRIPT`.
- Ownership is re-implemented per script: `MenuDisplay` has `IsParked`, `Foreign*`, `OwnsSurface`, `ClaimSurface`; `MapDisplay` has `Active`, `ShouldDraw`, `Acquire`.
- `Acquire` and `ClaimSurface` never tell the previous holder. Only GUI's `Unpark` and `view/go` route 5 notify, and only for a holder GUI already knew.
- The claimant chooses the mode (`SCRIPT` or `TEXT_AND_IMAGE`), so the content type alone cannot say who owns a surface.

## Goal

One small Core mechanism so any script can take a display from any other, in either mode, and every script can ask "may I still draw?". Keep Core lean: no new module, class, event or route, and a net reduction in characters across the scripts.

## Design

**State.** `DisplayModule` holds one map: display name to the `ContentType` that its current holder claimed. The name is the one scripts already use (`Block` or `Block:Index`, see `GetDisplaysByName`). The map lives in the module, so it survives a script's display rediscovery. No change to `Display`.

**API (all on `DisplayModule`).**

| Member | Behaviour |
|---|---|
| `Claim(string name, IMyTextSurface surface, ContentType type)` | Set `surface.ContentType` to `type`, and `surface.Script = ""` when `type` is `SCRIPT`; write only what differs. Record `claims[name] = type`. Broadcast a release for `name` to the construct. |
| `bool Holds(string name, IMyTextSurface surface)` | True when `claims` has `name`, `surface.ContentType` equals the claimed type, and (for `SCRIPT`) `surface.Script` is empty. |
| `Release(string name)` | Remove `claims[name]`. Called locally by the hook below. |

`Holds` is false after a release, and after a third-party script (Automatic LCDs) changes the content type. A script draws, or calls `WriteText`, only when `Holds` is true.

**Release signal.** Reuse the existing `"view"` route in `IntergridMessageService`.

1. `Claim` sends a `view` request with an empty `View`, the `Display` name, and `Active=0`, using `SendConstructBroadcastRequest` with a `null` callback.
2. `HandleViewRequest` treats an empty `View` as "release display": it calls `Mother.GetModule<DisplayModule>().Release(name)` and does not look up `Views`. A non-empty `View` behaves as it does today.

**Same-mode takeover.** `SCRIPT` over `SCRIPT`, or text over text, leaves nothing on the surface to detect. The broadcast is the only signal. The old holder may draw once more before the message arrives; accepted for now, to be revisited after testing.

## Work

1. **Core.** Add the members above to `DisplayModule`. Add the empty-`View` branch to `HandleViewRequest`. Unit tests (listed below).
2. **MotherAutopilotSystem.** `MapView` claims with `SCRIPT` and draws only when `Holds`. Remove `MapDisplay.Active`, `ShouldDraw` and `Acquire`, and the per-view activation callback in `MapModule`.
3. **MotherGUI.** Claim with `SCRIPT` when `view/go` or menu navigation renders a display. Background renders (`Run`, `RenderAll`, the periodic refresh, the notification clear) draw only when `Holds`. Remove `MenuDisplay.OwnsSurface` and `ClaimSurface`, and the `IsParked` and `Foreign*` handling that the claim replaces. Check that foreign-view parking (`TryGoToViewOrMenuOrPath` route 5) still works, or fold it into the claim.
4. **MotherOS.** `autolcd/print` calls `DisplayModule.Claim(name, surface, TEXT_AND_IMAGE)` after writing, so Mother scripts yield at once. `autolcd/clear` releases nothing, but it still removes the `@N` header, as it does now.
5. **Core log surfaces (optional).** `DisplayModule` forces `TEXT_AND_IMAGE` on log surfaces itself. It could use `Claim` so the log takes part in the same arbitration. Decide when item 1 lands.

## Tests

- `Claim` sets the type and clears `Script` for `SCRIPT`; leaves `Script` alone for text.
- `Holds` is true after `Claim`, false after `Release`, and false after another writer changes `ContentType` or sets `Script`.
- Two claims on one name: the second replaces the first; `Holds` is true only with the second's type.
- `HandleViewRequest` with an empty `View` releases the named display and does not invoke a registered view callback.
- `HandleViewRequest` with a `View` name still invokes its callback (existing behaviour).
- GUI: a background render skips a surface it no longer holds; `view/go` claims it again. Map: same.

## Size

- Core grows by roughly 25 lines. The build's type trimmer removes unused Core methods, so a script that never calls `Claim` or `Holds` pays nothing.
- GUI (101,500 characters) and OS (100,440) are already over the 100,000 limit after minification. Removing `IsParked`, `Foreign*`, `OwnsSurface`, `ClaimSurface`, `Active`, `ShouldDraw` and `Acquire` should make the scripts smaller overall. Measure before and after with the existing build.

## Out of scope

- Persisting claims across a reboot. A script that claims again after boot re-releases the others.
- Claiming a named built-in script (`Script` other than empty).
- Preventing the one-frame overlap when the previous holder has not yet received the release.
