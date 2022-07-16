using System;
using System.Collections.Generic;
using System.Text;

namespace TapeLibrary
{
    public class Packet
    {
        Parity _parity = Parity.None;
        StopBits _stopBits = StopBits.One;
        StartBits _startBits = StartBits.None;
        int _dataBits = 8;
        byte _data = 0;
        Error _state = Error.None;
        int _totalBits = 8;

        int _index = 0;
        int _start = 0;
        int _stop = 0;

        public enum Parity
        {
            None,
            Odd,
            Even,
            Mark,
            Space
        }

        public enum StopBits
        {
            None,
            One,
            Two,
            OnePointFive
        }

        public enum StartBits
        {
            None,
            Mark,
            Space
        }

        public enum Error : int
        {
            None = 0,
            Start = -1,
            Stop = -2,
            Unknown = -3,
            Overrun = -4,
        }

        public Packet(Parity parity, StartBits startBits, StopBits stopBits, int dataBits)
        {
            _parity = parity;
            _stopBits = stopBits;
            _startBits = startBits;
            _dataBits = dataBits;

            if (_startBits != StartBits.None) 
            {
                _start = 1;
            }
            if (_stopBits == StopBits.One)
            {
                _stop = 1;
            }
            else if (_stopBits == StopBits.Two)
            {
                _stop = 2;
            }
            _totalBits = _start + _dataBits + _stop;

        }

        public int Index
        {
            get { return _index; }
        }

        public bool IsComplete
        {
            get
            {
                if (_index == _totalBits)
                {
                    return (true);
                }
                else
                {
                    return (false);
                }
            }
        }

        public bool IsError
        {
            get
            {
                if (_state < 0)
                {
                    return (true);
                }
                else
                {
                    return (false);
                }
            }
        }

        public string ErrorDescription
        {
            get
            {
                if (_state == Error.Start)
                {
                    return ("No start bit");
                }
                else if (_state == Error.Stop)
                {
                    return ("No stop bit(s)");
                }
                else if (_state == Error.Overrun)
                {
                    return ("Data overrrun");
                }
                else
                {
                    return ("");
                }
            }
        }

        public void Clear()
        {
            _index = 0;
            _data = 0;
        }

        public byte Get()
        {
            return (_data);
        }

        public void Add(bool bit)
        {
            // The format should be
            // Start bits
            // Data bits
            // Stop bits

            _state = Error.Unknown;

            if (_index < _start)
            {
                if (_startBits == StartBits.Mark)
                {
                    if (bit == false)
                    {
                        _state = Error.Start; ;  // Incorrect bit
                    }
                    else
                    {
                        _state = Error.None; // Start bit
                    }
                }
                else if (_startBits == StartBits.Space)
                {
                    if (bit == true)
                    {
                        _state = Error.Start;  // Incorrect bit
                    }
                    else
                    {
                        _state = Error.None; // Start bit
                    }
                }
                _index++;
            }
            else if ((_index >= _start) && (_index < (_start + _dataBits)))
            {
                _data = (byte)(_data >> 1);
                if (bit == true)
                {
                    _data = (byte)(_data | 128);
                }
                _index++;
                _state = Error.None;
            }
            else if ((_index >= (_start + _dataBits)) && (_index < (_start + _dataBits + _stop)))
            {
                if (bit == true)
                {
                    _state = Error.None;
                }
                else
                {
                    _state = Error.Stop;
                }
                _index++;
            }
            else if (_index == _totalBits)
            {
                _state = Error.None;
            }
            else if (_index > _totalBits)
            {
                _state = Error.Overrun;
            }
        }
    }
}
