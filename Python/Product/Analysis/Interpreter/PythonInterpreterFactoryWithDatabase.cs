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
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter.Default;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Base class for interpreter factories that have an executable file
    /// following CPython command-line conventions, a standard library that is
    /// stored on disk as .py files, and a cached completion database.
    /// </summary>
    public class PythonInterpreterFactoryWithDatabase : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase, IDisposable {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private PythonTypeDatabase _typeDb;
        private bool _generating, _isValid, _disposed;
        private string[] _missingModules;
        private string _isCurrentException;
        private readonly Timer _refreshIsCurrentTrigger;
        private FileSystemWatcher _libWatcher;
        private readonly object _libWatcherLock = new object();

        public PythonInterpreterFactoryWithDatabase(Guid id, string description, InterpreterConfiguration config, bool watchLibraryForChanges) {
            _description = description;
            _id = id;
            _config = config;

            if (watchLibraryForChanges && Directory.Exists(_config.LibraryPath)) {
                _refreshIsCurrentTrigger = new Timer(RefreshIsCurrentTimer_Elapsed);
                try {
                    _libWatcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = _config.LibraryPath,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    _libWatcher.Created += OnChanged;
                    _libWatcher.Deleted += OnChanged;
                    _libWatcher.Changed += OnChanged;
                    _libWatcher.Renamed += OnRenamed;
                    _libWatcher.EnableRaisingEvents = true;
                } catch (ArgumentException ex) {
                    Console.WriteLine("Error starting FileSystemWatcher:\r\n{0}", ex);
                }
            }

            Task.Factory.StartNew(() => RefreshIsCurrent(false));
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public bool IsCurrentDatabaseInUse {
            get {
                return _typeDb.HasDatabaseReplacedListeners;
            }
        }

        public virtual string Description {
            get { return _description; }
        }

        public Guid Id {
            get { return _id; }
        }

        /// <summary>
        /// Returns a new interpreter created with the specific database.
        /// </summary>
        public virtual IPythonInterpreter MakeInterpreter(PythonTypeDatabase typeDb) {
            return new CPythonInterpreter(typeDb);
        }

        public IPythonInterpreter CreateInterpreter() {
            if (_typeDb == null || 
                _typeDb.DatabaseDirectory != DatabasePath ||
                _typeDb.HasCustomBuiltins) {
                var oldDb = _typeDb;
                if (_typeDb != null) {
                    _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
                }
                _typeDb = MakeTypeDatabase(DatabasePath);
                if (_typeDb != null) {
                    _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
                }

                if (oldDb != null) {
                    oldDb.OnDatabaseReplaced(_typeDb);
                }
            }

            return MakeInterpreter(_typeDb);
        }

        /// <summary>
        /// Returns a new database loaded from the specified path.
        /// </summary>
        public virtual PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (!_generating && ConfigurableDatabaseExists(databasePath, Configuration.Version)) {
                var paths = new List<string>();
                paths.Add(databasePath);
                if (includeSitePackages) {
                    paths.AddRange(Directory.EnumerateDirectories(databasePath));
                }

                try {
                    return new PythonTypeDatabase(this, paths);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }

            return PythonTypeDatabase.CreateDefaultTypeDatabase(this);
        }

        private static bool ConfigurableDatabaseExists(string databasePath, Version languageVersion) {
            if (Directory.Exists(databasePath) &&   // also ensures databasePath won't crash Path.Combine()
                File.Exists(Path.Combine(databasePath, languageVersion.Major == 3 ? "builtins.idb" : "__builtin__.idb"))) {
                string versionFile = Path.Combine(databasePath, "database.ver");
                if (File.Exists(versionFile)) {
                    try {
                        string allLines = File.ReadAllText(versionFile);
                        int version;
                        return Int32.TryParse(allLines, out version) && version == PythonTypeDatabase.CurrentVersion;
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    } catch (SecurityException) {
                    }
                }
            }
            return false;
        }

        private bool WatchingLibrary {
            get {
                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        return _libWatcher != null && _libWatcher.EnableRaisingEvents;
                    }
                }
                return false;
            }
            set {
                if (_libWatcher != null) {
                    lock (_libWatcherLock) {
                        if (_libWatcher == null) {
                            return;
                        }
                        try {
                            _libWatcher.EnableRaisingEvents = value;
                        } catch (IOException) {
                            // May occur if the library has been deleted while the
                            // watcher was disabled.
                            _libWatcher.Dispose();
                            _libWatcher = null;
                        } catch (ObjectDisposedException) {
                            _libWatcher = null;
                        }
                    }
                }
            }
        }

        public virtual void GenerateCompletionDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            if (_generating) {
                return;
            }
            WatchingLibrary = false;
            _generating = true;
            var req = new PythonTypeDatabaseCreationRequest {
                Factory = this,
                OutputPath = DatabasePath,
                SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
            };
            req.OnExit = onExit;
            PythonTypeDatabase.Generate(req);
        }

        public bool NotifyGeneratingDatabase(bool isGenerating) {
            var wasGenerating = _generating;
            if (isGenerating != _generating) {
                WatchingLibrary = !isGenerating;
                _generating = isGenerating;
            }
            return wasGenerating;
        }

        /// <summary>
        /// Returns true if the database is currently being regenerated. This
        /// state is managed automatically.
        /// </summary>
        protected bool IsGenerating {
            get {
                return _generating;
            }
        }

        public void NotifyNewDatabase() {
            if (_typeDb != null) {
                _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
            }
            var oldDb = _typeDb;
            var wasGenerating = NotifyGeneratingDatabase(false);

            _typeDb = MakeTypeDatabase(DatabasePath);
            if (_typeDb != null) {
                _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
            }

            WatchingLibrary = true;
            if (wasGenerating) {
                RefreshIsCurrent(false);
            }

            if (oldDb != null) {
                oldDb.OnDatabaseReplaced(_typeDb);
            }
        }

        private void OnDatabaseCorrupt(object sender, EventArgs args) {
            var oldDb = _typeDb;
            _typeDb = PythonTypeDatabase.CreateDefaultTypeDatabase(_config.Version);
            if (oldDb != null) {
                oldDb.OnDatabaseReplaced(_typeDb);
            }
            GenerateCompletionDatabase(GenerateDatabaseOptions.None);
        }

        public virtual void AutoGenerateCompletionDatabase() {
            RefreshIsCurrent();
            if (!IsCurrent) {
                GenerateCompletionDatabase(GenerateDatabaseOptions.None);
            }
        }

        public virtual bool IsCurrent {
            get {
                return !_generating && _isValid && _missingModules == null && _isCurrentException == null;
            }
        }

        public void NotifyInvalidDatabase() {
            if (_typeDb != null) {
                _typeDb.OnDatabaseCorrupt();
            }
        }

        public virtual string DatabasePath {
            get {
                return Path.Combine(
                    PythonTypeDatabase.CompletionDatabasePath,
                    Id.ToString(),
                    Configuration.Version.ToString()
                );
            }
        }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception e) {
                    return string.Format(culture, "Error reading: {0}", e);
                }
            }
            return null;
        }

        public virtual void RefreshIsCurrent() {
            RefreshIsCurrent(IsCurrent);
        }

        private static HashSet<string> GetExistingDatabase(string databasePath) {
            return new HashSet<string>(
                Directory.EnumerateFiles(databasePath, "*.idb", SearchOption.AllDirectories)
                    .Select(f => Path.GetFileNameWithoutExtension(f)),
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        private string[] GetMissingModules(HashSet<string> existingDatabase) {
            return ModulePath.GetModulesInLib(this)
                .Select(mp => mp.ModuleName)
                .Where(name => !existingDatabase.Contains(name))
                .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
        }

        private void RefreshIsCurrent(bool initialValue) {
            bool reasonChanged = false;

            try {
                if (!ConfigurableDatabaseExists(DatabasePath, Configuration.Version)) {
                    _isValid = false;
                } else {
                    _isValid = true;
                    HashSet<string> existingDatabase = null;
                    string[] missingModules = null;

                    for (int retries = 3; retries > 0; --retries) {
                        try {
                            existingDatabase = GetExistingDatabase(DatabasePath);
                            break;
                        } catch (UnauthorizedAccessException) {
                        } catch (IOException) {
                        }
                        Thread.Sleep(100);
                    }

                    if (existingDatabase == null) {
                        // This will either succeed or throw again. If it throws
                        // then the error is reported to the user.
                        existingDatabase = GetExistingDatabase(DatabasePath);
                    }

                    for (int retries = 3; retries > 0; --retries) {
                        try {
                            missingModules = GetMissingModules(existingDatabase);
                            break;
                        } catch (UnauthorizedAccessException) {
                        } catch (IOException) {
                        }
                        Thread.Sleep(100);
                    }

                    if (missingModules == null) {
                        // This will either succeed or throw again. If it throws
                        // then the error is reported to the user.
                        missingModules = GetMissingModules(existingDatabase);
                    }

                    if (missingModules.Length > 0) {
                        var oldModules = _missingModules;
                        if (oldModules == null ||
                            oldModules.Length != missingModules.Length ||
                            !oldModules.SequenceEqual(missingModules)) {
                            reasonChanged = true;
                        }
                        _missingModules = missingModules;
                    } else {
                        _missingModules = null;
                    }
                }
                _isCurrentException = null;
            } catch (Exception ex) {
                // Report the exception text as the reason.
                _isCurrentException = ex.ToString();
                reasonChanged = true;
            }

            if (IsCurrent != initialValue) {
                OnIsCurrentReasonChanged();
                OnIsCurrentChanged();
            } else if (reasonChanged) {
                OnIsCurrentReasonChanged();
            }
        }

        private void RefreshIsCurrentTimer_Elapsed(object state) {
            if (Directory.Exists(Configuration.LibraryPath)) {
                RefreshIsCurrent(false);
            } else {
                lock (_libWatcherLock) {
                    _libWatcher.Dispose();
                    _libWatcher = null;
                }
                OnIsCurrentChanged();
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            _refreshIsCurrentTrigger.Change(1000, Timeout.Infinite);
        }

        public event EventHandler IsCurrentChanged;

        protected void OnIsCurrentChanged() {
            var evt = IsCurrentChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public event EventHandler IsCurrentReasonChanged;

        protected void OnIsCurrentReasonChanged() {
            var evt = IsCurrentReasonChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        static string GetPackageName(string fullName) {
            int firstDot = fullName.IndexOf('.');
            return (firstDot > 0) ? fullName.Remove(firstDot) : fullName;
        }

        public virtual string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            var missingModules = _missingModules;
            if (_isCurrentException != null) {
                return "An error occurred. Click Copy to get full details.";
            } else if (_generating) {
                return "Currently regenerating";
            } else if (_libWatcher == null) {
                return "Interpreter has no library";
            } else if (!Directory.Exists(DatabasePath)) {
                return "Database has never been generated";
            } else if (!_isValid) {
                return "Database is corrupt or an old version";
            } else if (missingModules != null) {
                if (missingModules.Length < 100) {
                    return string.Format(culture,
                        "The following modules have not been analyzed:{0}    {1}",
                        Environment.NewLine,
                        string.Join(Environment.NewLine + "    ", missingModules)
                    );
                } else {
                    var packages = new List<string>(
                        from m in missingModules
                        group m by GetPackageName(m) into groupedByPackage
                        where groupedByPackage.Count() > 1
                        orderby groupedByPackage.Key
                        select groupedByPackage.Key
                    );

                    if (packages.Count > 0 && packages.Count < 100) {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.{2}Packages include:{2}    {1}",
                            missingModules.Length,
                            string.Join(Environment.NewLine + "    ", packages),
                            Environment.NewLine
                        );
                    } else {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.",
                            missingModules.Length
                        );
                    }
                }
            }

            return "Up to date";
        }

        public virtual string GetIsCurrentReason(IFormatProvider culture) {
            var missingModules = _missingModules;
            var reason = "Database at " + DatabasePath;
            if (_isCurrentException != null) {
                return _isCurrentException;
            } else if (_generating) {
                return reason + " is regenerating";
            } else if (_libWatcher == null) {
                return "Interpreter has no library";
            } else if (!Directory.Exists(DatabasePath)) {
                return reason + " does not exist";
            } else if (!_isValid) {
                return reason + " is corrupt or an old version";
            } else if (missingModules != null) {
                return reason + " does not contain the following modules:" + Environment.NewLine +
                    string.Join(Environment.NewLine, missingModules);
            }

            return reason + " is up to date";
        }


        #region IDisposable Members

        protected virtual void Dispose(bool disposing) {
            if (_libWatcher != null) {
                lock (_libWatcherLock) {
                    if (_libWatcher != null) {
                        _libWatcher.EnableRaisingEvents = false;
                        _libWatcher.Dispose();
                        _libWatcher = null;
                    }
                }
            }
            if (_refreshIsCurrentTrigger != null) {
                _refreshIsCurrentTrigger.Dispose();
            }
        }

        public void Dispose() {
            if (!_disposed) {
                Dispose(true);
                _disposed = true;
            }
        }

        #endregion
    }
}
