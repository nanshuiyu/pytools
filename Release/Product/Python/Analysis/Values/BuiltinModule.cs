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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal sealed class BuiltinModule : BuiltinNamespace<IPythonModule>, IReferenceableContainer, IModule {
        private readonly MemberReferences _references = new MemberReferences();
        private readonly IPythonModule _interpreterModule;

        public BuiltinModule(IPythonModule module, PythonAnalyzer projectState)
            : base(module, projectState) {
            _interpreterModule = module;
        }

        public IPythonModule InterpreterModule {
            get {
                return _interpreterModule;
            }
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _references.AddReference(node, unit, name);
            }
            return res;
        }

        public override IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            var res = ProjectState.GetAllMembers(_interpreterModule, moduleContext);
            if (_specializedValues != null) {
                foreach (var value in _specializedValues) {
                    INamespaceSet existing;
                    if(!res.TryGetValue(value.Key, out existing)) {
                        res[value.Key] = value.Value;
                    } else {
                        var newSet = existing.Union(value.Value, canMutate: false);
                        res[value.Key] = newSet;
                    }
                }
            }
            return res;
        }

        public override string Documentation {
            get {
                IPythonModule2 mod2 = _type as IPythonModule2;
                if (mod2 != null) {
                    return mod2.Documentation;
                }

                return String.Empty;
            }
        }

        public override string Description {
            get {
                return "built-in module " + _interpreterModule.Name;
            }
        }

        public override string Name {
            get {
                return _interpreterModule.Name;
            }
        }

        public override IPythonType PythonType {
            get {
                return this.ProjectState.Types.Module;
            }
        }

        public override PythonMemberType MemberType {
            get { return _interpreterModule.MemberType; }
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _references.GetDefinitions(name, _interpreterModule, ProjectState._defaultContext);
        }

        #endregion

        internal IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _type.GetMemberNames(moduleContext);
        }

        public IModule GetChildPackage(IModuleContext context, string name) {
            var mem = _type.GetMember(context, name);
            if (mem != null) {
                return ProjectState.GetNamespaceFromObjects(mem) as IModule;
            }
            return null;
        }

        public IEnumerable<KeyValuePair<string, Namespace>> GetChildrenPackages(IModuleContext context) {
            foreach (var name in _type.GetChildrenModules()) {
                yield return new KeyValuePair<string, Namespace>(name, ProjectState.GetNamespaceFromObjects(_type.GetMember(context, name)));
            }
        }

        public void SpecializeFunction(string name, Func<CallExpression, AnalysisUnit, INamespaceSet[], NameExpression[], INamespaceSet> dlg, bool analyze) {
            try {
                foreach (var v in this[name]) {
                    if (!(v is SpecializedNamespace)) {
                        this[name] = SpecializedCallable.MakeSpecializedCallable(dlg, analyze, v).SelfSet;
                        break;
                    }
                }
            } catch (KeyNotFoundException) {
            }
        }

        public void AddDependency(AnalysisUnit unit) {
            InterpreterModule.Imported(unit.DeclaringModule.InterpreterContext);
        }

        public override ILocatedMember GetLocatedMember() {
            return _interpreterModule as ILocatedMember;
        }


        public INamespaceSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef = true, InterpreterScope linkedScope = null, string linkedName = null) {
            var res = GetMember(node, unit, name);
            InterpreterModule.Imported(unit.DeclaringModule.InterpreterContext);
            return res;
        }


        public IEnumerable<string> GetModuleMemberNames(IModuleContext context) {
            return GetMemberNames(context);
        }

        public void Imported(AnalysisUnit unit) {
            InterpreterModule.Imported(unit.DeclaringModule.InterpreterContext);
        }
    }
}
