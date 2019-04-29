using DMISharp;
using CommandLine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine.Text;

namespace DMIExtract
{
    class Program
    {
        /// <summary>
        /// Uesd by CommandLineParser for parsing CLI options and values, as well as providing usage examples.
        /// </summary>
        public class Options
        {
            [Option('o', "output", Default = "out", HelpText = "Specify root directory to output files into")]
            public string RootOutputDirectory { get; set; }
            [Option('p', "png", HelpText = "Enable exporting PNG files of directions and their frames")]
            public bool ExportPNG { get; set; }
            [Option('g', "gif", HelpText = "Enable exporting GIF files of directions and their frames")]
            public bool ExportGIF { get; set; }
            [Option('d', "nodmi", HelpText = "Disable exporting files to individual folders per DMI file")]
            public bool NoDMIFolders { get; set; }
            [Option('f', "noformat", HelpText = "Disable exporting files to seperate PNG and GIF folders")]
            public bool NoFormatFolders { get; set; }
            [Value(0, Required = true, HelpText = "Specify a file or files to export from.", MetaName = "File[s]")]
            public IEnumerable<string> Files { get; set; }

            [Usage(ApplicationAlias = "DMIExtract.exe")]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    yield return new Example("Extract all frames and animations from DMI file", new Options { Files = new List<string>() { "input.dmi" }, ExportGIF = true, ExportPNG = true });
                    yield return new Example("Extract only animations from some DMI files", new Options { Files = new List<string>() { "inputA.dmi", "inputB.dmi" }, ExportGIF = true });
                    yield return new Example("Extract only frames from a DMI file without format folders [./png/..., ./gif/...]", new Options { Files = new List<string>() { "input.dmi" }, ExportPNG = true, NoFormatFolders = true });
                    yield return new Example("Extract animations from some DMI files, without individual DMI folders ['bulk export']", new Options { Files = new List<string>() { "inputA.dmi", "inputB.dmi" }, ExportGIF = true, NoDMIFolders = true });
                }
            }
        }

        /// <summary>
        /// Program entry point, parses the command line arguements.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>An integer reprsenting the exit status of the program, with non-zero values indicating an error.</returns>
        static int Main(string[] args)
        {
            int errorCode = 0;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts =>
                {
                    errorCode = Run(opts);
                });

            return errorCode;
        }


        /// <summary>
        /// Performs the requested operations on DMI files.
        /// </summary>
        /// <param name="args">Parsed arguments from the CLI</param>
        /// <returns>An integer reprsenting the exit status of the program, with non-zero values indicating an error.</returns>
        public static int Run(Options args)
        {
            // Check for validity of output directory
            if (!Directory.Exists(args.RootOutputDirectory))
            {
                try
                {
                    Directory.CreateDirectory(args.RootOutputDirectory);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[CRITICAL] Failed to create directory {args.RootOutputDirectory}");
                    throw e;
                }
            }
            
            // Check for validity of file[s]
            foreach (var file in args.Files)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"[ERROR] File does not exist: {file}");
                    return 1;
                }
                else if (!file.EndsWith(".dmi"))
                {
                    Console.WriteLine($"[ERROR] Non-DMI file extension: {file}");
                    return 2;
                }
            }

            // Iterate through each file to process.
            foreach (var file in args.Files)
            {
                // Develop the filename and base directory (root dir/[filename/])
                var fname = file.Split(new char[] { '/', '\\' }).Last().Split('.').First();
                var outputBase = $"{(args.RootOutputDirectory.TrimEnd(new char[] { '\\', '/' }).Replace('\\', '/'))}{(!args.NoDMIFolders ? $"/{fname}" : "")}";

                // Ensure PNG directory exists if required.
                if (!args.NoFormatFolders && args.ExportPNG)
                {
                    Directory.CreateDirectory($"{outputBase}/png/");
                }

                // Safely handle the DMIFile object to allow for proper disposal.
                using (var dmi = new DMIFile(file))
                {
                    // Iterate through each state [icon] in the DMI file.
                    foreach (var state in dmi.States)
                    {
                        // Exporting PNGs
                        if (args.ExportPNG)
                        {
                            if (state.Frames == 1)
                            {
                                if (state.Dirs == 1)
                                {
                                    state.Images[0, 0].Save($"{outputBase}{(!args.NoFormatFolders ? "/png" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}.png");
                                }
                                else
                                {
                                    for (int dir = 0; dir < state.Dirs; dir++)
                                    {
                                        state.Images[dir, 0].Save($"{outputBase}{(!args.NoFormatFolders ? "/png" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}_D{dir}.png");
                                    }
                                }
                            }
                            else
                            {
                                if (state.Dirs == 1)
                                {
                                    for (int frame = 0; frame < state.Frames; frame++)
                                    {
                                        state.Images[0, frame].Save($"{outputBase}{(!args.NoFormatFolders ? "/png" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}_F{frame}.png");
                                    }
                                }
                                else
                                {
                                    for (int dir = 0; dir < state.Dirs; dir++)
                                    {
                                        for (int frame = 0; frame < state.Frames; frame++)
                                        {
                                            state.Images[dir, frame].Save($"{outputBase}{(!args.NoFormatFolders ? "/png" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}_D{dir}_F{frame}.png");
                                        }
                                    }
                                }
                            }
                        }

                        // Exporting GIFs
                        if (args.ExportGIF && state.IsAnimated())
                        {
                            // Ensure GIF directory exists if required.
                            if (!args.NoFormatFolders) Directory.CreateDirectory($"{outputBase}/gif/");
                            var gifs = state.GetAnimated();
                            var encoder = new GifEncoder()
                            {
                                Quantizer = new OctreeQuantizer(false) // Disable dithering as this generally negatively impacts pixelart animations.
                            };

                            if (gifs.Length == 1)
                            {
                                gifs[0].Save($"{outputBase}{(!args.NoFormatFolders ? "/gif" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}.gif");
                                gifs[0].Dispose();
                            }
                            else
                            {
                                for (int dir = 0; dir < state.Dirs; dir++)
                                {
                                    gifs[dir].Save($"{outputBase}{(!args.NoFormatFolders ? "/gif" : "")}{(args.NoDMIFolders ? $"/{fname}_" : "/")}{state.Name}_D{dir}.gif");
                                    gifs[dir].Dispose();
                                }
                            }
                        }
                    }
                }
            }

            return 0;
        }
    }
}
