using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace TapeLibrary
{
    public class Tape
    {
        /*
         * C64 RAW TAPE (.TAP) FILE FORMAT.

        0000 'C64-TAPE-RAW'
        000C UBYTE Version
        000D UBYTE[3] For future use...
        0010 ULONG data length (Intel format LSB,MSB)
        0014 UBYTE[] data
        ...

        Version=0:
        Each data byte represent the length of a pulse (the time until the c64's hardware triggers again).
        The length is (8*data) cycles (PAL C64), i.e. the data byte of 2F represent (47*8/985248) seconds.
        The data byte value of 00 represents overflow, any pulselength of more than 255*8 cycles.

        Version=1:
        As above but data value of 00 is now followed by three bytes, representing a 24 bit value of C64
        cyles (NOT times 8). The order is as follow: 00 <bit0-7> <bit8-15> <bit16-24>.
         */

        /*
         *           1         2
         * 0123456789012345678901234...
         * UK101-TAPRAW0000nnnnxxxxx...
         * 
         * ~----+-----¬|~+¬~-+¬-...+
         *      |      | |   |     |
         *      |      | |   |     +- data(x)
         *      |      | |   +- length(4)
         *      |      | +- Optional(3)
         *      |      +- Version(1)
         *      +- header(12)
         */

        #region Fields

        private int _clockFrequency = 1000000;   // UK101 clock = 1mHz
        private char[] _header;
        private byte _version = 0;
        private uint _length = 0;
        private List<Cycle> _data;
        private string _path = null;
        private string _name = null;
        private Wave _wave = null;
        private TimeSpan _start = new TimeSpan(0);
        private TimeSpan _end = new TimeSpan(0);
        private float _threshold = 0;

        public struct Cycle
        {
            double _start;
            double _mid;
            double _end;
            double _maximum;
            double _minimum;

            public Cycle(double start, double mid, double end, double max, double min)
            {
                _start = start;
                _mid = mid;
                _end = end;
                _maximum = max;
                _minimum = min;
            }

            public double Start
            {
                get
                {
                    return (_start);
                }
                set
                {
                    _start = value;
                }
            }

            public double End
            {
                get
                {
                    return (_end);
                }
                set
                {
                    _end = value;
                }
            }

            public double Maximum
            {
                get
                {
                    return (_maximum);
                }
                set
                {
                    _maximum = value;
                }
            }

            public double Minimum
            {
                get
                {
                    return (_minimum);
                }
                set
                {
                    _minimum = value;
                }
            }
        }

        #endregion
        #region Constructor

        public Tape()
        {
            _header = new char[12] { 'U', 'K', '1', '0', '1', '-', 'T', 'A', 'P', 'R', 'A', 'W' };
            _data = new List<Cycle>();
        }

        public Tape(Wave wave)
        {
            _wave = wave;
            _header = new char[12] { 'U', 'K', '1', '0', '1', '-', 'T', 'A', 'P', 'R', 'A', 'W' };
            _data = new List<Cycle>();
        }
        public Tape(Wave wave, string path, string name)
        {
            _wave = wave;
            _header = new char[12] { 'U', 'K', '1', '0', '1', '-', 'T', 'A', 'P', 'R', 'A', 'W' };
            _data = new List<Cycle>();
            _path = path;
            _name = name;
            // Remove any extension and assume it must be tap
            if (_name != null)
            {
                int pos = _name.LastIndexOf(".");
                if (pos > 0)
                {
                    _name = _name.Substring(0, pos);
                }
            }
        }

        public Tape(string path, string name)
        {
            _header = new char[12] { 'U', 'K', '1', '0', '1', '-', 'T', 'A', 'P', 'R', 'A', 'W' };
            _data = new List<Cycle>();
            _path = path;
            _name = name;
            // Remove any extension and assume it must be tap
            if (_name != null)
            {
                int pos = _name.LastIndexOf(".");
                if (pos > 0)
                {
                    _name = _name.Substring(0, pos);
                }
            }
        }

        #endregion
        #region Properties

        /// <summary>
        /// Add default index method
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Cycle this[int index]
        {
            get
            {
                return (_data[index]);
            }
        }
        public int Count
        {
            get
            {
                return (_data.Count);
            }
        }

        public int Frequency
        {
            get
            {
                return(_clockFrequency);
            }
            set
            {
                _clockFrequency = value;
            }
        }

        public char[] Header
        {
            get
            {
                return (_header);
            }
            set
            {
                _header = value;
            }
        }

        public int Version
        {
            set
            {
                if (value == 0)
                {
                    _version = 0;
                }
                else
                {
                    _version = 1;
                }
            }
            get
            {
                return (_version);
            }
        }

        public TimeSpan Start
        {
            set
            {
                _start = value;
            }
            get
            {
                return (_start);
            }
        }

        public TimeSpan End
        {
            set
            {
                _end = value;
            }
            get
            {
                return (_end);
            }
        }

        public string Path
        {
            set
            {
                _path = value;
            }
            get
            {
                return (_path);
            }
        }

        public string Name
        {
            set
            {
                _name = value;
                // Remove any extension and assume it must be wav
                if (_name != null)
                {
                    int pos = _name.LastIndexOf(".");
                    if (pos > 0)
                    {
                        _name = _name.Substring(0, pos);
                    }
                }
            }
            get
            {
                return (_name);
            }
        }

        public Wave Wave
        {
            set
            {
                _wave = value;
            }
            get
            {
                return (_wave);
            }
        }

        #endregion
        #region Methods

        public void Analyse()
        {
            TimeSpan span;
            int length = _data.Count;
            UInt32 previous = 0;

            for (int count = 0; count < length; count++)
            {
                Cycle cycle = _data[count];

                span = TimeSpan.FromSeconds(cycle.Start);  // in seconds

                if (span.TotalSeconds > _start.TotalSeconds)  // Offset in seconds
                {
                    if ((_end.TotalSeconds == 0) || (span.TotalSeconds < _end.TotalSeconds))
                    {
                        // Convert the cycle length from the sample size to T / 8 states based on the clock frequency.

                        double wavelength = cycle.End - cycle.Start;
                        double temp = wavelength * (double)_clockFrequency / 8;

                        UInt32 cycleLength = (UInt32)(Math.Round(temp));   // in clock cycles

                        int mode = 0;

                        if (mode == 0)
                        {
                            double newWavelength = (double)(cycleLength * 8) / _clockFrequency;
                            cycle.End = cycle.Start + newWavelength;
                            _data[count] = cycle;
                        }
                        else if (mode == 1)
                        {
                            // Test aligning to specific values

                            // S = 2400 = 0.0004166666 = 52
                            // L = 1200 = 0.0008333333 = 104
                            // M = (2400 + 1200) /2 = 1800 = 0.000555555

                            if (cycleLength < 50)
                            {
                                Debug.WriteLine("Too short offset={0} wavellength ={1}", cycle.Start, cycleLength);
                            }
                            else if ((cycleLength >= 50) && (cycleLength < 60))
                            {
                                cycleLength = 52;  // 
                            }
                            else if ((cycleLength >= 60) && (cycleLength < 80))
                            {
                                if (previous >= cycleLength)
                                {
                                    cycleLength = 52;
                                }
                                else
                                {
                                    cycleLength = 104;
                                }
                            }
                            else if ((cycleLength >= 80) && (cycleLength < 90))
                            {
                                if (previous <= wavelength)
                                {
                                    cycleLength = 104;
                                }
                                else
                                {
                                    cycleLength = 52;
                                }
                            }
                            else if ((cycleLength >= 90) && (cycleLength < 110))
                            {
                                cycleLength = 104;
                            }
                            else
                            {
                                Debug.WriteLine("Too long offset={0} wavellength ={1}", cycle.Start, cycleLength);
                            }

                            double newwavelength = (double)(cycleLength * 8) / _clockFrequency;
                            cycle.End = cycle.Start + newwavelength;
                            _data[count] = cycle;

                            previous = cycleLength;

                        }
                        else if (mode == 2)
                        {
                            // Test grouping to specific values

                            // S = 2400 = 0.0004166666 = 52
                            // L = 1200 = 0.0008333333 = 104

                            switch (cycleLength)
                            {
                                case 48: // 0x30
                                case 49: // 0x31
                                case 50: // 0x32
                                case 51: // 0x33
                                case 53: // 0x35
                                case 54: // 0x36
                                case 55: // 0x37
                                    {
                                        cycleLength = 52;
                                        break;
                                    }
                                case 102: // 0x66
                                case 103: // 0x67
                                case 105: // 0x69
                                case 106: // 0x6a
                                case 107: // 0x6b
                                case 108: // 0x6c
                                case 109: // 0x6d
                                    {
                                        cycleLength = 104;
                                        break;
                                    }
                                default:
                                    {
                                        cycleLength = 0;
                                        break;
                                    }
                            }

                            double newwavelength = (double)(cycleLength * 8) / _clockFrequency;
                            cycle.End = cycle.Start + newwavelength;
                            _data[count] = cycle;

                            previous = cycleLength;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Convert the wave file to a series of cycles by calulating
        /// the cycle length.
        /// </summary>
        public void Convert()
        {
            double max = double.MinValue;
            double min = double.MaxValue;
            bool rising = false;
            double previous = 0;
            double start = 0;
            double mid = 0;
            double end = 0;
            double interval = 1 / (double)_wave.SampleRate; // Cycles per second
            TimeSpan span;

            _data.Clear();
            int length = _wave.Length;
            for (long count = 0; count < length; count++)
            {
                span = TimeSpan.FromSeconds(count * interval);  // in seconds

                if (span.TotalSeconds >= _start.TotalSeconds)
                {
                    if ((_end.TotalSeconds == 0) || (span.TotalSeconds < _end.TotalSeconds))
                    {
                        double x = (double)_wave.Left[count];    // Assume that this is mono

                        // May need a threshold

                        if (x > previous)
                        {
                            rising = true;
                        }
                        else if (x == previous)
                        {
                            // Assume no change 
                        }
                        else
                        {
                            rising = false;
                        }

                        if (rising == true)
                        {
                            if ((previous < 0) && (x >= 0)) // Fix issue where x is actually zero
                            {
                                if (start == 0)
                                {

                                    Debug.WriteLine("Count=" + (count - 1) + " x=" + previous);

                                    // Start of pulse identified
                                    max = double.MinValue;
                                    min = double.MaxValue;
                                    start = count - 1 - previous / (-previous + x);    // in seconds

                                    Debug.WriteLine("start=" + span);

                                }
                                else if (mid > 0)
                                {

                                    Debug.WriteLine("Count=" + (count - 1) + " x=" + previous);
                                    
                                    // Start of pulse identified
                                    end = count - 1 - previous / (-previous + x);

                                    Debug.WriteLine("End=" + span);

                                    if ((start > 0) && (mid > 0))
                                    {
                                        if ((Math.Abs(max) > _threshold) || (Math.Abs(min) > _threshold))
                                        {
                                            // Store the cycle as proportions of the sample rate rather
                                            // than converting to seconds at this stage.

                                            if (start <= end)
                                            {
                                                _data.Add(new Cycle(start / _wave.SampleRate, mid / _wave.SampleRate, end / _wave.SampleRate, max, min));

                                                // What needs to happen is that start, mid and end are converted into seconds
                                                // from samples so that when we save there is a common format. Currently
                                                // reading a tap file and then saving it will do another conversion
                                                // 

                                                Debug.WriteLine("offset=" + span + " wavelength=" + (end - start) + " min=" + min + " max=" + max);
                                            }
                                            else
                                            {
                                                //throw new InvalidDataException();
                                            }
                                        }
                                        start = end;
                                        max = float.MinValue;
                                        min = float.MaxValue;

                                        Debug.WriteLine("new start=" + span);

                                        mid = 0;
                                        end = 0;
                                    }
                                }
                            }
                        }

                        if (rising == false)
                        {
                            if ((previous > 0) && (x <= 0)) // Fix issue where x is actually zero
                            {
                                if ((start > 0) && (mid == 0))
                                {

                                    Debug.WriteLine("Count=" + count + " x=" + x);

                                    mid = count - 1 + previous / (previous - x);

                                    Debug.WriteLine("Mid=" + span);

                                }
                            }
                        }

                        if (start > 0)
                        {
                            if (x > max)
                            {
                                max = x;
                            }
                            if (x < min)
                            {
                                min = x;
                            }

                            Debug.WriteLine("Time=" + span + " x=" + x + " Rising=" + rising);

                        }

                        previous = x;
                    }
                }
            }
        }

        public void Write(bool overwrite)
        {
            Write(_path, _name, overwrite);
        }

        /// <summary>
        /// Write the cycles into the tape format
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="overwrite"></param>
        public void Write(string path, string name, bool overwrite)
        {
            if (path == null)
            {
                path = _wave.Path;
            }

            // Remove any extension and assume it must be tap
            if (name != null)
            {
                int pos = name.LastIndexOf(".");
                if (pos > 0)
                {
                    name = name.Substring(0, pos);
                }
            }
            else
            { 
                name = _wave.Name;
            }

            string filenamePath = System.IO.Path.Combine(path, name) + ".tap";
            if (overwrite == true)
            {
                if (File.Exists(filenamePath) == true)
                {
                    File.Delete(filenamePath);
                }
            }

            BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath, FileMode.OpenOrCreate));
            binaryWriter.Seek(0, SeekOrigin.Begin); // Move to start of the file
            binaryWriter.Write(_header,0,12);       // Write the header (12)
            binaryWriter.Write(_version);           // Write the version (1)
            binaryWriter.Write(new byte[3]);        // Write optional future features (3) empty

            UInt32 length = (UInt32)_data.Count;
            binaryWriter.Write(length);             // Write the length (4) as unsigned integer

            TimeSpan span;

            // Temp

            byte previous = 0;

            for(int count=0; count<length; count++)
            {
                Cycle cycle = _data[count];
                span = TimeSpan.FromSeconds(cycle.Start);  // in seconds

                // Convert the cycle length from the sample size to T / 8 states based on the clock frequency.

                byte interval;
                double wavelength = cycle.End - cycle.Start;

                // Look at options to adjust the cycle length based on the amplitude of the wave.
                // It appears that the hugher cycles have a lower amplitude.
                // Need to check on rounding as hving issues converting forwards and backwards to get the same resule

                UInt32 cycleLength = (UInt32)(Math.Round(wavelength * _clockFrequency / 8));   // in clock cycles

                if (cycleLength > 255)
                {
                    interval = 0;
                    if (_version == 0)
                    {
                        binaryWriter.Write(interval);              // Write zero data to indicate long pulse
                    }
                    else
                    {
                        cycleLength = (UInt32)(wavelength * _clockFrequency);   // Remove 8bit scaling
                        byte[] bytes = BitConverter.GetBytes(cycleLength);      //
                        binaryWriter.Write(interval);                           // Write the data
                        binaryWriter.Write(bytes, 0, 3);                        // Only write 24 bits of data
                        length = (UInt16)(length + 3);
                    }
                }
                else
                {
                    interval = (byte)cycleLength;
                    previous = interval;
                    binaryWriter.Write(interval);              // Write the data
                }
            }

            binaryWriter.Seek(16, SeekOrigin.Begin);    // Move relative to start of the file
            binaryWriter.Write(length);                 // Rewrite the length as there may have been some long pulses

            binaryWriter.Flush();
            binaryWriter.Close();
            binaryWriter.Dispose();
        }

        public void Read()
        {
            Read(_path, _name);
        }

        /// <summary>
        /// Read the tape format file into a cycle format
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public int Read(string path, string name)
        {
            int errorCode = 1;

            // Remove any extension and assume it must be Tap
            if (name != null)
            {
                int pos = name.LastIndexOf(".");
                if (pos > 0)
                {
                    name = name.Substring(0, pos);
                }
            }
            else
            {
                name = _wave.Name;
            }

            string filenamePath = System.IO.Path.Combine(path, name) + ".tap";
            BinaryReader binaryReader = null;

            try
            {
                binaryReader = new BinaryReader(new FileStream(filenamePath, FileMode.Open));
                binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
                char[] header = binaryReader.ReadChars(12);
                if (_header.ToString() == header.ToString())
                {
                    binaryReader.BaseStream.Seek(12, SeekOrigin.Begin);
                    _version = binaryReader.ReadByte();
                    byte[] optional = binaryReader.ReadBytes(3);
                    _length = binaryReader.ReadUInt32();
                    _data.Clear();

                    double start = 0;
                    byte previous = 0;
                    for (int count = 0; count < _length; count++)
                    {
                        byte interval = binaryReader.ReadByte();
                        double cycles = 0;

                        if (interval == 0)
                        {
                            if (_version == 0)
                            {
                                // Assume interval is large so cycles tends to 256
                                cycles = (double)(256 * 8);
                                cycles = (double)(cycles / _clockFrequency);
                            }
                            else
                            {
                                byte[] bytes = new byte[4];
                                binaryReader.ReadBytes(3).CopyTo(bytes, 0);
                                cycles = BitConverter.ToInt16(bytes);
                                cycles = (double)(cycles / _clockFrequency);
                            }
                        }
                        else
                        {
                            if (count == (91072)) // 91092 - 20
                            {
                                interval = 104;
                            }

                            cycles = interval * 8;
                            cycles = (double)(cycles / _clockFrequency);
                        }

                        Cycle cycle = new Cycle();
                        cycle.Start = start;
                        start = start + cycles;     // this is in seconds now not samples
                        cycle.End = start;
                        _data.Add(cycle);
                    }
                    errorCode = 0;

                }
            }
            catch
            {
                errorCode = 1;
            }

            if (binaryReader != null)
            {
                binaryReader.Close();
                binaryReader.Dispose();
            }

            return (errorCode);
        }


        public IEnumerator GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        #endregion
        #region Private
        #endregion
    }
}
