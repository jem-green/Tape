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
        private TimeSpan _start = new TimeSpan(0);
        private TimeSpan _end = new TimeSpan(0);
        private List<byte> _data;
        private int _baudRate = 300;

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
            TapeLibrary.Packet packet = new TapeLibrary.Packet(Packet.Parity.None, Packet.StartBits.Space, Packet.StopBits.Two, 8);
            packet.Clear();
            _data.Clear();

            byte data = 0;

            double start = 0;
            double interval = 1 / (double)_baudRate; // This is in seconds

            for (int count = 0; count < _tape.Count; count++)
            {
                if (_tape[count].Start > _start.TotalSeconds)  // Offset in seconds
                {
                    if ((_end.TotalSeconds == 0) || (_tape[count].Start < _end.TotalSeconds))
                    {

                        double sum = 0; // Duration in seconds
                        int cycle = 0;

                        for (int i = 0; i < _tape.Count - count; i++)
                        {
                            start = _tape[count + i].Start;              // This should be in seconds
                            double length = _tape[count + i].Length;     // This should already be in seconds
                            if (length == 0)
                            {
                                Debug.WriteLine("Data error");
                                break;
                            }
                            else
                            {
                                Debug.WriteLine("Count={0} Start={1} i={2} Length={3} sum={4}", count + i + 20, start, i, length, sum);
                                if (sum + length < interval)
                                {
                                    sum = sum + _tape[count + i].Length;
                                    cycle = cycle + 1;
                                }
                                else
                                {
                                    Debug.WriteLine("Too long Length={0}", sum + _tape[count + i].Length);
                                    break;
                                }
                            }
                        }
                        Debug.WriteLine("Cycles=" + cycle + " Sum=" + sum);
                        if (cycle == 4)
                        {
                            Debug.WriteLine("Bit=0");
                            Console.WriteLine("Bit = 0");
                            packet.Add(false);
                            if (packet.IsError == false)
                            {
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
                                Debug.WriteLine("Error " + packet.Error);
                                Console.WriteLine("Error " + packet.Error);
                                packet.Clear(); // No start bit
                            }
                            count = count + 3;
                        }
                        else if (cycle == 8)
                        {
                            Console.WriteLine("Bit = 1");
                            Debug.WriteLine("Bit=1");
                            packet.Add(true);
                            if (packet.IsError == false)
                            {
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
                                Debug.WriteLine("Error " + packet.Error);
                                Console.WriteLine("Error " + packet.Error);
                                packet.Clear(); // No start bit
                            }
                            count = count + 7;
                        }
                        else
                        {
                            Debug.WriteLine("Noise");
                            Console.WriteLine("Noise");
                        }
                    }
                }
            }
            #endregion

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
    }
}
