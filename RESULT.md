# Result: sub-agents

- prompts:

  ```txt
  please implement the product as specified in `/home/dev/src/fancy_zones/PRODUCT_REQUIREMENTS.md`.

  I want you to act as an engineering manager. you do not perform any implementation work yourself, but instead of coordinate a fleet of sub-agents to perform the various phases of the implementation. you spawn the sub-agents
  sequentially and have each one work until it is done. then you spawn the next one, until the product is successfully implemented.

  - first, you spawn a sub-agent to create the architecture, i.e. project structure, modules, etc., including testing infrastructure that accounts for the fact that the real app will run on Windows, but development and unit
  testing are happening on Linux (e.g. creating a mocking layer for the windows API integrations)
  - then, you spawn a planner sub-agent that splits the implementation into multiple smaller chunks
  - then you spawn a sub-agent to implement each chunk at a time, using red-green TDD, to ensure a properly tested result
  - once the implementation is considered complete by the sub-agents, you spawn a validating sub-agent that independently critically reviews and validates the result
  - finally, any feedback from the validating sub-agent is fed into one more sub-agent to implement any review findings

  you only accept the result once you are happy with the quality and are willing to take accountability for any issues that are found in production
  ```

  ```txt
  when running the app, it immediately crashes with this error in the logs:

    <log>
    [2026-04-09 18:22:58.971] [ERROR] Unhandled domain exception. | System.ArgumentNullException: Value cannot be null. (Parameter 'path1')
    at System.ArgumentNullException.Throw(String paramName)
    at System.IO.Path.Combine(String path1, String path2)
    at FancyZonesPortable.Core.Config.ConfigLoader.DiscoverConfigPath()
    at FancyZonesPortable.Core.Config.ConfigLoader.Load()
    at FancyZonesPortable.App.AppContext..ctor()
    at FancyZonesPortable.Program.Main()
    </log>
  ```

  ```txt
  great, now the app is working, well done. however, I have a few improvement requests. please spawn a sub-agent for each of my requests to implement each one using red-green TDD. my requests are:

  - currently the overlay is not always on top. if I activate other windows after launching the app, and then activate the shortcut for moving a window, the blue overlay is getting rendered behind the other window, but it should always be on top
  - currently, you have to hold shift first and then start dragging the window, but with the real FancyZones, you can start dragging a window and then hold the activation key, and it will immediately activate the positioning mode; and if you release the activation key without dropping the window, then the positioning mode is canceled. please change our logic so that it works the same way
  - currently the overlay is not properly transparent, but fully opaque. I want the overlay to be transparent so that I can see other windows in the background when dragging a new one over them; this also matches the original FancyZones
  - on my Windows 11 system, when dragging a window into a zone, there seems to be a 7px gap on the sides and at the bottom; only at the top is the window flush with the zone border; please make sure that windows are fully covering the zone 

  don't do them in parallel, do them sequentially. also, remember that you are the engineering
  ```

  ```txt
  great, the changes are mostly working exactly like I wanted. however, I still have some minor improvement requests:

  - the 7px fix was applied in a way that first the wrong position is set, and then the offset fix is applied; this leads to a visible movement of the window after it is dropped into a zone; I want the window position to be set only once to the correct final position, accounting for any offsets
  - when starting to drag a window in one zone, and then moving it into another zone without activating the shortcut, and _then_ activating the shortcut while the window is on top of the other zone, the wrong zone is briefly showing as activated; so, for example with a left and right zone, when I drag a window from the left to the right of the screen, and then press shift, then the left zone is briefly showing as activated; we need to ensure that the initially active zone considers the current cursor position, even if we are already dragging a window

  don't do these changes in parallel, do them sequentially. also, remember that you are the engineering manager, so don't implement anything yourself, delegate everything to sub-agents, including root cause analysis
  ```

  ```txt
  unfortunately the bug with the wrong zone being highlighted initially when activating after dragging the window is still happening. please spawn another sub-agent to investigate and fix this bug. here is the log .output from one such case. unfortunately it does not say much:

    <log>
    [2026-04-09 19:48:12.989] [INFO] Drag started for window 0xE805F8.
    [2026-04-09 19:48:13.749] [INFO] Snapping to zone 'sharing'.
    [2026-04-09 19:48:13.750] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-09 19:48:13.764] [INFO] Snapped window 0xE805F8 to zone (1444, 251, 1926, 1080)
    </log>
  ```

  ```txt
  great, now the app is fully working. there is one thing I noticed though: there is a noticeable, roughly 500ms delay before the zones are highlighted once the shortcut is activated while dragging a window and also when starting to drag a window while the shortcut is already pressed. is this something we do in our code, or is this some windows delay maybe? please spawn a sub-agent to investigate and fix
  ```

  ```txt
  the app is fully working, but there is one thing I noticed: there is a noticeable, roughly 200-500ms delay before the zones are highlighted once the shortcut is activated while dragging a window and also when starting to drag a window while the shortcut is already pressed. we already tried to fix this (see the latest commits), but the delay is still there. we have recently added detailed debug logging to help with diagnosing this issue. you can find the relevant log excerpt below. please investigate how we can get rid of this delay and make the activation instantaneous. once you have found a way to do it, implement it using red-green TDD.

  <log>
    [2026-04-10 07:26:05.238] [DEBUG] Window move started event for window 0x20C44.
    [2026-04-10 07:26:05.245] [DEBUG] Started drag tracking for window 0x20C44; keyboard hook installed and cursor timer started.
    [2026-04-10 07:26:05.245] [DEBUG] Waiting for activation modifier key press during drag.
    [2026-04-10 07:26:05.741] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:26:05.742] [DEBUG] Activating snapping for window 0x20C44 at cursor (1886, 483) on monitor 'primary'.
    [2026-04-10 07:26:05.742] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:26:05.742] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:26:05.742] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:26:05.742] [DEBUG] Activation cursor at (1886, 483) selected initial zone 'sharing'.
    [2026-04-10 07:26:05.743] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'sharing'.
    [2026-04-10 07:26:05.743] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'sharing'.
    [2026-04-10 07:26:05.916] [DEBUG] Overlay handle registered with window filter: 0x80D20.
    [2026-04-10 07:26:06.218] [DEBUG] Window move ended event for window 0x20C44.
    [2026-04-10 07:26:06.218] [DEBUG] Final cursor position at drag end: (1886, 483).
    [2026-04-10 07:26:06.218] [DEBUG] CommitSnap requested while in state 'DragActive'.
    [2026-04-10 07:26:06.218] [DEBUG] CommitSnap resolved zone 'sharing' to bounds (1444, 251, 1926, 1080).
    [2026-04-10 07:26:06.219] [INFO] Snapping to zone 'sharing'.
    [2026-04-10 07:26:06.219] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:26:06.219] [DEBUG] Applying snap for window 0x20C44 to bounds (1444, 251, 1926, 1080).
    [2026-04-10 07:26:06.219] [DEBUG] Applying snap to window 0x20C44 with target bounds (1444, 251, 1926, 1080).
    [2026-04-10 07:26:06.220] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-10 07:26:06.220] [DEBUG] Using final window bounds (1439, 251, 1936, 1085) after DWM compensation.
    [2026-04-10 07:26:06.244] [INFO] Snapped window 0x20C44 to zone (1444, 251, 1926, 1080)
    [2026-04-10 07:26:06.245] [DEBUG] Stopping overlay render cycle after drag end.
    [2026-04-10 07:26:06.245] [DEBUG] Stopping overlay rendering.
    [2026-04-10 07:26:06.248] [DEBUG] Stopping drag tracking for window 0x20C44; keyboard hook uninstalled and cursor timer stopped.
    [2026-04-10 07:26:07.606] [DEBUG] Window move started event for window 0x20C44.
    [2026-04-10 07:26:07.617] [DEBUG] Started drag tracking for window 0x20C44; keyboard hook installed and cursor timer started.
    [2026-04-10 07:26:07.617] [DEBUG] Waiting for activation modifier key press during drag.
    [2026-04-10 07:26:07.972] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:26:07.972] [DEBUG] Activating snapping for window 0x20C44 at cursor (2093, 150) on monitor 'primary'.
    [2026-04-10 07:26:07.972] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:26:07.973] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:26:07.973] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:26:07.973] [DEBUG] Activation cursor at (2093, 150) selected initial zone 'right-half'.
    [2026-04-10 07:26:07.973] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'right-half'.
    [2026-04-10 07:26:07.973] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'right-half'.
    [2026-04-10 07:26:08.153] [DEBUG] Overlay handle registered with window filter: 0x80D20.
    [2026-04-10 07:26:08.537] [DEBUG] Window move ended event for window 0x20C44.
    [2026-04-10 07:26:08.538] [DEBUG] Final cursor position at drag end: (2093, 149).
    [2026-04-10 07:26:08.539] [DEBUG] CommitSnap requested while in state 'DragActive'.
    [2026-04-10 07:26:08.539] [DEBUG] CommitSnap resolved zone 'right-half' to bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:26:08.539] [INFO] Snapping to zone 'right-half'.
    [2026-04-10 07:26:08.539] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:26:08.539] [DEBUG] Applying snap for window 0x20C44 to bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:26:08.539] [DEBUG] Applying snap to window 0x20C44 with target bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:26:08.540] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-10 07:26:08.540] [DEBUG] Using final window bounds (1371, 0, 2074, 1445) after DWM compensation.
    [2026-04-10 07:26:08.540] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:26:08.540] [DEBUG] Activating snapping for window 0x20C44 at cursor (2093, 149) on monitor 'primary'.
    [2026-04-10 07:26:08.540] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:26:08.540] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:26:08.540] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:26:08.540] [DEBUG] Activation cursor at (2093, 149) selected initial zone 'right-half'.
    [2026-04-10 07:26:08.541] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'right-half'.
    [2026-04-10 07:26:08.541] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'right-half'.
    [2026-04-10 07:26:08.716] [DEBUG] Overlay handle registered with window filter: 0x80D20.
    [2026-04-10 07:26:08.721] [DEBUG] Activation modifier key released (VK=0xA0); cancelling active snap session.
    [2026-04-10 07:26:08.721] [DEBUG] CancelDrag invoked for current snap session.
    [2026-04-10 07:26:08.722] [DEBUG] CancelDrag requested for active drag session.
    [2026-04-10 07:26:08.722] [INFO] Drag cancelled.
    [2026-04-10 07:26:08.722] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:26:08.722] [DEBUG] Stopping overlay render cycle due to drag cancellation.
    [2026-04-10 07:26:08.722] [DEBUG] Stopping overlay rendering.
    [2026-04-10 07:26:08.735] [INFO] Snapped window 0x20C44 to zone (1376, 0, 2064, 1440)
    [2026-04-10 07:26:08.735] [DEBUG] Stopping overlay render cycle after drag end.
    [2026-04-10 07:26:08.736] [DEBUG] Stopping drag tracking for window 0x20C44; keyboard hook uninstalled and cursor timer stopped.
    [2026-04-10 07:26:10.544] [DEBUG] Window move started event for window 0x20C44.
    [2026-04-10 07:26:10.550] [DEBUG] Started drag tracking for window 0x20C44; keyboard hook installed and cursor timer started.
    [2026-04-10 07:26:10.550] [DEBUG] Activation modifier already held at drag start; activating snapping immediately.
    [2026-04-10 07:26:10.551] [DEBUG] Activating snapping for window 0x20C44 at cursor (2204, 27) on monitor 'primary'.
    [2026-04-10 07:26:10.551] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:26:10.552] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:26:10.552] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:26:10.552] [DEBUG] Activation cursor at (2204, 27) selected initial zone 'right-half'.
    [2026-04-10 07:26:10.553] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'right-half'.
    [2026-04-10 07:26:10.553] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'right-half'.
    [2026-04-10 07:26:11.033] [DEBUG] Overlay handle registered with window filter: 0x80D20.
    [2026-04-10 07:26:11.034] [DEBUG] Active zone changed: 'right-half' -> 'left-half' at cursor (783, 256).
    [2026-04-10 07:26:11.034] [DEBUG] Overlay active zone changed: 'right-half' -> 'left-half'.
    [2026-04-10 07:26:11.469] [DEBUG] Window move ended event for window 0x20C44.
    [2026-04-10 07:26:11.470] [DEBUG] Final cursor position at drag end: (783, 256).
    [2026-04-10 07:26:11.471] [DEBUG] CommitSnap requested while in state 'DragActive'.
    [2026-04-10 07:26:11.471] [DEBUG] CommitSnap resolved zone 'left-half' to bounds (0, 0, 1376, 1440).
    [2026-04-10 07:26:11.471] [INFO] Snapping to zone 'left-half'.
    [2026-04-10 07:26:11.472] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:26:11.472] [DEBUG] Applying snap for window 0x20C44 to bounds (0, 0, 1376, 1440).
    [2026-04-10 07:26:11.472] [DEBUG] Applying snap to window 0x20C44 with target bounds (0, 0, 1376, 1440).
    [2026-04-10 07:26:11.472] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-10 07:26:11.473] [DEBUG] Using final window bounds (-5, 0, 1386, 1445) after DWM compensation.
    [2026-04-10 07:26:11.473] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:26:11.473] [DEBUG] Activating snapping for window 0x20C44 at cursor (783, 256) on monitor 'primary'.
    [2026-04-10 07:26:11.474] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:26:11.474] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:26:11.474] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:26:11.474] [DEBUG] Activation cursor at (783, 256) selected initial zone 'left-half'.
    [2026-04-10 07:26:11.475] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'left-half'.
    [2026-04-10 07:26:11.475] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'left-half'.
    [2026-04-10 07:26:11.644] [DEBUG] Overlay handle registered with window filter: 0x80D20.
    [2026-04-10 07:26:11.666] [INFO] Snapped window 0x20C44 to zone (0, 0, 1376, 1440)
    [2026-04-10 07:26:11.666] [DEBUG] Stopping overlay render cycle after drag end.
    [2026-04-10 07:26:11.666] [DEBUG] Stopping overlay rendering.
    [2026-04-10 07:26:11.669] [DEBUG] Stopping drag tracking for window 0x20C44; keyboard hook uninstalled and cursor timer stopped.
  </log>
  ```

  ```txt
  the app is fully working, but there is a minor issue I noticed: when I hold the activation key (e.g. shift) and then move a window for the first time, the overlay gets rendered correctly; but when I drop the window and then start dragging it again, the overlay is not getting rendered during the dragging, just for a split second when I drop the window; I assume we have some bug in the shortcut press detection that does not pick up that the shortcut is already still pressed from a previous activation. please investigate how we can get rid of this delay and make the activation instantaneous. once you have found a way to do it, implement it using red-green TDD.

    <log>
    [2026-04-10 07:48:44.127] [DEBUG] Window move started event for window 0x20C44.
    [2026-04-10 07:48:44.131] [DEBUG] Started drag tracking for window 0x20C44; keyboard hook installed and cursor timer started.
    [2026-04-10 07:48:44.131] [DEBUG] Activation modifier already held at drag start; activating snapping immediately.
    [2026-04-10 07:48:44.132] [DEBUG] Activating snapping for window 0x20C44 at cursor (833, 19) on monitor 'primary'.
    [2026-04-10 07:48:44.132] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:48:44.132] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:48:44.132] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:48:44.132] [DEBUG] Activation cursor at (833, 19) selected initial zone 'left-half'.
    [2026-04-10 07:48:44.132] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'left-half'.
    [2026-04-10 07:48:44.133] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'left-half'.
    [2026-04-10 07:48:44.204] [DEBUG] Overlay handle registered with window filter: 0x390C86.
    [2026-04-10 07:48:44.273] [DEBUG] Active zone changed: 'left-half' -> 'right-half' at cursor (1415, 105).
    [2026-04-10 07:48:44.273] [DEBUG] Overlay active zone changed: 'left-half' -> 'right-half'.
    [2026-04-10 07:48:44.943] [DEBUG] Window move ended event for window 0x20C44.
    [2026-04-10 07:48:44.943] [DEBUG] Final cursor position at drag end: (2182, 146).
    [2026-04-10 07:48:44.944] [DEBUG] CommitSnap requested while in state 'DragActive'.
    [2026-04-10 07:48:44.944] [DEBUG] CommitSnap resolved zone 'right-half' to bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:48:44.945] [INFO] Snapping to zone 'right-half'.
    [2026-04-10 07:48:44.945] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:48:44.945] [DEBUG] Applying snap for window 0x20C44 to bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:48:44.945] [DEBUG] Applying snap to window 0x20C44 with target bounds (1376, 0, 2064, 1440).
    [2026-04-10 07:48:44.945] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-10 07:48:44.945] [DEBUG] Using final window bounds (1371, 0, 2074, 1445) after DWM compensation.
    [2026-04-10 07:48:44.953] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:48:44.954] [DEBUG] Activating snapping for window 0x20C44 at cursor (2182, 146) on monitor 'primary'.
    [2026-04-10 07:48:44.955] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:48:44.955] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:48:44.955] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:48:44.956] [DEBUG] Activation cursor at (2182, 146) selected initial zone 'right-half'.
    [2026-04-10 07:48:44.956] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'right-half'.
    [2026-04-10 07:48:44.956] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'right-half'.
    [2026-04-10 07:48:45.036] [DEBUG] Overlay handle registered with window filter: 0x390C86.
    [2026-04-10 07:48:45.037] [INFO] Snapped window 0x20C44 to zone (1376, 0, 2064, 1440)
    [2026-04-10 07:48:45.038] [DEBUG] Stopping overlay render cycle after drag end.
    [2026-04-10 07:48:45.038] [DEBUG] Stopping overlay rendering.
    [2026-04-10 07:48:45.041] [DEBUG] Stopping drag tracking for window 0x20C44; keyboard hook uninstalled and cursor timer stopped.
    [2026-04-10 07:48:45.685] [DEBUG] Window move started event for window 0x20C44.
    [2026-04-10 07:48:45.692] [DEBUG] Started drag tracking for window 0x20C44; keyboard hook installed and cursor timer started.
    [2026-04-10 07:48:45.692] [DEBUG] Activation modifier already held at drag start; activating snapping immediately.
    [2026-04-10 07:48:45.693] [DEBUG] ActivateSnapping ignored because snap engine is already active.
    [2026-04-10 07:48:45.829] [DEBUG] Active zone changed: 'right-half' -> 'left-half' at cursor (1371, 113).
    [2026-04-10 07:48:45.829] [DEBUG] Overlay active zone changed: 'right-half' -> 'left-half'.
    [2026-04-10 07:48:46.309] [DEBUG] Window move ended event for window 0x20C44.
    [2026-04-10 07:48:46.309] [DEBUG] Final cursor position at drag end: (627, 106).
    [2026-04-10 07:48:46.310] [DEBUG] CommitSnap requested while in state 'DragActive'.
    [2026-04-10 07:48:46.310] [DEBUG] CommitSnap resolved zone 'left-half' to bounds (0, 0, 1376, 1440).
    [2026-04-10 07:48:46.310] [INFO] Snapping to zone 'left-half'.
    [2026-04-10 07:48:46.310] [DEBUG] Resetting snap engine state to Idle.
    [2026-04-10 07:48:46.310] [DEBUG] Applying snap for window 0x20C44 to bounds (0, 0, 1376, 1440).
    [2026-04-10 07:48:46.311] [DEBUG] Applying snap to window 0x20C44 with target bounds (0, 0, 1376, 1440).
    [2026-04-10 07:48:46.311] [INFO] DWM frame compensation: insets L=5 T=0 R=5 B=5
    [2026-04-10 07:48:46.311] [DEBUG] Using final window bounds (-5, 0, 1386, 1445) after DWM compensation.
    [2026-04-10 07:48:46.333] [DEBUG] Activation modifier key pressed (VK=0xA0); activating snapping.
    [2026-04-10 07:48:46.333] [DEBUG] Activating snapping for window 0x20C44 at cursor (627, 106) on monitor 'primary'.
    [2026-04-10 07:48:46.333] [DEBUG] BeginDrag requested for window 0x20C44 while in state 'Idle'.
    [2026-04-10 07:48:46.333] [DEBUG] Resolved 3 zones for monitor 'primary' in working area (0, 0, 3440, 1440).
    [2026-04-10 07:48:46.334] [INFO] Drag started for window 0x20C44.
    [2026-04-10 07:48:46.334] [DEBUG] Activation cursor at (627, 106) selected initial zone 'left-half'.
    [2026-04-10 07:48:46.334] [DEBUG] Starting overlay render cycle with 3 zones; initial active zone 'left-half'.
    [2026-04-10 07:48:46.334] [DEBUG] Starting overlay rendering for 3 zones in bounds (0, 0, 3440, 1440) with active zone 'left-half'.
    [2026-04-10 07:48:46.411] [DEBUG] Overlay handle registered with window filter: 0x390C86.
    [2026-04-10 07:48:46.412] [INFO] Snapped window 0x20C44 to zone (0, 0, 1376, 1440)
    [2026-04-10 07:48:46.412] [DEBUG] Stopping overlay render cycle after drag end.
    [2026-04-10 07:48:46.412] [DEBUG] Stopping overlay rendering.
    [2026-04-10 07:48:46.415] [DEBUG] Stopping drag tracking for window 0x20C44; keyboard hook uninstalled and cursor timer stopped.
    </log>
  ```
- created a nice architecture, including tests
- did not create single executable by itself, and created no scrips to run the publish
- it created an app that crashed on launch
- was able to fix the blocking bug after one prompt, then just worked
- was able to implement my feature requests with tests
- was not able to fix one bug on first try, but was on second
- was able to change architecture for key detection to make activation more snappy
