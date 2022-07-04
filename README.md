# Tape
### Kansas City Tape utility

- Process wav files into a tap format
- Process tap files into ASCII characters

### Usage:
TapeConsole [options] <filename> [command]

### Arguments:
<filename>

### Options:
/v, --version <version>        The tape version
  
/f, --frequency <frequency>    The processor frequency in Hz
  
/S, --start <start>            Offset to start of data in seconds
  
/E, --end <end>                Offset to end of data in seconds

### Commands:
histogram    Generate histogram
code         Generate ascii format file
tape         Generate tape format file
