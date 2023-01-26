# RDR2 DLSS Replacer
Rockstar Launcher replaces DLSS file with their own version every time you start Red Dead Redemption 2. This app bypasses this problem, so you can use your preferred DLSS version easily.

Run it before launching RDR2, it will replace DLSS with your preferred version, and when you exit RDR2 it will restore original DLSS file so that Rockstar Launcher does not update the game every time you launch or exit game.

## How to use
1. Download from: https://github.com/Bullz3y3/RDR2_DLSS_Replacer/releases/latest
2. Extract it.
3. Notice `dlss_file_to_use.dll` - This is the DLSS file that will be used for RDR2.
   - Replace it with your own version if you prefer. Currently, I've packed it with: DLSS v2.5.1.0
5. Run `RDR2_DLSS_Replacer.exe`
   - It needs Administrator Privileges to replace `nvngx_dlss.dll` in RDR2 directory.
5. `RDR2_DLSS_Replacer.exe` can be kept open for as long as you want, it does not exit with the game, so you can launch or exit the game multiple times and it will keep processing.

## How it works
It monitors `RDR2.exe` process in task manager and as soon as it finds the process, it immediately replaces the game's DLSS in the game's location and creates backup of original DLSS file. Similarly, when the `RDR.exe` process exits it reverts back DLSS.