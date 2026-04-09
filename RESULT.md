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

- created a nice architecture, including tests
- did not create single executable by itself, and created no scrips to run the publish
- it created an app that crashed on launch
- was able to fix the blocking bug after one prompt, then just worked
- was able to implement my feature requests with tests
