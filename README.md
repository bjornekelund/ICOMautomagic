# ICOMautomagic
A small WPF app that listens to the UDP broadcast from N1MM Logger+ 
and updates the waterfall/spectrum edges and corresponding reference 
level based on this on an IC-7610 (likely also other waterfall-capable 
ICOM but this is not verified.) Accepts and remembers changes to the 
settings, also between uses. 
Has both a "normal" mode where the settings are based on band and mode 
and a "zoomed" setting where the frequency range is fixed and centered 
around the current operating frequency. Only considers radio #1. 
Typically connects to the "remote" CI-V port of the radio while the 
logger is connected to the USB CI-V port. 
Accepts a command line argument for the used port (e.g. "COM3") 
and remembers this between runs. 
