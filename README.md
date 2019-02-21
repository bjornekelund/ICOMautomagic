# ICOM Automagic
A Windows application with small screen footprint that use the radio information broadcast over UDP from [N1MM Logger+](www.n1mm.com) and updates a waterfall/spectrum display capable ICOM radio at band or mode changes, based on this. (e.g. IC-7300, IC-7610, and IC-7850/51). 

It sets waterfall/spectrum edges, display reference level, and output power based on used frequency band and operating mode, also offering a zoom mode with a 20kHz (configurable in the source code) span centered around the current operating frequency, having its own dedicated reference level setting. All settings are controlled using two input boxes for frequency (requiring RETURN for entry, with erroneous input silently ignored.) and sliders for reference level and output power. All settings are remembered. Clicking the power display toggles "barefoot" mode which sets radio output power to 100% at activation and then re-sets it to 100% whenever band or mode changes. 

The application only considers radio #1. 

A typical usage scenario is to connect ICOM Automagic to the slow "remote" CI-V port of the radio while the logger is connected to the fast USB CI-V port. The application accepts a command line argument for the used port (e.g. "COM3") which is also remembered.  
