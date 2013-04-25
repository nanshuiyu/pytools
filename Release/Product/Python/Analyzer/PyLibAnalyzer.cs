﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    internal class PyLibAnalyzer {
        private const string AnalysisLimitsKey = "Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion + "\\PythonTools\\Analysis\\StandardLibrary";

        private readonly string[] _dirs;
        private readonly string _indir;
        private readonly Guid _id;
        private readonly PythonLanguageVersion _version;

        private PyLibAnalyzer(List<string> dirs, string indir, Guid id, PythonLanguageVersion version) {
            _dirs = dirs.ToArray();
            _indir = indir;
            _id = id;
            _version = version;
        }

        public static int Main(string[] args) {
            List<string> dirs = new List<string>();
            PythonLanguageVersion version = PythonLanguageVersion.V27;
            string outdir = Environment.CurrentDirectory;
            string indir = Assembly.GetExecutingAssembly().Location;
            Guid id = Guid.Empty;
            for (int i = 0; i < args.Length; i++) {
                if (args[i].StartsWith("/") || args[i].StartsWith("-")) {
                    switch (args[i].Substring(1).ToLower()) {
                        case "dir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing directory after {0}", args[i]);
                                return -1;
                            }
                            dirs.Add(args[i + 1]);
                            i++;
                            break;
                        case "version":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing version after {0}", args[i]);
                                return -1;
                            }
                            if (!Enum.TryParse<PythonLanguageVersion>(args[i + 1], true, out version)) {
                                Help();
                                Console.WriteLine("Bad version specified: {0}", args[i + 1]);
                                return -1;
                            }
                            i++;
                            break;
                        case "outdir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing output dir after {0}", args[i]);
                                return -1;
                            }
                            outdir = args[i + 1];
                            i++;
                            break;
                        case "indir":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing input dir after {0}", args[i]);
                                return -1;
                            }
                            indir = args[i + 1];
                            i++;
                            break;
                        case "id":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing GUID after {0}", args[i]);
                                return -1;
                            }
                            Guid.TryParse(args[i + 1], out id);
                            i++;
                            break;
                        case "log":
                            if (i == args.Length - 1) {
                                Help();
                                Console.WriteLine("Missing filename after {0}", args[i]);
                                return -1;
                            }
                            AnalysisLog.Output = new StreamWriter(args[i + 1], true, Encoding.UTF8);
                            i++;
                            break;
                    }
                }
            }

            if (dirs.Count == 0) {
                Help();
                return -2;
            }

            if (id == Guid.Empty) {
                id = Guid.NewGuid();
            }

            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;
            try {
                using (var writer = new StreamWriter(File.Open(Path.Combine(outdir, "AnalysisLog.txt"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read))) {
                    try {
                        // TODO: Add trigger to cancel analysis
                        new PyLibAnalyzer(dirs, indir, id, version).AnalyzeStdLib(writer, outdir, CancellationToken.None);
                    } catch (Exception e) {
                        Console.WriteLine("Error while saving analysis: {0}", e.ToString());
                        Log(writer, "ANALYSIS FAIL: \"" + e.ToString().Replace("\r\n", " -- ") + "\"");
                        return -3;
                    }
                }
            } catch (IOException) {
                // another process is already analyzing this project.  This happens when we have 2 VSs started at the same time w/o the
                // database being created yet.  Wait for that process to finish and then we can exit ourselves and both VS's will pick 
                // up the new analysis.

                while (true) {
                    try {
                        using (var writer = new StreamWriter(File.Open(Path.Combine(outdir, "AnalysisLog.txt"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))) {
                            break;
                        }
                    } catch (IOException) {
                        Thread.Sleep(20000);
                    }
                }
            }

            return 0;
        }

        private static void Help() {
            Console.WriteLine("Python Library Analyzer");
            Console.WriteLine();
            Console.WriteLine(" /dir     [directory]        - analyze provided directory (multiple directories can be provided)");
            Console.WriteLine(" /version [version]          - specify language version to be used ({0})", String.Join(", ", Enum.GetNames(typeof(PythonLanguageVersion))));
            Console.WriteLine(" /outdir  [output dir]       - specify output directory for analysis (default is current dir)");
            Console.WriteLine(" /indir   [input dir]        - specify input directory for baseline analysis");
            Console.WriteLine(" /id      [GUID]             - specify GUID of the interpreter being used");
            Console.WriteLine(" /log     [filename]         - write detailed (CSV) analysis log");
        }

        private void AnalyzeStdLib(StreamWriter writer, string outdir, CancellationToken cancel) {
            var identifier = string.Format(CultureInfo.InvariantCulture, "{0};{1}", _id, _version);
            using (var updater = new AnalyzerStatusUpdater(identifier)) {
                var fileGroups = new List<List<string>>();

                var siteDirs = _dirs.Select(dir => Path.Combine(dir, "site-packages")).ToArray();
                var allModules = ModulePath.GetModulesInLib(
                    // Directories to include files in root
                    _dirs.Concat(ModulePath.ExpandPathFiles(siteDirs)),
                    // Directories to exclude files in root
                    siteDirs);
                var allFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var group in allModules.GroupBy(mp => mp.LibraryPath, StringComparer.OrdinalIgnoreCase)) {
                    fileGroups.Add(group
                        .Where(mp => !mp.IsCompiled)
                        .Select(mp => mp.SourceFile)
                        .Except(allFileSet)
                        .ToList());
                    
                    allFileSet.UnionWith(group
                        .Where(mp => !mp.IsCompiled)
                        .Select(mp => mp.SourceFile));
                }

                int progressOffset = 0;
                int progressTotal = 0;
                foreach (var files in fileGroups) {
                    progressTotal += files.Count;
                }

                foreach (var files in fileGroups) {
                    if (files.Count > 0) {
                        Log(writer, "GROUP START \"" + Path.GetDirectoryName(files[0]) + "\"");
                        Console.WriteLine("Now analyzing: {0}", Path.GetDirectoryName(files[0]));
                        var fact = new CPythonInterpreterFactory(_version.ToVersion());
                        var projectState = new PythonAnalyzer(new CPythonInterpreter(fact, new PythonTypeDatabase(_indir, _version.Is3x())), _version);

                        int mostItemsInQueue = 0;

                        projectState.SetQueueReporting(itemsInQueue => {
                            if (itemsInQueue > mostItemsInQueue) {
                                mostItemsInQueue = itemsInQueue;
                            }

                            updater.UpdateStatus(progressOffset + (files.Count * (mostItemsInQueue - itemsInQueue)) / mostItemsInQueue, progressTotal);
                        }, 10);

                        using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                            projectState.Limits = AnalysisLimits.LoadFromStorage(key, defaultToStdLib: true);
                        }

                        var modules = new List<IPythonProjectEntry>();
                        for (int i = 0; i < files.Count; i++) {
                            string modName = PythonAnalyzer.PathToModuleName(files[i]);

                            modules.Add(projectState.AddModule(modName, files[i]));
                        }

                        var nodes = new List<PythonAst>();
                        for (int i = 0; i < modules.Count; i++) {
                            PythonAst ast = null;
                            try {
                                var sourceUnit = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read);

                                Log(writer, "PARSE START: \"" + modules[i].FilePath + "\"");
                                ast = Parser.CreateParser(sourceUnit, _version, new ParserOptions() { BindReferences = true }).ParseFile();
                                Log(writer, "PARSE END: \"" + modules[i].FilePath + "\"");
                            } catch (Exception ex) {
                                Log(writer, "PARSE ERROR: \"" + modules[i].FilePath + "\" \"" + ex.ToString().Replace("\r\n", " -- ") + "\"");
                            }
                            nodes.Add(ast);
                        }

                        for (int i = 0; i < modules.Count; i++) {
                            var ast = nodes[i];

                            if (ast != null) {
                                modules[i].UpdateTree(ast, null);
                            }
                        }

                        for (int i = 0; i < modules.Count; i++) {
                            var ast = nodes[i];
                            if (ast != null) {
                                Log(writer, "ANALYSIS START: \"" + modules[i].FilePath + "\"");
                                modules[i].Analyze(cancel, true);
                                Log(writer, "ANALYSIS END: \"" + modules[i].FilePath + "\"");
                            }
                        }

                        if (modules.Count > 0) {
                            modules[0].AnalysisGroup.AnalyzeQueuedEntries(cancel);
                        }

                        Log(writer, "SAVING GROUP: \"" + Path.GetDirectoryName(files[0]) + "\"");
                        new SaveAnalysis().Save(projectState, outdir);
                        Log(writer, "GROUP END \"" + Path.GetDirectoryName(files[0]) + "\"");
                    }

                    progressOffset += files.Count;
                }
            }
        }

        private static void Log(StreamWriter writer, string contents) {
            writer.WriteLine(
                "\"{0}\" {1}",
                DateTime.Now.ToString("s"),
                contents
            );
            writer.Flush();
        }
    }
}