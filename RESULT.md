# Result: One-shot implementation

- prompts:

  ```txt
  please implement the product as specified in `/home/dev/src/fancy_zones/PRODUCT_REQUIREMENTS.md`
  ```

  ```txt
  I built the app as a single executable, ran it on my Windows host, and it launched, registered itself in the tray, and created the default config file, but when I drag a window and hold down the activation key (shift), nothing happens, i.e. no overlay is being shown, and the window is not snapping into any zone when dropping it
  ```

  ```txt
  it is working now, but there is one thing I don't like: I have to hold shift before I start dragging the window. with the real Microsoft PowerToys FancyZones, when I am already dragging a window, and then hold shift, the zones are shown, and when I release shift without dropping the window, then the zones are hidden and nothing else happens. please refactor the app to allow dynamic activation while a window is already being dragged
  ```

  ```txt
  great. now please make the zones a bit transparent when showing them. currently they are fully opaque, hiding all the existing windows behind them
  ```

- no tests
- single binary not built automatically
- the app did not work on first try
- with one round of fixing, it works
- one feature addition worked
- second feature addition worked
