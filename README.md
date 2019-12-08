# ICOM Automagic
A Windows utility with small screen footprint that use the radio information broadcast over 
UDP from [N1MM Logger+](http://www.n1mm.com) or [DXLog.net](http://dxlog.net) to update 
spectrum scope settings and output power on a range of ICOM radios. 

It sets and remembers waterfall/spectrum edges, display reference level, and output power for 
each used frequency band and operating mode and also offers a zoomed-in display mode user 
configurable width, centered around the current operating frequency, with its own dedicated 
reference level setting. 

All settings are controlled using two input boxes 
for frequency (requiring RETURN for entry, with erroneous input silently ignored) and sliders 
for reference level and output power. Clicking the power level percentage number toggles "barefoot" 
mode which sets radio output power to 100% at activation and then re-sets it to 100% whenever band or mode changes.

A typical usage scenario is to connect **ICOM Automagic** to the "Remote" CI-V port of the 
ICOM radio while the logger is connected to the fast USB CI-V port. (Please note that the two ports 
need to be unlinked in the settings.)

For the application to work you need to check the "Radio" box on the "Broadcast data" tab in N1MM Logger+'s 
Configuration panel and make sure the UDP port is the same in both N1MM Logger+ and ICOM Automagic. 
For DXLog.net UDP broadcast needs to be enabled and both the port and the station ID of the tracked station needs to be entered correctly.

The application does not query the radio for settings. Any manual changes made using the radio's own 
controls are ignored and are consequently not saved. 

Please note that the application only considers radio #1 which means it e.g. does not 
support cross band SO2V.

This software controls radio transmitter hardware. 
Be aware that wrongly used or malfunctioning software can damage or destroy such hardware. 
This software is used entirely at your own risk.