using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TapeLibrary
{
    public class Wave
    {
        #region Fields

        private float[] _left;
        private float[] _right;
        private string _path;
        private string _name;
        private int _sampleRate = 0;
        private int _channelCount = 0;

        #endregion
        #region Constructor
        public Wave()
        {
        }

        public Wave(string path, string name)
        {
            _path = path;
            _name = name;
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

        #endregion
        #region Properties

        public int Channels
        {
            get
            {
                return(_channelCount);
            }
        }

        public string Name
        {
            set
            {
                _name = value;
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

        public int SampleRate
        {
            get
            {
                return _sampleRate;
            }
        }

        public float[] Left
        {
            get
            {
            return(_left);
            }
        }

        public float[] Right
        {
            get
            {
                return (_right);
            }
        }

        public int Length
        {
            get
            {
                return (_left.Length);
            }
        }

        #endregion
        #region Methods
        public bool Read()
        {
            return (Read(_path, _name));
        }

        public bool Read(string path, string name)
        {
            bool read = true;
            try
            {
                // Remove any extension and assume it must be wav
                if (name != null)
                {
                    int pos = _name.LastIndexOf(".");
                    if (pos > 0)
                    {
                        name = name.Substring(0, pos);
                    }
                }
                string filenamePath = System.IO.Path.Combine(path, name) + ".wav";
                using (FileStream fs = File.Open(filenamePath, FileMode.Open))
                {
                    BinaryReader reader = new BinaryReader(fs);

                    // chunk 0
                    int data = reader.ReadInt32();
                    byte[] buf = BitConverter.GetBytes(data);
                    string chunkID = System.Text.Encoding.ASCII.GetString(buf);
                    if (chunkID == "RIFF") // Means little Endian
                    {
                        int fileSize = reader.ReadInt32();
                        int riffType = reader.ReadInt32();

                        // chunk 1
                        int fmtID = reader.ReadInt32();
                        int fmtSize = reader.ReadInt32(); // bytes for this chunk (expect 16 or 18)

                        // 16 bytes coming...
                        int fmtCode = reader.ReadInt16();
                        int channels = reader.ReadInt16();
                        _sampleRate = reader.ReadInt32();
                        int byteRate = reader.ReadInt32();
                        int fmtBlockAlign = reader.ReadInt16();
                        int bitDepth = reader.ReadInt16();

                        if (fmtSize == 18)
                        {
                            // Read any extra values
                            int fmtExtraSize = reader.ReadInt16();
                            reader.ReadBytes(fmtExtraSize);
                        }

                        // chunk 2
                        int dataID = reader.ReadInt32();
                        int bytes = reader.ReadInt32();

                        // DATA!
                        byte[] byteArray = reader.ReadBytes(bytes);

                        int bytesForSamp = bitDepth / 8;
                        int nValues = bytes / bytesForSamp;


                        float[] asFloat = null;
                        switch (bitDepth)
                        {
                            case 64:
                                {
                                    Int64[] asInt64 = new Int64[nValues];
                                    Buffer.BlockCopy(byteArray, 0, asInt64, 0, bytes);
                                    asFloat = Array.ConvertAll(asInt64, e => e / (float)Int64.MaxValue);
                                    break;
                                }
                            case 32:
                                {
                                    Int32[] asInt32 = new Int32[nValues];
                                    Buffer.BlockCopy(byteArray, 0, asInt32, 0, bytes);
                                    //asFloat = Array.ConvertAll(asInt32, e => e / (float)Int32.MaxValue);
                                    asFloat = Array.ConvertAll(asInt32, e => (float)e);
                                    break;
                                }
                            case 16:
                                {
                                    Int16[] asInt16 = new Int16[nValues];
                                    Buffer.BlockCopy(byteArray, 0, asInt16, 0, bytes);
                                    asFloat = Array.ConvertAll(asInt16, e => e / (float)(Int16.MaxValue + 1));
                                    break;
                                }
                            case 8:
                                {
                                    sbyte[] asByte = new sbyte[nValues];
                                    Buffer.BlockCopy(byteArray, 0, asByte, 0, bytes);
                                    asFloat = Array.ConvertAll(asByte, e => e / (float)(byte.MaxValue + 1));
                                    break;
                                }
                            default:
                                {
                                    read = false;
                                    break;
                                }
                        }

                        switch (channels)
                        {
                            case 1:
                                {
                                    _left = asFloat;
                                    _right = null;
                                    read = true;
                                    break;
                                }
                            case 2:
                                {
                                    // de-interleave
                                    int nSamps = nValues / 2;
                                    _left = new float[nSamps];
                                    _right = new float[nSamps];
                                    for (int s = 0, v = 0; s < nSamps; s++)
                                    {
                                        _left[s] = asFloat[v++];
                                        _right[s] = asFloat[v++];
                                    }
                                    read = true;
                                    break;
                                }
                            default:
                                {
                                    read = false;
                                    break;
                                }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Not a WAV file");
                        read = false;
                    }
                }
            }
            catch
            {
                Debug.WriteLine("...Failed to load: " + name);
                read = false;
            }

            return (read);
        }
    }
    #endregion
    #region Private
    #endregion
}
