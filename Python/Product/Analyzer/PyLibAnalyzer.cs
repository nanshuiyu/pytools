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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    internal class PyLibAnalyzer : IDisposable {
        private const string AnalysisLimitsKey = "Software\\Microsoft\\VisualStudio\\" +
            AssemblyVersionInfo.VSVersion +
            "\\PythonTools\\Analysis\\StandardLibrary";

        private readonly Guid _id;
        private readonly Version _version;
        private readonly string _interpreter;
        private readonly string _library;
        private readonly HashSet<string> _builtinSourceLibraries;
        private readonly string _outDir;
        private readonly List<string> _baseDb;
        private readonly string _logPrivate, _logGlobal, _logDiagnostic;
        private readonly bool _dryRun;

        private bool _all;

        private readonly AnalyzerStatusUpdater _updater;
        private readonly CancellationToken _cancel;
        private TextWriter _listener;
        internal readonly List<List<ModulePath>> _scrapeFileGroups, _analyzeFileGroups;
        private IEnumerable<string> _callDepthOverrides;

        private int _progressOffset;
        private int _progressTotal;

        private const string BuiltinName2x = "__builtin__.idb";
        private const string BuiltinName3x = "builtins.idb";
        private static readonly HashSet<string> SkipBuiltinNames = new HashSet<string> {
            "__main__"
        };

        private static void Help() {
            Console.WriteLine("Python Library Analyzer {0} ({1})",
                AssemblyVersionInfo.StableVersion,
                AssemblyVersionInfo.Version);
            Console.WriteLine("Generates a cached analysis database for a Python interpreter.");
            Console.WriteLine();
            Console.WriteLine(" /id         [GUID]             - specify GUID of the interpreter being used");
            Console.WriteLine(" /v[ersion]  [version]          - specify language version to be used (x.y format)");
            Console.WriteLine(" /py[thon]   [filename]         - full path to the Python interpreter to use");
            Console.WriteLine(" /lib[rary]  [directory]        - full path to the Python library to analyze");
            Console.WriteLine(" /outdir     [output dir]       - specify output directory for analysis (default " +
                              "is current dir)");
            Console.WriteLine(" /all                           - don't skip file groups that look up to date");

            Console.WriteLine(" /basedb     [input dir]        - specify directory for baseline analysis.");
            Console.WriteLine(" /log        [filename]         - write analysis log");
            Console.WriteLine(" /glog       [filename]         - write start/stop events");
            Console.WriteLine(" /diag       [filename]         - write detailed (CSV) analysis log");
            Console.WriteLine(" /dryrun                        - don't analyze, but write out list of files that " +
                              "would have been analyzed.");
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseArguments(IEnumerable<string> args) {
            string currentKey = null;

            using (var e = args.GetEnumerator()) {
                while (e.MoveNext()) {
                    if (e.Current.StartsWith("/")) {
                        if (currentKey != null) {
                            yield return new KeyValuePair<string, string>(currentKey, null);
                        }
                        currentKey = e.Current.Substring(1).Trim();
                    } else {
                        yield return new KeyValuePair<string, string>(currentKey, e.Current);
                        currentKey = null;
                    }
                }
            }
        }

        public static int Main(string[] args) {
            PyLibAnalyzer inst;
            try {
                inst = MakeFromArguments(args);
            } catch (ArgumentNullException ex) {
                Console.Error.WriteLine("{0} is a required argument", ex.Message);
                Help();
                return -1;
            } catch (ArgumentException ex) {
                Console.Error.WriteLine("'{0}' is not valid for {1}", ex.Message, ex.ParamName);
                Help();
                return -1;
            } catch (IdentifierInUseException) {
                Console.Error.WriteLine("This interpreter is already being analyzed.");
                return -3;
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine(ex.Message);
                Help();
                return -2;
            }

            try {
                for (bool ready = false; !ready; ) {
                    try {
                        inst.StartTraceListener();
                        ready = true;
                    } catch (IOException) {
                        Thread.Sleep(20000);
                    }
                }

                inst.LogToGlobal("START_STDLIB");

#if DEBUG
                // Running with the debugger attached will skip the
                // unhandled exception handling to allow easier debugging.
                if (Debugger.IsAttached) {
                    // Ensure that this code block matches the protected one
                    // below.

                    inst.Prepare();
                    inst.Scrape();
                    inst.Analyze();
                    inst.Epilogue();
                } else {
#endif
                    try {
                        inst.Prepare();
                        inst.Scrape();
                        inst.Analyze();
                        inst.Epilogue();
                    } catch (Exception e) {
                        Console.WriteLine("Error while saving analysis: {0}{1}", Environment.NewLine, e.ToString());
                        inst.LogToGlobal("FAIL_STDLIB" + Environment.NewLine + e.ToString());
                        inst.TraceError("Analysis failed{0}{1}", Environment.NewLine, e.ToString());
                        return -10;
                    }
#if DEBUG
                }
#endif

                inst.LogToGlobal("DONE_STDLIB");

            } finally {
                inst.Dispose();
            }

            return 0;
        }

        public PyLibAnalyzer(
            Guid id,
            Version langVersion,
            string interpreter,
            string library,
            List<string> baseDb,
            string outDir,
            string logPrivate,
            string logGlobal,
            string logDiagnostic,
            bool rescanAll,
            bool dryRun
        ) {
            _id = id;
            _version = langVersion;
            _interpreter = interpreter;
            _library = library;
            _baseDb = baseDb;
            _outDir = outDir;
            _logPrivate = logPrivate;
            _logGlobal = logGlobal;
            _logDiagnostic = logDiagnostic;
            _all = rescanAll;
            _dryRun = dryRun;
            _scrapeFileGroups = new List<List<ModulePath>>();
            _analyzeFileGroups = new List<List<ModulePath>>();
            _builtinSourceLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _builtinSourceLibraries.Add(_library);
            if (!string.IsNullOrEmpty(_library)) {
                var sitePackagesDir = Path.Combine(_library, "site-packages");
                if (Directory.Exists(sitePackagesDir)) {
                    _builtinSourceLibraries.Add(sitePackagesDir);
                }
            }

            if (_id != Guid.Empty) {
                var identifier = AnalyzerStatusUpdater.GetIdentifier(_id, _version);
                _updater = new AnalyzerStatusUpdater(identifier);
                // We worry about initialization exceptions here, specifically
                // that our identifier may already be in use.
                _updater.WaitForWorkerStarted();
                _updater.ThrowPendingExceptions();
                // Immediately inform any listeners that we've started running
                // successfully.
                _updater.UpdateStatus(0, 0, "Initializing");
            }
            // TODO: Link cancellation into the updater
            _cancel = CancellationToken.None;
        }

        public void LogToGlobal(string message) {
            if (!string.IsNullOrEmpty(_logGlobal)) {
                for (int retries = 10; retries > 0; --retries) {
                    try {
                        File.AppendAllText(_logGlobal,
                            string.Format("{0:s} {1} {2}{3}",
                                DateTime.Now,
                                message,
                                Environment.CommandLine,
                                Environment.NewLine
                            )
                        );
                        break;
                    } catch (DirectoryNotFoundException) {
                        // Create the directory and try again
                        Directory.CreateDirectory(Path.GetDirectoryName(_logGlobal));
                    } catch (IOException) {
                        // racing with someone else generating?
                        Thread.Sleep(25);
                    }
                }
            }
        }

        public void Dispose() {
            if (_updater != null) {
                _updater.Dispose();
            }
            if (_listener != null) {
                _listener.Flush();
                _listener.Close();
                _listener = null;
            }
        }

        private static PyLibAnalyzer MakeFromArguments(IEnumerable<string> args) {
            var options = ParseArguments(args)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.InvariantCultureIgnoreCase);

            string value;

            Guid id;
            Version version;
            string interpreter, library, outDir;
            List<string> baseDb;
            string logPrivate, logGlobal, logDiagnostic;
            bool rescanAll, dryRun;

            if (!options.TryGetValue("id", out value)) {
                id = Guid.Empty;
            } else if (!Guid.TryParse(value, out id)) {
                throw new ArgumentException(value, "id");
            }

            if (!options.TryGetValue("version", out value) && !options.TryGetValue("v", out value)) {
                throw new ArgumentNullException("version");
            } else if (!Version.TryParse(value, out version)) {
                throw new ArgumentException(value, "version");
            }

            if (!options.TryGetValue("python", out value) && !options.TryGetValue("py", out value)) {
                value = null;
            }
            if (!string.IsNullOrEmpty(value) && !CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "python");
            }
            interpreter = value;

            if (!options.TryGetValue("library", out value) && !options.TryGetValue("lib", out value)) {
                throw new ArgumentNullException("library");
            }
            if (!CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "library");
            }
            library = value;

            if (!options.TryGetValue("outdir", out value)) {
                value = Environment.CurrentDirectory;
            }
            if (!CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "outdir");
            }
            outDir = value;

            if (!options.TryGetValue("basedb", out value)) {
                value = Environment.CurrentDirectory;
            }
            if (!CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "basedb");
            }
            baseDb = value.Split(';').ToList();

            // Private log defaults to in current directory
            if (!options.TryGetValue("log", out value)) {
                value = Path.Combine(Environment.CurrentDirectory, "Analysislog.txt");
            }
            if (!CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "log");
            }
            if (!Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logPrivate = value;

            // Global log defaults to null - we don't write start/stop events.
            if (!options.TryGetValue("glog", out value)) {
                value = null;
            }
            if (!string.IsNullOrEmpty(value) && !CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "glog");
            }
            if (!string.IsNullOrEmpty(value) && !Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logGlobal = value;

            // Diagnostic log defaults to registry setting or else we don't use it.
            if (!options.TryGetValue("diag", out value)) {
                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    if (key != null) {
                        value = key.GetValue("LogPath") as string;
                    }
                }
            }
            if (!string.IsNullOrEmpty(value) && !CommonUtils.IsValidPath(value)) {
                throw new ArgumentException(value, "diag");
            }
            if (!string.IsNullOrEmpty(value) && !Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logDiagnostic = value;

            rescanAll = options.ContainsKey("all");
            dryRun = options.ContainsKey("dryrun");

            return new PyLibAnalyzer(
                id,
                version,
                interpreter,
                library,
                baseDb,
                outDir,
                logPrivate,
                logGlobal,
                logDiagnostic,
                rescanAll,
                dryRun
            );
        }

        internal void StartTraceListener() {
            if (!CommonUtils.IsValidPath(_logPrivate)) {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_logPrivate));
            _listener = new StreamWriter(
                new FileStream(_logPrivate, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                Encoding.UTF8);
            _listener.WriteLine();
            TraceInformation("Start analysis");
        }

        internal void Prepare() {
            if (_updater != null) {
                _updater.UpdateStatus(0, 0, "Collecting files");
            }

            // Concat the contents of directories referenced by .pth files
            // to ensure that they are overruled by normal packages in
            // naming collisions.
            var allModuleNames = new HashSet<string>(StringComparer.Ordinal);

            var fileGroups = ModulePath.GetModulesInLib(
                _interpreter,
                _library,
                null,   // default site-packages path
                allModuleNames,
                requireInitPyFiles: ModulePath.PythonVersionRequiresInitPyFiles(_version)
            )
                .GroupBy(mp => mp.LibraryPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.ToList())
                .ToList();

            // Move the standard library and builtin groups to the first
            // positions within the list of groups.
            var builtinGroups = fileGroups.FindAll(g => g.Count > 0 && _builtinSourceLibraries.Contains(g[0].LibraryPath));
            var stdLibGroups = builtinGroups.FindAll(g => g.Count > 0 && CommonUtils.IsSamePath(g[0].LibraryPath, _library));
            fileGroups.RemoveAll(g => builtinGroups.Contains(g));
            builtinGroups.RemoveAll(g => stdLibGroups.Contains(g));
            fileGroups.InsertRange(0, builtinGroups);
            fileGroups.InsertRange(0, stdLibGroups);

            // If the database was invalid, we have to refresh everything.
            var databaseVer = Path.Combine(_outDir, "database.ver");
            if (!_all) {
                _all = true;
                try {
                    var version = int.Parse(File.ReadAllText(databaseVer));
                    if (version == PythonTypeDatabase.CurrentVersion) {
                        _all = false;
                    }
                } catch (ArgumentException) {
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (NotSupportedException) {
                } catch (FormatException) {
                } catch (OverflowException) {
                }
            }

            var filesInDatabase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_dryRun) {
                if (File.Exists(databaseVer)) {
                    Console.WriteLine("DELETE;{0}", databaseVer);
                }

                // The output directory for a dry run may be completely invalid.
                // If the top level does not contain any .idb files, we won't
                // bother recursing.
                if (Directory.Exists(_outDir) &&
                    Directory.EnumerateFiles(_outDir, "*.idb", SearchOption.TopDirectoryOnly).Any()) {
                    filesInDatabase.UnionWith(Directory.EnumerateFiles(_outDir, "*.idb", SearchOption.AllDirectories));
                }
            } else {
                Directory.CreateDirectory(_outDir);

                // Mark the database as invalid until we complete analysis.
                try {
                    File.Delete(databaseVer);
                } catch (ArgumentException) {
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (NotSupportedException) {
                }

                filesInDatabase.UnionWith(Directory.EnumerateFiles(_outDir, "*.idb", SearchOption.AllDirectories));
            }

            // Store the files we want to keep separately, in case we decide to
            // delete the entire existing database.
            var filesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!_all) {
                var builtinModulePaths = GetBuiltinModuleOutputFiles().ToArray();
                if (builtinModulePaths.Any()) {
                    var interpreterTime = File.GetLastWriteTimeUtc(_interpreter);
                    if (builtinModulePaths.All(p => File.Exists(p) && File.GetLastWriteTimeUtc(p) > interpreterTime)) {
                        filesToKeep.UnionWith(builtinModulePaths);
                    } else {
                        _all = true;
                    }
                } else {
                    // Failed to get builtin names, so don't delete anything
                    // from the main output directory.
                    filesToKeep.UnionWith(
                        Directory.EnumerateFiles(_outDir, "*.idb", SearchOption.TopDirectoryOnly)
                    );
                }
            }

            _progressTotal = 0;
            _progressOffset = 0;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fileGroup in fileGroups) {
                if (ShouldAnalyze(fileGroup)) {
                    if (!_all && (builtinGroups.Contains(fileGroup) || stdLibGroups.Contains(fileGroup))) {
                        _all = true;
                    }

                    var toScrape = fileGroup.Where(mp => mp.IsCompiled && seen.Add(mp.ModuleName)).ToList();
                    var toAnalyze = fileGroup.Where(mp => seen.Add(mp.ModuleName)).ToList();

                    _progressTotal += toScrape.Count + toAnalyze.Count;

                    if (toScrape.Any()) {
                        _scrapeFileGroups.Add(toScrape);
                    }
                    if (toAnalyze.Any()) {
                        _analyzeFileGroups.Add(toAnalyze);
                    }
                } else if (!_all) {
                    filesToKeep.UnionWith(fileGroup
                        .Where(mp => File.Exists(mp.SourceFile))
                        .Select(GetOutputFile));
                }
            }

            if (!_all) {
                filesInDatabase.ExceptWith(filesToKeep);
            }

            // Scale file removal by 10 because it's much quicker than analysis.
            _progressTotal += filesInDatabase.Count / 10;
            Clean(filesInDatabase, 10);
        }

        bool ShouldAnalyze(IEnumerable<ModulePath> group) {
            if (_all) {
                return true;
            }

            foreach (var file in group.Where(f => File.Exists(f.SourceFile))) {
                var destPath = GetOutputFile(file);
                if (!File.Exists(destPath) ||
                    File.GetLastWriteTimeUtc(file.SourceFile) > File.GetLastWriteTimeUtc(destPath)) {
                    return true;
                }
            }
            return false;
        }

        internal IEnumerable<string> GetBuiltinModuleOutputFiles() {
            if (string.IsNullOrEmpty(_interpreter)) {
                return Enumerable.Empty<string>();
            }

            // Ignoring case because these will become file paths, even though
            // they are case-sensitive module names.
            var builtinNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            builtinNames.Add(_version.Major == 3 ? BuiltinName3x : BuiltinName2x);
            using (var output = ProcessOutput.RunHiddenAndCapture(
                _interpreter,
                "-c",
                "import sys; print('\\n'.join(sys.builtin_module_names))"
            )) {
                output.Wait();
                if (output.ExitCode != 0) {
                    TraceInformation("Getting builtin names");
                    TraceInformation("Command {0}", output.Arguments);
                    if (output.StandardErrorLines.Any()) {
                        TraceError("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                    }
                    return Enumerable.Empty<string>();
                } else {
                    builtinNames = new HashSet<string>(output.StandardOutputLines);
                }
            }
            TraceVerbose("Builtin names are: {0}", string.Join(", ", builtinNames.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));

            return builtinNames
                .Where(n => !SkipBuiltinNames.Contains(n))
                .Where(CommonUtils.IsValidPath)
                .Select(n => GetOutputFile(n));
        }

        internal void Clean(HashSet<string> files, int progressScale = 1) {
            if (_updater != null) {
                _updater.UpdateStatus(_progressOffset, _progressTotal, "Cleaning old files");
            }

            int modCount = 0;
            TraceInformation("Deleting {0} files", files.Count);
            foreach (var file in files) {
                if (_updater != null && ++modCount >= progressScale) {
                    modCount = 0;
                    _updater.UpdateStatus(++_progressOffset, _progressTotal, "Cleaning old files");
                }

                TraceVerbose("Deleting \"{0}\"", file);
                if (_dryRun) {
                    Console.WriteLine("DELETE:{0}", file);
                } else {
                    try {
                        File.Delete(file);
                        File.Delete(file + ".$memlist");
                        var dirName = Path.GetDirectoryName(file);
                        if (!Directory.EnumerateFileSystemEntries(dirName, "*", SearchOption.TopDirectoryOnly).Any()) {
                            Directory.Delete(dirName);
                        }
                    } catch (ArgumentException) {
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    } catch (NotSupportedException) {
                    }
                }
            }
        }


        internal string PythonScraperPath {
            get {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var file = Path.Combine(dir, "PythonScraper.py");
                return file;
            }
        }

        internal string ExtensionScraperPath {
            get {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var file = Path.Combine(dir, "ExtensionScraper.py");
                return file;
            }
        }

        private IEnumerable<string> CallDepthOverrides {
            get {
                if (_callDepthOverrides == null) {
                    var values = ConfigurationManager.AppSettings.Get("NoCallSiteAnalysis");
                    if (string.IsNullOrEmpty(values)) {
                        _callDepthOverrides = Enumerable.Empty<string>();
                    } else {
                        _callDepthOverrides = values.Split(',', ';').Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
                    }
                }
                return _callDepthOverrides;
            }
        }

        internal void Scrape() {
            if (string.IsNullOrEmpty(_interpreter)) {
                return;
            }

            if (_updater != null) {
                _updater.UpdateStatus(_progressOffset, _progressTotal, "Scraping standard library");
            }

            if (_all) {
                if (_dryRun) {
                    Console.WriteLine("Scrape builtin modules");
                } else {
                    // Scape builtin Python types
                    using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, PythonScraperPath, _outDir, _baseDb.First())) {
                        TraceInformation("Scraping builtin modules");
                        TraceInformation("Command: {0}", output.Arguments);
                        output.Wait();

                        if (output.StandardOutputLines.Any()) {
                            TraceInformation("Output{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                        }
                        if (output.StandardErrorLines.Any()) {
                            TraceWarning("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                        }

                        if (output.ExitCode != 0) {
                            if (output.ExitCode.HasValue) {
                                TraceError("Failed to scrape builtin modules (Exit Code: {0})", output.ExitCode);
                            } else {
                                TraceError("Failed to scrape builtin modules");
                            }
                            return;
                        } else {
                            TraceInformation("Scraped builtin modules");
                        }
                    }
                }
            }

            foreach (var file in _scrapeFileGroups.SelectMany()) {
                Debug.Assert(file.IsCompiled);

                if (_updater != null) {
                    _updater.UpdateStatus(_progressOffset++, _progressTotal,
                        "Scraping " + CommonUtils.GetRelativeFilePath(_library, file.LibraryPath));
                }

                var destFile = Path.ChangeExtension(GetOutputFile(file), null);
                if (_dryRun) {
                    Console.WriteLine("SCRAPE;{0};{1}.idb", file.SourceFile, CommonUtils.CreateFriendlyDirectoryPath(_outDir, destFile));
                } else {
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                    using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, ExtensionScraperPath, "scrape", file.ModuleName, "-", destFile)) {
                        TraceInformation("Scraping {0}", file.ModuleName);
                        TraceInformation("Command: {0}", output.Arguments);
                        output.Wait();

                        if (output.StandardOutputLines.Any()) {
                            TraceInformation("Output{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                        }
                        if (output.StandardErrorLines.Any()) {
                            TraceWarning("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                        }

                        if (output.ExitCode != 0) {
                            if (output.ExitCode.HasValue) {
                                TraceError("Failed to scrape {1} (Exit code: {0})", output.ExitCode, file.ModuleName);
                            } else {
                                TraceError("Failed to scrape {0}", file.ModuleName);
                            }
                        } else {
                            TraceVerbose("Scraped {0}", file.ModuleName);
                        }
                    }
                }
            }
            if (_scrapeFileGroups.Any()) {
                TraceInformation("Scraped {0} files", _scrapeFileGroups.SelectMany().Count());
            }
        }

        internal void Analyze() {
            if (_updater != null) {
                _updater.UpdateStatus(_progressOffset, _progressTotal, "Starting analysis");
            }

            if (!string.IsNullOrEmpty(_logDiagnostic) && AnalysisLog.Output == null) {
                try {
                    AnalysisLog.Output = new StreamWriter(new FileStream(_logDiagnostic, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                    AnalysisLog.AsCSV = _logDiagnostic.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase);
                } catch (Exception ex) {
                    TraceWarning("Failed to open \"{0}\" for logging{1}{2}", _logDiagnostic, Environment.NewLine, ex.ToString());
                }
            }

            foreach (var files in _analyzeFileGroups) {
                if (_cancel.IsCancellationRequested) {
                    break;
                }

                if (files.Count == 0) {
                    continue;
                }

                var outDir = GetOutputDir(files[0]);

                if (_dryRun) {
                    foreach (var file in files) {
                        Debug.Assert(!file.IsCompiled);
                        var idbFile = CommonUtils.CreateFriendlyDirectoryPath(
                            _outDir,
                            Path.Combine(outDir, file.ModuleName)
                        );
                        Console.WriteLine("ANALYZE;{0};{1}.idb", file.SourceFile, idbFile);
                    }
                    continue;
                }

                Directory.CreateDirectory(outDir);

                TraceInformation("Start group \"{0}\" with {1} files", files[0].LibraryPath, files.Count);
                AnalysisLog.StartFileGroup(files[0].LibraryPath, files.Count);
                Console.WriteLine("Now analyzing: {0}", files[0].LibraryPath);
                string currentLibrary;
                if (_builtinSourceLibraries.Contains(files[0].LibraryPath)) {
                    currentLibrary = "standard library";
                } else {
                    currentLibrary = CommonUtils.CreateFriendlyDirectoryPath(_library, files[0].LibraryPath);
                }

                var factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(
                    _version,
                    null,
                    new[] { _outDir, outDir }.Concat(_baseDb.Skip(1)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                );

                var projectState = new PythonAnalyzer(factory);

                int? mostItemsInQueue = null;
                if (_updater != null) {
                    projectState.SetQueueReporting(itemsInQueue => {
                        if (itemsInQueue > (mostItemsInQueue ?? 0)) {
                            mostItemsInQueue = itemsInQueue;
                        }

                        if (mostItemsInQueue > 0) {
                            var progress = (files.Count * (mostItemsInQueue - itemsInQueue)) / mostItemsInQueue;
                            _updater.UpdateStatus(_progressOffset +  (progress ?? 0), _progressTotal,
                                "Analyzing " + currentLibrary);
                        } else {
                            _updater.UpdateStatus(_progressOffset + files.Count, _progressTotal,
                                "Analyzing " + currentLibrary);
                        }
                    }, 10);
                }

                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    if (key != null) {
                        projectState.Limits = AnalysisLimits.LoadFromStorage(key, defaultToStdLib: true);
                    }
                }

                var items = files.Select(f => new AnalysisItem(f)).ToList();

                foreach (var item in items) {
                    if (_cancel.IsCancellationRequested) {
                        break;
                    }

                    item.Entry = projectState.AddModule(item.ModuleName, item.SourceFile);

                    if (CallDepthOverrides.Any(n => item.ModuleName.StartsWith(n))) {
                        item.Entry.Properties[AnalysisLimits.CallDepthKey] = 0;
                    }
                }

                foreach (var item in items) {
                    if (_cancel.IsCancellationRequested) {
                        break;
                    }

                    if (_updater != null) {
                        _updater.UpdateStatus(_progressOffset, _progressTotal,
                            string.Format("Parsing {0}", currentLibrary));
                    }
                    try {
                        var sourceUnit = new FileStream(item.SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var errors = new CollectingErrorSink();
                        var opts = new ParserOptions() { BindReferences = true, ErrorSink = errors };

                        TraceInformation("Parsing \"{0}\" (\"{1}\")", item.ModuleName, item.SourceFile);
                        item.Tree = Parser.CreateParser(sourceUnit, _version.ToLanguageVersion(), opts).ParseFile();
                        if (errors.Errors.Any() || errors.Warnings.Any()) {
                            TraceWarning("File \"{0}\" contained parse errors", item.SourceFile);
                            TraceInformation(string.Join(Environment.NewLine, errors.Errors.Concat(errors.Warnings)
                                .Select(er => string.Format("{0} {1}", er.Span, er.Message))));
                        }
                    } catch (Exception ex) {
                        TraceError("Error parsing \"{0}\" \"{1}\"{2}{3}", item.ModuleName, item.SourceFile, Environment.NewLine, ex.ToString());
                    }
                }

                TraceInformation("Parsing complete");

                foreach (var item in items) {
                    if (_cancel.IsCancellationRequested) {
                        break;
                    }

                    if (item.Tree != null) {
                        item.Entry.UpdateTree(item.Tree, null);
                    }
                }

                foreach (var item in items) {
                    if (_cancel.IsCancellationRequested) {
                        break;
                    }

                    try {
                        if (item.Tree != null) {
                            TraceInformation("Analyzing \"{0}\"", item.ModuleName);
                            item.Entry.Analyze(_cancel, true);
                            TraceVerbose("Analyzed \"{0}\"", item.SourceFile);
                        }
                    } catch (Exception ex) {
                        TraceError("Error analyzing \"{0}\" \"{1}\"{2}{3}", item.ModuleName, item.SourceFile, Environment.NewLine, ex.ToString());
                    }
                }

                if (items.Count > 0 && !_cancel.IsCancellationRequested) {
                    TraceInformation("Starting analysis of {0} modules", items.Count);
                    items[0].Entry.AnalysisGroup.AnalyzeQueuedEntries(_cancel);
                    TraceInformation("Analysis complete");
                }

                if (_cancel.IsCancellationRequested) {
                    break;
                }

                TraceInformation("Saving group \"{0}\"", files[0].LibraryPath);
                if (_updater != null) {
                    _progressOffset += files.Count;
                    _updater.UpdateStatus(_progressOffset, _progressTotal, "Saving " + currentLibrary);
                }
                Directory.CreateDirectory(outDir);
                new SaveAnalysis().Save(projectState, outDir);
                TraceInformation("End of group \"{0}\"", files[0].LibraryPath);
                AnalysisLog.EndFileGroup();

                AnalysisLog.Flush();
            }
        }

        internal void Epilogue() {
            if (_dryRun) {
                Console.WriteLine("WRITE;{0};{1}", Path.Combine(_outDir, "database.ver"), PythonTypeDatabase.CurrentVersion);
            } else {
                File.WriteAllText(Path.Combine(_outDir, "database.ver"), PythonTypeDatabase.CurrentVersion.ToString());
            }
        }

        private string GetOutputDir(ModulePath file) {
            if (_builtinSourceLibraries.Contains(file.LibraryPath) ||
                !CommonUtils.IsSubpathOf(_library, file.LibraryPath)) {
                return _outDir;
            } else {
                return Path.Combine(_outDir, Regex.Replace(
                    CommonUtils.TrimEndSeparator(CommonUtils.GetRelativeFilePath(_library, file.LibraryPath)),
                    @"[.\\/]",
                    "_"
                ));
            }
        }

        private string GetOutputFile(string builtinName) {
            return Path.Combine(_outDir, builtinName + ".idb");
        }

        private string GetOutputFile(ModulePath file) {
            return Path.Combine(GetOutputDir(file), file.ModuleName + ".idb");
        }

        class AnalysisItem {
            readonly ModulePath _path;

            public IPythonProjectEntry Entry { get; set; }
            public PythonAst Tree { get; set; }

            public AnalysisItem(ModulePath path) {
                _path = path;
            }

            public string ModuleName { get { return _path.ModuleName; } }
            public string SourceFile { get { return _path.SourceFile; } }
        }


        internal void TraceInformation(string message, params object[] args) {
            if (_listener != null) {
                _listener.WriteLine(DateTime.Now.ToString("s") + ": " + string.Format(message, args));
                _listener.Flush();
            }
        }

        internal void TraceWarning(string message, params object[] args) {
            if (_listener != null) {
                _listener.WriteLine(DateTime.Now.ToString("s") + ": [WARNING] " + string.Format(message, args));
                _listener.Flush();
            }
        }

        internal void TraceError(string message, params object[] args) {
            if (_listener != null) {
                _listener.WriteLine(DateTime.Now.ToString("s") + ": [ERROR] " + string.Format(message, args));
                _listener.Flush();
            }
        }

        [Conditional("DEBUG")]
        internal void TraceVerbose(string message, params object[] args) {
            if (_listener != null) {
                _listener.WriteLine(DateTime.Now.ToString("s") + ": [VERBOSE] " + string.Format(message, args));
                _listener.Flush();
            }
        }
    }
}