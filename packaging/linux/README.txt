ReqMint for Linux
=================

ReqMint is self-contained, so a separate .NET installation is not required.

1. Install the native desktop libraries required by Avalonia.
   Debian/Ubuntu: sudo apt install libx11-6 libice6 libsm6 libfontconfig1
   Fedora:        sudo dnf install libX11 libICE libSM fontconfig
2. Open a terminal in this directory.
3. Run: ./reqmint

The archive is portable and does not install files outside its extracted
directory. ReqMint stores local device settings and history in the operating
system's standard application-data location.

Project: https://github.com/alparslanakbas/ReqMint
