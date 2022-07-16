using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;
using System.IO;

namespace TapeLibrary
{
    public class Code
    {
        #region Fields

        private Tape _tape;
        private string _path = null;
        private string _name = null;
        private TimeSpan _start = new TimeSpan(0);  // In seconds
        private TimeSpan _end = new TimeSpan(0);    // In seconds
        private List<byte> _data;
        private int _baudRate = 300;                // In Hz

        private enum State
        {
            None = -1,
            Noise = 0,
            Header = 1,
            Data = 2
        }

        #endregion
        #region Constructors
        public Code(Tape tape)
        {
            _tape = tape;
            _data = new List<byte>();
        }

        public Code(string path, string name)
        {
            _path = path;
            _name = name;
            if (_name != null)
            {
                int pos = _name.LastIndexOf(".");
                if (pos > 0)
                {
                    _name = _name.Substring(0, pos);
                }
            }
            _data = new List<byte>();
        }

        public Code(Tape tape, string path, string name)
        {
            _tape = tape;
            _path = path;
            _name = name;
            if (_name != null)
            {
                int pos = _name.LastIndexOf(".");
                if (pos > 0)
                {
                    _name = _name.Substring(0, pos);
                }
            }
            _data = new List<byte>();
        }

        #endregion
        #region Properties

        public int BaudRate
        {
            get
            {
                return _baudRate;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("BaudRate", "ArgumentOutOfRange_NeedPosNum");
                }
                _baudRate = value;
            }
        }

        public Tape Tape
        {
            get
            {
                return (_tape);
            }
            set
            {
                _tape = value;
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

        #endregion
        #region Methods

        /// <summary>
        /// Convert the tape file to a ASCII file
        /// </summary>
        public void Convert()
        {
            //TapeLibrary.Packet packet = new TapeLibrary.Packet(Packet.Parity.None, Packet.StartBits.Space, Packet.StopBits.Two, 8);
            TapeLibrary.Packet packet = new TapeLibrary.Packet(Packet.Parity.None, Packet.StartBits.Space, Packet.StopBits.None, 8);

            packet.Clear();
            _data.Clear();
            State state;
            double headerLength = 0;

            byte data = 0;
            double start = 0;
            double interval = 1 / (double)_baudRate; // This is in seconds

            state = State.None;

            for (int count = 0; count < _tape.Count; count++)
            {
                if (_tape[count].Start > _start.TotalSeconds)  // Offset in seconds
                {
                    if ((_end.TotalSeconds == 0) || (_tape[count].Start < _end.TotalSeconds))
                    {

                        double sum = 0;     // Duration in seconds
                        int cycle = 0;      // 

                        // read forwards from count

                        double previous = 0;
                        double average = 0;

                        for (int i = 0; i < _tape.Count - count; i++)
                        {
                            Debug.WriteLine("Interval {0}", i);

                            start = _tape[count + i].Start;                 // This should be in seconds
                            double length = _tape[count + i].End - start;   // This will be in seconds
                            if (average != 0)
                            {
                                average = (average + length) / 2;
                            }
                            else
                            {
                                average = length;
                            }

                            if (length == 0)
                            {
                                Debug.WriteLine("Data error");
                                break;
                            }
                            else
                            {
                                Debug.WriteLine("Count={0} Start={1} i={2} Length={3} sum={4}", count + i + 20, start, i, length, sum);
                                if ((sum + length) <= interval)
                                {
                                    sum = sum + length;
                                    cycle = cycle + 1;
                                }
                                else
                                {
                                    if ((sum + length - interval - average / 32) < 0) // average / 32)
                                    {
                                        sum = sum + length;
                                        cycle = cycle + 1;
                                    }
                                    else
                                    {
                                        Debug.WriteLine("Too long Length={0} > {1}", sum + length, interval);
                                        break;
                                    }
                                }
                            }
                            previous = length;
                        }

                        Debug.WriteLine("Cycles=" + cycle + " Sum=" + sum);

                        if (cycle == 4)
                        {
                            if (state != State.None)
                            {
                                Debug.WriteLine("Bit=0");
                            }
                            packet.Add(false);  // Add a zero bit to the packet
                            if (packet.IsError == false)
                            {
                                if (packet.IsComplete == true)
                                {
                                    data = packet.Get();    // Get the byte
                                    _data.Add(data);
                                    packet.Clear();
                                    Debug.WriteLine("Byte=" + data);
                                    Console.WriteLine("Byte=" + data);
                                }
                            }
                            else
                            {
                                if (state != State.None)
                                {
                                    Debug.WriteLine("Error " + packet.ErrorDescription);
                                    Console.WriteLine("Error " + packet.ErrorDescription);
                                }
                                packet.Clear(); // No start bit

                            }
                            count = count + 3;
                        }
                        else if (cycle == 8)
                        {
                            if (state != State.None)
                            {
                                Debug.WriteLine("Bit=1");
                            }
                            packet.Add(true);    // Add a one bit to the packet
                            if (packet.IsError == false)
                            {
                                state = State.Data;
                                if (packet.IsComplete == true)
                                {
                                    data = packet.Get();
                                    _data.Add(data);
                                    packet.Clear();
                                    Debug.WriteLine("Byte=" + data);
                                    Console.WriteLine("Byte=" + data);
                                }
                            }
                            else
                            {
                                if (state == State.Data)
                                {
                                    Debug.WriteLine("Error " + packet.ErrorDescription);
                                    Console.WriteLine("Error " + packet.ErrorDescription);
                                }
                                if (state == State.None)
                                {
                                    headerLength = headerLength + 1;
                                    if (headerLength > 8)
                                    {
                                        state = State.Header;
                                    }
                                }
                                packet.Clear(); // No start bit
                            }
                            count = count + 7;
                        }
                        else
                        {
                            if (state != State.None)
                            {
                                Debug.WriteLine("Noise");
                                Console.WriteLine("Noise");
                                Debug.WriteLine("average={0}", average);
                            }
                            if (state == State.Header)
                            {
                                state = State.None;
                                headerLength = 0;
                            }
                        }
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
                path = _tape.Path;
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
                name = _tape.Name;
            }

            string filenamePath = System.IO.Path.Combine(path, name) + ".bas";
            if (overwrite == true)
            {
                if (File.Exists(filenamePath) == true)
                {
                    File.Delete(filenamePath);
                }
            }

            BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath, FileMode.OpenOrCreate));
            UInt32 length = (UInt32)_data.Count;
            for (int count = 0; count < length; count++)
            {
                binaryWriter.Write(_data[count]);
            }

            binaryWriter.Flush();
            binaryWriter.Close();
            binaryWriter.Dispose();
        }

        #endregion
    }
}
