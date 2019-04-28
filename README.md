# ICOM Automagic
A Windows utility with small screen footprint that use the radio information broadcast over 
UDP from [N1MM Logger+](http://www.n1mm.com) and updates an IC-7610 at band or mode changes 
based on this. It also supports IC-7300 and IC-7850/51 but this requires changes to the 
source code and a new build.

updates a waterfall/spectrum display capable 
ICOM radio at band or mode changes, based on this. (e.g. IC-7300, IC-7610, and IC-7850/51). 

It sets and remembers waterfall/spectrum edges, display reference level, and output power for 
each used frequency band and operating mode and also offers a zoomed-in display mode with a 
20kHz (configurable in the source code) span centered around the current operating frequency, 
with its own dedicated reference level setting. All settings are controlled using two input boxes 
for frequency (requiring RETURN for entry, with erroneous input silently ignored) and sliders 
for reference level and output power. Clicking the power level percentage number toggles "barefoot" 
mode which sets radio output power to 100% at activation and then re-sets it to 100% whenever band or mode changes.

A typical usage scenario is to connect **ICOM Automagic** to the slow "Remote" CI-V port of the 
ICOM radio while the logger is connected to the fast USB CI-V port. The application also accepts a 
command line argument to set the serial port to be used (e.g. `$ icomautomagic COM3`\). This setting is remembered. 

For the application to work you need to check the "Radio" box on the "Broadcast data" tab in N1MM Logger+'s 
Configuration panel. Please also note that the application does not query the radio for settings. Any manual 
changes made using the radio's own controls are ignored and are thus not saved. 

Limitations:

- Only considers radio #1 and thus does not support cross band SO2V
- Listens to UDP packets on port 12060. Change requires re-build.
- Assumes CI-V address 0x98 (IC-7610). Change requires re-build.
- Assumes 19200 bps CI-V interface. Change requires re-build.

This software controls radio transmitter hardware. 
Be aware that wrongly used or malfunctioning software can damage or destroy such hardware. 
This software is used entirely at your own risk.

