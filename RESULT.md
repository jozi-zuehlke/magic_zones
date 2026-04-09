# Result: One-shot implementation

- prompts:

  ```txt
  please implement the product as specified in `/home/dev/src/fancy_zones/PRODUCT_REQUIREMENTS.md`
  ```

  ```txt
  I built the app as a single executable, ran it on my Windows host, and it launched, registered itself in the tray, and created the default config file, but when I drag a window and hold down the activation key (shift), nothing happens, i.e. no overlay is being shown, and the window is not snapping into any zone when dropping it
  ```

- no tests
- single binary not built automatically
- the app did not work on first try
- with one round of fixing, it works
