# Background mode

ReqMint can keep the current workspace available in the operating system's tray or menu-bar area after the main window is closed.

## Close behavior

The first ordinary close asks the user to choose one of two actions:

- **Keep running** hides the window and exposes **Open ReqMint**, **New request**, and **Exit** from the tray menu.
- **Exit ReqMint** performs the same unsaved-change protection as an explicit exit.

The choice is stored locally only when **Remember my choice** is selected. It can be changed or reset under **Settings → Background behavior**. Start at login is separate and remains disabled.

Closing to the tray does not save, discard, or otherwise modify workspace drafts. Explicit exit can save or discard supported drafts, and it refuses to exit while a request, collection run, or Git operation is active. Operating-system shutdown is never converted into background mode.

Tray-icon clicks restore the window on Windows and supported Linux desktop environments. The native tray menu remains the portable restore path on platforms where click activation is unavailable.
