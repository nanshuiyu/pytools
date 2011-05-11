/* ****************************************************************************
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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    
    public class FromImportStatement : Statement {
        private static readonly string[] _star = new[] { "*" };
        private readonly ModuleName _root;
        private readonly string[] _names;
        private readonly string[] _asNames;
        private readonly bool _fromFuture;
        private readonly bool _forceAbsolute;

        private PythonVariable[] _variables;

        internal static string/*!*/[]/*!*/ Star {
            get { return FromImportStatement._star; }
        }

        public DottedName Root {
            get { return _root; }
        } 

        public bool IsFromFuture {
            get { return _fromFuture; }
        }

        public IList<string/*!*/> Names {
            get { return _names; }
        }

        public IList<string> AsNames {
            get { return _asNames; }
        }

        internal PythonVariable[] Variables {
            get { return _variables; }
            set { _variables = value; }
        }

        public FromImportStatement(ModuleName root, string/*!*/[] names, string[] asNames, bool fromFuture, bool forceAbsolute) {
            _root = root;
            _names = names;
            _asNames = asNames;
            _fromFuture = fromFuture;
            _forceAbsolute = forceAbsolute;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            res.Append(this.GetProceedingWhiteSpace(ast));
            res.Append("from");
            Root.AppendCodeString(res, ast);
            res.Append(this.GetSecondWhiteSpace(ast));
            res.Append("import");
            if (!this.IsAltForm(ast)) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append('(');
            }

            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            int asIndex = 0;
            for (int i = 0; i < _names.Length; i++) {
                if (i > 0) {
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }
                    res.Append(',');
                }
                
                if (asNameWhiteSpace != null) {
                    res.Append(asNameWhiteSpace[asIndex++]);
                }

                res.Append(_names[i]);
                if (AsNames != null && AsNames[i] != null) {
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }
                    res.Append("as");
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }
                    res.Append(_asNames[i]);
                }
            }

            if (asIndex < asNameWhiteSpace.Length) {
                // trailing comma
                res.Append(asNameWhiteSpace[asNameWhiteSpace.Length - 1]);
                res.Append(",");
            }

            if (!this.IsAltForm(ast)) {
                res.Append(this.GetFourthWhiteSpace(ast));
                res.Append(')');
            }
        }
    }
}
