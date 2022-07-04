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
        int _state = -1;

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
        }

        public bool IsComplete
        {
            get
            {
                if (_state == 1)
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

        public string Error
        {
            get
            {
                if (_state == -1)
                {
                    return ("No start bit");
                }
                else if (_state == -2)
                {
                    return ("No stop bit");
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

            _state = -1;

            if (_index < _start)
            {
                if (_startBits == StartBits.Mark)
                {
                    if (bit == false)
                    {
                        _state = -1;  // Incorrect bit
                    }
                    else
                    {
                        _state = 0; // Start bit
                    }
                }
                else if (_startBits == StartBits.Space)
                {
                    if (bit == true)
                    {
                        _state = -1;  // Incorrect bit
                    }
                    else
                    {
                        _state = 0; // Start bit
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
                _state = 0;
            }
            else if ((_index >= (_start + _dataBits)) && (_index < (_start + _dataBits + _stop)))
            {
                if (bit == true)
                {
                    _state = 0;
                }
                else
                {
                    _state = -2;
                }
                _index++;
            }

            if (_index == (_start + _dataBits + _stop))
            {
                if (_state == 0)
                {
                    _state = 1;
                }
            }

        }

    }
}
