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


namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Well known built-in types that the analysis engine needs for doing interpretation.
    /// </summary>
    public enum BuiltinTypeId {
        Unknown,
        Int,
        Long,
        Float,
        Complex,
        Dict,
        Bool,
        List,
        Tuple,
        Generator,
        Function,
        Set,
        Type,
        Object,
        /// <summary>
        /// The unicode string type
        /// </summary>
        Str,
        BuiltinMethodDescriptor,
        BuiltinFunction,
        NoneType,
        Ellipsis,
        /// <summary>
        /// The non-unicode string type
        /// </summary>
        Bytes,
    }
}
