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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    internal static class NodeAttributes {
        /// <summary>
        /// Value is a string which proceeds a token in the node.
        /// </summary>
        public static readonly object PreceedingWhiteSpace = new object();
        /// <summary>
        /// Value is a string which proceeds a second token in the node.
        /// </summary>
        public static readonly object SecondPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a third token in the node.
        /// </summary>
        public static readonly object ThirdPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a fourth token in the node.
        /// </summary>
        public static readonly object FourthPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a fifth token in the node.
        /// </summary>
        public static readonly object FifthPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is an array of strings which proceeed items in the node.
        /// </summary>
        public static readonly object ListWhiteSpace = new object();

        /// <summary>
        /// Value is an array of strings which proceeed items names in the node.
        /// </summary>
        public static readonly object NamesWhiteSpace = new object();

        /// <summary>
        /// Value is a string which is the name as it appeared verbatim in the source code (for mangled name).
        /// </summary>
        public static readonly object VerbatimImage = new object();

        /// <summary>
        /// Value is a string which represents extra node specific verbatim text.
        /// </summary>
        public static readonly object ExtraVerbatimText = new object();

        /// <summary>
        /// Stores the trailing new line for statements
        /// </summary>
        public static readonly object TrailingNewLine = new object();

        /// <summary>
        /// The tuple expression was constructed without parenthesis.  The value doesn't matter, only the
        /// presence of the metadata indicates the value is set.
        /// </summary>
        public static readonly object IsAltFormValue = new object();


        public static string GetProceedingWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.PreceedingWhiteSpace);
        }

        public static string GetProceedingWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.PreceedingWhiteSpace, null);
        }

        private static string GetWhiteSpace(Node node, PythonAst ast, object kind, string defaultValue = " ") {
            object whitespace;
            if (ast.TryGetAttribute(node, kind, out whitespace)) {
                return (string)whitespace;
            } else {
                return defaultValue;
            }
        }

        public static string GetSecondWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.SecondPreceedingWhiteSpace);
        }

        public static string GetSecondWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.SecondPreceedingWhiteSpace, null);
        }

        public static string GetThirdWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.ThirdPreceedingWhiteSpace);
        }

        public static string GetFourthWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.FourthPreceedingWhiteSpace);
        }

        public static string GetFifthWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.FifthPreceedingWhiteSpace);
        }

        public static string GetExtraVerbatimText(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.ExtraVerbatimText, null);
        }

        public static string GetTrailingNewLine(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.TrailingNewLine, null);
        }

        public static bool IsAltForm(this Node node, PythonAst ast) {
            object dummy;
            if (ast.TryGetAttribute(node, NodeAttributes.IsAltFormValue, out dummy)) {
                return true;
            } else {
                return false;
            }
        }

        public static string[] GetListWhiteSpace(this Node node, PythonAst ast) {
            object whitespace;
            if (ast.TryGetAttribute(node, NodeAttributes.ListWhiteSpace, out whitespace)) {
                return (string[])whitespace;
            } else {
                return null;
            }
        }

        public static string[] GetNamesWhiteSpace(this Node node, PythonAst ast) {
            object whitespace;
            if (ast.TryGetAttribute(node, NodeAttributes.NamesWhiteSpace, out whitespace)) {
                return (string[])whitespace;
            } else {
                return null;
            }
        }

        public static string GetVerbatimImage(this Node node, PythonAst ast) {
            object image;
            if (ast.TryGetAttribute(node, NodeAttributes.VerbatimImage, out image)) {
                return (string)image;
            } else {
                return null;
            }
        }

        public static void AppendTrailingNewLine(this Statement node, StringBuilder res, PythonAst ast) {
            var trailingNewLine = node.GetTrailingNewLine(ast);
            if (trailingNewLine != null) {
                res.Append(trailingNewLine);
            }
        }
    }

}
