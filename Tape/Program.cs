using System;
using System.Diagnostics;
using System.IO;
using TapeLibrary;
using System.CommandLine;
using System.CommandLine.Parser;
using System.CommandLine.Invocation;
using System.Collections.Generic;

namespace TapeConsole
{
    /*
     * TAP filenamepath.extension [options]
     * --version -> 0 or 1
     * --cycles -> in Hz defaults to 1MHz
     * --name -> filename
     * --path -> filepath
     * --output -> output file name .tap extension assumes same path as input
     * --start -> offset to the start of data in seconds
     * --end -> offset to the end of data in seconds
     */

    #region Fields
    #endregion

    public class Program
    {
        static int Main(string[] args)
        {
            var root = new RootCommand
                {
                    new Argument<string>("filename"),
                    new Option<int?>(new string[] { "--version", "/V" }, getDefaultValue: () => 0, "The tape version"),
                    new Option<int?>(new string[] { "--frequency", "/F"}, getDefaultValue: () => 1000000, "The processor frequency in Hz"),
                    //new Option<string?>(new string[] { "--name", "/p" }, "The filename"),
                    //new Option<string?>(new string[] { "--path", "/n" }, "The file path")
                    new Option<double?>(new string[] {"--start", "/S"}, getDefaultValue: () => 0, "Offset to start of data in seconds"),
                    new Option<double?>(new string[] {"--end", "/E"}, getDefaultValue: () => 0, "Offset to end of data in seconds"),
            };

            Command wavelength = new Command("wavelength", "Generate wavelength histogram")
            {
                Handler = CommandHandler.Create<string, int, int, double, double>(Wavelength)
            };

            Command amplitude = new Command("amplitude", "Generate amplitude histogram")
            {
                Handler = CommandHandler.Create<string, int, int, double, double>(Amplitude)
            };

            Command code = new Command("code", "Generate ascii format file")
            {
                new Option<int?>(new string[] { "--baud","/B" },  getDefaultValue: () => 297, "Baud rate"),
                new Option<int>(new string[] { "--output", "/O" }, description: "The Output filename"),
            };
            code.Handler = CommandHandler.Create<string, int, int, double, double, int>(Tap2Code);

            Command tape = new Command("tape", "Generate tape format file")
            {
                new Option<int>(new string[] { "--output", "/O" }, description: "The Output filename"),
            };
            tape.Handler = CommandHandler.Create<string, int, int, double, double>(Wave2Tape);

            root.Add(wavelength);
            root.Add(amplitude);
            root.Add(code);
            root.Add(tape);

            return root.Invoke(args);

        }

        static void Tap2Code(string filename, int version, int frequency, double start, double end, int baud)
        {
            string path = Path.GetDirectoryName(filename);
            string name = Path.GetFileName(filename);
            Tape tape = new Tape
            {
                Version = version,
                Start = new TimeSpan(Convert.ToInt64(1e7 * start)),  // Should now be in ticks from seconds
                End = new TimeSpan(Convert.ToInt64(1e7 * end))      // Should now be in ticks from seconds
            };
            tape.Read(path, name);
            TapeLibrary.Code program = new TapeLibrary.Code(tape)
            {
                BaudRate = baud
            };
            program.Convert();
            program.Write(path, name, true);

        }

        static void Amplitude(string filename, int version, int frequency, double start, double end)
        {
            string path = Path.GetDirectoryName(filename);
            string name = Path.GetFileName(filename);
            Wave wave = new Wave(path, name);
            wave.Read();

            Tape tape = new Tape(wave)
            {
                Start = new TimeSpan(Convert.ToInt64(1e7 * start)),     // Should now be in ticks from seconds
                End = new TimeSpan(Convert.ToInt64(1e7 * end)),         // Should now be in ticks from seconds
                Version = version
            };
            tape.Analyse();

            SortedDictionary<int, double> histogram = new SortedDictionary<int, double>();
            for (int i = 0; i <= 256; i++)
            {
                histogram.Add(i, 0);
            }

            // The sample range is already limited by start and end

            for (int count = 0; count < tape.Count; count++)
            {
                double amplitude = Math.Abs(tape[count].Maximum - tape[count].Minimum);
                double wavelength = (tape[count].End - tape[count].Start) / wave.SampleRate;
                UInt32 cycleLength = (UInt32)(wavelength * frequency / 8);   // in clock cycles

                if ((cycleLength > 255) || (cycleLength < 0))
                {
                    try
                    {
                        histogram[(int)cycleLength] = (histogram[(int)cycleLength] + amplitude) / 2;
                    }
                    catch
                    {
                        histogram.Add((int)cycleLength, amplitude);
                    }
                }
                else
                {
                    byte interval = (byte)cycleLength;
                    if (histogram[interval] != 0)
                    {
                        histogram[interval] = (histogram[interval] + amplitude) / 2;
                    }
                    else
                    {
                        histogram[interval] = amplitude;
                    }
                }
            }

            foreach (KeyValuePair<int, double> kvp in histogram)
            {
                int interval = kvp.Key;
                double amplitude = kvp.Value;
                if (amplitude != 0)
                {
                    Console.WriteLine(interval.ToString("000") + " " + amplitude);
                }
            }

        }

        static void Wavelength(string filename, int version, int frequency, double start, double end)
        {
            string path = Path.GetDirectoryName(filename);
            string name = Path.GetFileName(filename);
            Tape tape = new Tape
            {
                Start = new TimeSpan(Convert.ToInt64(1e7 * start)),     // Should now be in ticks from seconds
                End = new TimeSpan(Convert.ToInt64(1e7 * end)),         // Should now be in ticks from seconds
                Version = version
            };
            tape.Read(path, name);

            Dictionary<int, long> histogram = new Dictionary<int, long>();
            for (int i = 0; i <= 256; i++)
            {
                histogram.Add(i, 0);
            }

            // The sample range is already limited by start and end

            for (int count = 0; count < tape.Count; count++)
            {
                double wavelength = (tape[count].End - tape[count].Start);
                UInt32 cycleLength = (UInt32)(wavelength * frequency / 8);   // in clock cycles

                if ((cycleLength > 255) || (cycleLength < 0))
                {
                    try
                    {
                        histogram[(int)cycleLength] = histogram[(int)cycleLength] + 1;
                    }
                    catch 
                    {
                        histogram.Add((int)cycleLength, 1);
                    }
                }
                else
                {
                    byte interval = (byte)cycleLength;
                    histogram[interval] = histogram[interval] + 1;
                }
            }

            foreach (KeyValuePair<int, long> kvp in histogram)
            {
                int interval = kvp.Key;
                long count = kvp.Value;
                if (count != 0)
                {
                    Console.WriteLine(interval.ToString("000") + " " + count);
                }
            }
        }

        static void Wave2Tape(string filename, int version, int frequency, double start, double end)
        {
            string path = Path.GetDirectoryName(filename);
            string name = Path.GetFileName(filename);
            Wave wave = new Wave(path, name);
            wave.Read();
            Tape tape = new Tape(wave)
            {
                Start = new TimeSpan(Convert.ToInt64(1e7 * start)),     // Should now be in ticks from seconds
                End = new TimeSpan(Convert.ToInt64(1e7 * end)),         // Should now be in ticks from seconds
                Version = version
            };
            tape.Analyse();
            tape.Write(path, name, true);
        }
    }
}
