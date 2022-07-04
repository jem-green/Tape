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

            Command histogram = new Command("histogram", "Generate histogram")
            {
                Handler = CommandHandler.Create<string, int, int, double, double>(Histogram)
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

            root.Add(histogram);
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

        static void Histogram(string filename, int version, int frequency, double start, double end)
        {
            string path = Path.GetDirectoryName(filename);
            string name = Path.GetFileName(filename);
            Tape tape = new Tape
            {
                Version = version
            };
            tape.Read(path, name);

            Dictionary<int, long> histogram = new Dictionary<int, long>();
            for (int i = 0; i <= 256; i++)
            {
                histogram.Add(i, 0);
            }

            for (int count = 0; count < tape.Count; count++)
            {
                if (tape[count].Start > start)
                {
                    if ((end == 0) || (tape[count].Start < end))
                    {
                        double length = tape[count].Length;
                        length = length * frequency / 8;
                        int interval = (int)length;
                        if (interval > 256)
                        {
                            try
                            {
                                histogram.Add(interval, 0);
                            }
                            catch { }
                        }
                        histogram[interval] = histogram[interval] + 1;
                    }
                }
            }

            for (int interval = 0; interval < histogram.Count; interval++)
            {
                long count = histogram[interval];
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
            tape.Convert();
            tape.Write(path, name, true);
        }
    }
}
