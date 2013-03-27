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
using System.Reflection;
using System.Threading;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private PythonTypeDatabase _typeDb;
        private bool _generating;

        public CPythonInterpreterFactory()
            : this(default(Version), Guid.Empty, "Default interpreter", "", "", "PYTHONPATH", ProcessorArchitecture.X86) {
        }

        public CPythonInterpreterFactory(Version version, Guid id, string description, string pythonPath, string pythonwPath, string pathEnvVar, ProcessorArchitecture arch) {
            if (version == default(Version)) {
                version = new Version(2, 7);
            }
            _description = description;
            _id = id;
            _config = new CPythonInterpreterConfiguration(pythonPath, pythonwPath, pathEnvVar, arch, version);
        }

        internal CPythonInterpreterFactory(Version version, PythonTypeDatabase typeDb)
            : this(version, Guid.Empty, "Test interpreter", "", "", "PYTHONPATH", ProcessorArchitecture.X86) {
            _typeDb = typeDb;
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get { return _description; }
        }

        public Guid Id {
            get { return _id; }
        }

        public IPythonInterpreter CreateInterpreter() {
            lock (_interpreters) {
                if (_typeDb == null) {
                    _typeDb = MakeTypeDatabase();
                } else if (_typeDb.DatabaseDirectory != DatabasePath && ConfigurableDatabaseExists()) {
                    // database has been generated for this interpreter, switch to the specific version.
                    _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
                    _typeDb = new PythonTypeDatabase(DatabasePath, Is3x);
                    _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
                }

                var res = new CPythonInterpreter(this, _typeDb);
                
                _interpreters.Add(new WeakReference(res));
                
                return res;
            }
        }

        internal PythonTypeDatabase MakeTypeDatabase() {
            if (ConfigurableDatabaseExists()) {
                var res = new PythonTypeDatabase(DatabasePath, Is3x);
                res.DatabaseCorrupt += OnDatabaseCorrupt;
                return res;
            }

            // default DB is "never" corrupt
            return PythonTypeDatabase.CreateDefaultTypeDatabase(_config.Version);            
        }

        private bool ConfigurableDatabaseExists() {
            if (File.Exists(Path.Combine(DatabasePath, Is3x ? "builtins.idb" : "__builtin__.idb"))) {
                string versionFile = Path.Combine(DatabasePath, "database.ver");
                if (File.Exists(versionFile)) {
                    try {
                        string allLines = File.ReadAllText(versionFile);
                        int version;
                        return Int32.TryParse(allLines, out version) && version == PythonTypeDatabase.CurrentVersion;
                    } catch (IOException) {
                    }
                }
                return false;
            }
            return false;
        }

        bool IInterpreterWithCompletionDatabase.GenerateCompletionDatabase(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            return GenerateCompletionDatabaseWorker(options, databaseGenerationCompleted);
        }

        private bool GenerateCompletionDatabaseWorker(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            lock (this) {
                _generating = true;
            }
            string outPath = DatabasePath;

            if (!PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    lock (_interpreters) {
                        if (ConfigurableDatabaseExists()) {
                            if (_typeDb != null) {
                                _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
                            }

                            _typeDb = new PythonTypeDatabase(outPath, Is3x);
                            _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
                            OnNewDatabaseAvailable();
                        }
                    }
                    databaseGenerationCompleted();
                    lock (this) {
                        _generating = false;
                    }
                })) {
                lock (this) {
                    _generating = false;
                }
                return false;
            }

            return true;
        }

        private void OnDatabaseCorrupt(object sender, EventArgs args) {
            _typeDb = PythonTypeDatabase.CreateDefaultTypeDatabase(_config.Version);
            OnNewDatabaseAvailable();

            GenerateCompletionDatabaseWorker(
                GenerateDatabaseOptions.StdLibDatabase | GenerateDatabaseOptions.BuiltinDatabase,
                () => { }
            );
        }

        private void OnNewDatabaseAvailable() {
            foreach (var interpreter in _interpreters) {
                var curInterpreter = interpreter.Target as CPythonInterpreter;
                if (curInterpreter != null) {
                    curInterpreter.TypeDb = _typeDb;
                }
            }
            _interpreters.Clear();
        }

        void IInterpreterWithCompletionDatabase.AutoGenerateCompletionDatabase() {
            lock (this) {
                if (!ConfigurableDatabaseExists() && !_generating) {
                    _generating = true;
                    ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
                }
            }
        }

        public bool IsCurrent {
            get {
                return !_generating;
            }
        }

        private bool Is3x {
            get {
                return Configuration.Version.Major == 3;
            }
        }

        public void NotifyInvalidDatabase() {
            if (_typeDb != null) {
                _typeDb.OnDatabaseCorrupt();
            }
        }

        public string DatabasePath {
            get {
                return Path.Combine(PythonTypeDatabase.CompletionDatabasePath, String.Format("{0}\\{1}", Id, Configuration.Version));
            }
        }

        public string GetAnalysisLogContent() {
            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception e) {
                    return "Error reading: " + e;
                }
            }
            return null;
        }
    }
}
