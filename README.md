# Tape
## Kansas City Tape Utility:

- Process wav files into a tap format
- Process tap files into ASCII characters

### Usage:
TapeConsole [options] \<filename\> \[command\]

### Arguments:
\<filename\>

### Options:
/v, --version \<version\>		The tape version
  
/F, --frequency \<frequency\>	The processor frequency in Hz
  
/S, --start \<start\>			Offset to start of data in seconds
  
/E, --end \<end\>				Offset to end of data in seconds

### Commands:
 wavelength   Generate a wavelength histogram
 
 amplitude    Generate a amplitude histogram
 
 tape         Generate tape format file
 
 code         Generate ascii format file

---

## code
 Generate ascii format file

### Usage
TapeConsole \<filename\> code \[options\]

### Arguments
  \<filename\>

### Options
/B, --baud \<baud\>        Baud rate

/O, --output \<output\>    The Output filename
