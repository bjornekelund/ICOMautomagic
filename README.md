# ICOM Automagic
A Windows utility with small screen footprint that use the radio information broadcast over 
UDP from [DXLog.net](http://dxlog.net) or [N1MM Logger+](http://www.n1mm.com) to automatically set 
spectrum scope settings and output power on a range of ICOM radios. 

It sets and remembers waterfall/spectrum edges, display reference level, and output power for 
each used frequency band and operating mode and also offers a zoomed-in display mode with user 
configurable width, centered around the current operating frequency, with its own dedicated 
reference level setting. 

Settings are controlled using two input boxes for frequency (requiring RETURN for entry, 
with erroneous input silently ignored) and sliders for reference level and output power. 
Clicking the power level percentage number toggles "barefoot" mode which sets radio output 
power to 100% at activation and then re-sets it to 100% whenever band or mode changes.

The configuration panel is opened by right-clicking the zoom button in the lower right corner. 

The configuration panel allows setting the UDP broadcast port, zoom width in kHz, ICOM radio model, 
radio control COM port and speed, which of the radio's three edge sets should be used. It offers 
the option to make the application always stay on top of any other window. 
This is particularly useful when used together with a full-screen logger such as DXLog.net.

A typical usage scenario is to connect **ICOM Automagic** to the "Remote" CI-V port of the 
ICOM radio while the logger is connected to the fast USB CI-V port. Please note that the two ports 
need to be unlinked in the settings. On IC-705 you typically use the Bluetooth port.

For the application to work you need to enabled UDP XML broadcast in the logger.

In DXLog.net this is done with the `Options|Broadcast|Radio information menu` item. 
Make sure the correct IP address and port are set in `Options|Configure network` and 
that the port is the same as in ICOM Automagic's XML broadcast setting. 
The default is 127.0.0.1 and 12060. 

With N1MM Logger+ you need to check the *Radio* box on the *Broadcast data* tab in 
N1MM Logger+'s Configuration panel. Also here, make sure the UDP port is the same 
in both N1MM Logger+ and ICOM Automagic.

The application does not query the radio for settings. 
Any manual changes made using the radio's own controls are ignored and are consequently not saved. 
To not interfer with manual adjustments, the application only updates the radio's settings on band or mode changes. 

Please note that, contrary to DXLog.net, N1MM Logger+ only broadcasts the main receiver's frequency over UDP. 
This means e.g. cross band SO2V is only supported with DXLog.net.

**This software controls radio transmitter hardware.** 
Be aware that wrongly used or malfunctioning software can damage or destroy such hardware. 
This software is used entirely at your own risk.
