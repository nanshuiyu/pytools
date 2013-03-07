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

using System.Windows.Automation;
using System;

namespace TestUtilities.UI.Python.Django {
    class PythonProjectDebugProperties : AutomationWrapper {
        public PythonProjectDebugProperties(AutomationElement element)
            : base(element) {
        }

        public string LaunchMode {
            get {
                DumpElement(this.Element);

                var launchMode = FindFirstByControlType("Launch mode:", ControlType.ComboBox);

                return new ComboBox(launchMode).GetSelectedItemName();
            }
        }
    }
}
