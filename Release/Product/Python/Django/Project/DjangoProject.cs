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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Django.Project {
    [Guid("564253E9-EF07-4A40-89CF-790E61F53368")]
    class DjangoProject : FlavoredProject, IOleCommandTarget {
        internal DjangoPackage _package;
        private IVsProjectFlavorCfgProvider _innerVsProjectFlavorCfgProvider;
        private static Guid PythonProjectGuid = new Guid("888888a0-9f3d-457c-b088-3a5042f75d52");
        private OleMenuCommandService _menuService;
        internal Dictionary<string, HashSet<AnalysisValue>> _tags = new Dictionary<string, HashSet<AnalysisValue>>();
        internal Dictionary<string, HashSet<AnalysisValue>> _filters = new Dictionary<string, HashSet<AnalysisValue>>();
        internal Dictionary<string, Dictionary<string, HashSet<AnalysisValue>>> _templateFiles = new Dictionary<string, Dictionary<string, HashSet<AnalysisValue>>>(StringComparer.OrdinalIgnoreCase);

        private static ImageList _images;

        #region IVsAggregatableProject

        /// <summary>
        /// Do the initialization here (such as loading flavor specific
        /// information from the project)
        /// </summary>      
        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel) {
            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);

            // register the open command with the menu service provided by the base class.  We can't just handle this
            // internally because we kick off the context menu, pass ourselves as the IOleCommandTarget, and then our
            // base implementation dispatches via the menu service.  So we could either have a different IOleCommandTarget
            // which handles the Open command programmatically, or we can register it with the menu service.  
            var menuService = (IMenuCommandService)((System.IServiceProvider)this).GetService(typeof(IMenuCommandService));
            if (menuService != null) {
                CommandID menuCommandID = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.Open);
                OleMenuCommand menuItem = new OleMenuCommand(OpenFile, null, OpenFileBeforeQueryStatus, menuCommandID);
                menuService.AddCommand(menuItem);
            }

            var pyProj = this.innerVsHierarchy.GetProject().GetPythonProject();
            if (pyProj != null) {
                var analyzer = pyProj.GetProjectAnalyzer();
                var projAnalyzer = pyProj.GetAnalyzer();
                var djangoMod = analyzer.GetModule("django");
                foreach (var mod in djangoMod) {

                    foreach (var loc in mod.Locations) {
                        // replace any cached analysis w/ a live one...
                        var dirName = Path.GetDirectoryName(loc.FilePath);
                        projAnalyzer.AnalyzeDirectory(dirName);
                        analyzer.SpecializeFunction("django.template.loader", "render_to_string", RenderToStringProcessor);

                        analyzer.SpecializeFunction("django.template.base.Library", "filter", FilterProcessor);
                        analyzer.SpecializeFunction("django.template.base.Library", "tag", TagProcessor);
                        analyzer.SpecializeFunction("django.template.base.Parser", "parse", ParseProcessor);
                        analyzer.SpecializeFunction("django.template.base", "import_library", "django.template.base.Library");

                        analyzer.SpecializeFunction("django.template.loader", "get_template", GetTemplateProcessor);
                        analyzer.SpecializeFunction("django.template.context", "Context", ContextClassProcessor);
                        analyzer.SpecializeFunction("django.template.base.Template", "render", TemplateRenderProcessor);
                        break;
                    }
                }
            }
        }

        private IEnumerable<AnalysisValue> ParseProcessor(CallExpression call, CallInfo callInfo) {
            // def parse(self, parse_until=None):
            // We want to find closing tags here passed to parse_until...
            if (callInfo.NormalArgumentCount >= 2) {
                foreach (var tuple in callInfo.GetArgument(1)) {
                    foreach (var indexValue in tuple.GetItems()) {
                        var values = indexValue.Value;
                        foreach (var value in values) {
                            var str = value.GetConstantValueAsString();
                            if (str != null) {
                                RegisterTag(_tags, str);
                            }
                        }
                    }
                }
            }
            return null;
        }

        private IEnumerable<AnalysisValue> FilterProcessor(CallExpression call, CallInfo callInfo) {
            ProcessTags(callInfo, _filters);
            return null;
        }

        private IEnumerable<AnalysisValue> TagProcessor(CallExpression call, CallInfo callInfo) {
            ProcessTags(callInfo, _tags);
            return null;
        }

        private static void ProcessTags(CallInfo callInfo, Dictionary<string, HashSet<AnalysisValue>> tags) {
            if (callInfo.NormalArgumentCount >= 3) {
                // library.filter(name, value)
                foreach (var name in callInfo.GetArgument(1)) {
                    var constName = name.GetConstantValue();
                    if (constName == Type.Missing) {
                        if (name.Name != null) {
                            RegisterTag(tags, name.Name);
                        }
                    } else {
                        var strName = name.GetConstantValueAsString();
                        if (strName != null) {
                            RegisterTag(tags, strName);
                        }
                    }
                }
            } else if (callInfo.NormalArgumentCount >= 2) {
                // library.filter(value)
                foreach (var name in callInfo.GetArgument(1)) {
                    if (name.Name != null) {
                        RegisterTag(tags, name.Name);
                    }
                }
            }
        }

        private static void RegisterTag(Dictionary<string, HashSet<AnalysisValue>> tags, string name, IEnumerable<AnalysisValue> value = null) {
            HashSet<AnalysisValue> set;
            if (!tags.TryGetValue(name, out set)) {
                tags[name] = set = new HashSet<AnalysisValue>();
            }
            if (value != null) {
                foreach (var curVal in value) {
                    set.Add(curVal);
                }
            }
        }

        private IEnumerable<AnalysisValue> RenderToStringProcessor(CallExpression call, CallInfo callInfo) {
            if (callInfo.NormalArgumentCount == 2) {
                foreach (var name in callInfo.GetArgument(0)) {
                    var strName = name.GetConstantValueAsString();
                    if (strName != null) {
                        var dictArgs = callInfo.GetArgument(1);

                        AddTemplateMapping(strName, dictArgs);
                    }
                }
            }
            return null;
        }

        private void AddTemplateMapping(string filename, IEnumerable<AnalysisValue> dictArgs) {
            Dictionary<string, HashSet<AnalysisValue>> tags;
            if (!_templateFiles.TryGetValue(filename, out tags)) {
                _templateFiles[filename] = tags = new Dictionary<string, HashSet<AnalysisValue>>();
            }

            foreach (var dict in dictArgs) {
                foreach (var keyValue in dict.GetItems()) {
                    foreach (var key in keyValue.Key) {
                        var keyName = key.GetConstantValueAsString();
                        if (keyName != null) {
                            RegisterTag(tags, keyName, keyValue.Value);
                        }
                    }
                }
            }
        }

        class GetTemplateAnalysisValue : ExternalAnalysisValue {
            public readonly string Filename;
            public readonly TemplateRenderMethod RenderMethod;
            public readonly DjangoProject Project;

            public GetTemplateAnalysisValue(DjangoProject project, string name) {
                Project = project;
                Filename = name;
                RenderMethod = new TemplateRenderMethod(this);
            }

            public override IEnumerable<AnalysisValue> GetMember(string name) {
                if (name == "render") {
                    return new[] { RenderMethod };
                }
                return base.GetMember(name);
            }

        }

        class TemplateRenderMethod : ExternalAnalysisValue {
            public readonly GetTemplateAnalysisValue GetTemplateValue;

            public TemplateRenderMethod(GetTemplateAnalysisValue getTemplateAnalysisValue) {
                this.GetTemplateValue = getTemplateAnalysisValue;
            }

            public override IEnumerable<AnalysisValue> Call(ISet<AnalysisValue>[] args, NameExpression[] keywordArgNames) {
                if (args.Length == 1) {
                    foreach (var contextArg in args[0]) {
                        var context = contextArg as ExternalAnalysisValue<ContextMarker>;

                        if (context != null) {
                            // we now have the template and the context

                            string filename = GetTemplateValue.Filename;

                            GetTemplateValue.Project.AddTemplateMapping(filename, context.Data.Arguments);
                        }
                    }
                }
                return base.Call(args, keywordArgNames);
            }
        }

        private readonly Dictionary<string, GetTemplateAnalysisValue> _templateAnalysis = new Dictionary<string, GetTemplateAnalysisValue>();

        private IEnumerable<AnalysisValue> GetTemplateProcessor(CallExpression call, CallInfo callInfo) {
            HashSet<AnalysisValue> res = new HashSet<AnalysisValue>();
            if (callInfo.NormalArgumentCount >= 1) {
                foreach (var filename in callInfo.GetArgument(0)) {
                    var file = filename.GetConstantValueAsString();
                    if (file != null) {
                        GetTemplateAnalysisValue value;
                        if (!_templateAnalysis.TryGetValue(file, out value)) {
                            _templateAnalysis[file] = value = new GetTemplateAnalysisValue(this, file);
                        }
                        res.Add(value);
                    }
                }
            }
            return res;
        }

        class ContextMarker {
            public readonly HashSet<AnalysisValue> Arguments;

            public ContextMarker() {
                Arguments = new HashSet<AnalysisValue>();
            }
        }

        private ConditionalWeakTable<CallExpression, ExternalAnalysisValue<ContextMarker>> _contextTable = new ConditionalWeakTable<CallExpression, ExternalAnalysisValue<ContextMarker>>();

        private IEnumerable<AnalysisValue> ContextClassProcessor(CallExpression call, CallInfo callInfo) {
            HashSet<AnalysisValue> res = new HashSet<AnalysisValue>();
            if (callInfo.NormalArgumentCount == 1) {
                ExternalAnalysisValue<ContextMarker> contextValue;

                if (!_contextTable.TryGetValue(call, out contextValue)) {
                    contextValue = new ExternalAnalysisValue<ContextMarker>(new ContextMarker());

                    _contextTable.Add(call, contextValue);
                }

                contextValue.Data.Arguments.UnionWith(callInfo.GetArgument(0));
                return new[] { contextValue };
            }
            return null;
        }

        private IEnumerable<AnalysisValue> TemplateRenderProcessor(CallExpression call, CallInfo callInfo) {
            if (callInfo.NormalArgumentCount == 2) {
                foreach (var selfArg in callInfo.GetArgument(0)) {
                    var templateValue = selfArg as GetTemplateAnalysisValue;

                    if (templateValue != null) {
                        foreach (var contextArg in callInfo.GetArgument(1)) {
                            var context = contextArg as ExternalAnalysisValue<ContextMarker>;

                            if (context != null) {
                                // we now have the template and the context

                                string filename = templateValue.Filename;

                                AddTemplateMapping(filename, context.Data.Arguments);
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Analyzes a complete directory including all of the contained files and packages.
        /// </summary>
        public void AnalyzeDirectory(PythonAnalyzer analyzer, string dir) {
            try {
                foreach (string filename in Directory.GetFiles(dir, "*.py")) {
                    analyzer.AddModule(
                        PythonAnalyzer.PathToModuleName(filename),
                        filename,
                        null
                    );
                }
            } catch (DirectoryNotFoundException) {
            }

            try {
                foreach (string filename in Directory.GetFiles(dir, "*.pyw")) {
                    analyzer.AddModule(
                        PythonAnalyzer.PathToModuleName(filename),
                        filename,
                        null
                    );
                }
            } catch (DirectoryNotFoundException) {
            }

            try {
                foreach (string innerDir in Directory.GetDirectories(dir)) {
                    if (File.Exists(Path.Combine(innerDir, "__init__.py"))) {
                        AnalyzeDirectory(analyzer, innerDir);
                    }
                }
            } catch (DirectoryNotFoundException) {
            }
        }

        private void OpenFileBeforeQueryStatus(object sender, EventArgs e) {
            var oleMenu = sender as OleMenuCommand;
            oleMenu.Supported = false;

            foreach (var vsItemSelection in GetSelectedItems()) {
                object name;
                ErrorHandler.ThrowOnFailure(vsItemSelection.pHier.GetProperty(vsItemSelection.itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));

                if (IsHtmlFile(vsItemSelection.pHier, vsItemSelection.itemid)) {
                    oleMenu.Supported = true;
                }
            }
        }

        private bool IsHtmlFile(IVsHierarchy iVsHierarchy, uint itemid) {
            object name;
            ErrorHandler.ThrowOnFailure(iVsHierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));

            return IsHtmlFile(name);
        }

        private static bool IsHtmlFile(object name) {
            string strName = name as string;
            if (strName != null) {
                var ext = Path.GetExtension(strName);
                if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        private void OpenFile(object sender, EventArgs e) {
            var oleMenu = sender as OleMenuCommand;
            oleMenu.Supported = false;

            foreach (var vsItemSelection in GetSelectedItems()) {
                if (IsHtmlFile(vsItemSelection.pHier, vsItemSelection.itemid)) {
                    ErrorHandler.ThrowOnFailure(OpenWithDjangoEditor(vsItemSelection.itemid));
                } else {
                    ErrorHandler.ThrowOnFailure(OpenWithDefaultEditor(vsItemSelection.itemid));
                }
            }
        }

        /// <summary>
        /// Gets all of the currently selected items.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<VSITEMSELECTION> GetSelectedItems() {
            IVsMonitorSelection monitorSelection = _package.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainer = IntPtr.Zero;
            try {
                uint selectionItemId;
                IVsMultiItemSelect multiItemSelect = null;
                ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(out hierarchyPtr, out selectionItemId, out multiItemSelect, out selectionContainer));

                if (selectionItemId != VSConstants.VSITEMID_NIL && hierarchyPtr != IntPtr.Zero) {
                    IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;

                    if (selectionItemId != VSConstants.VSITEMID_SELECTION) {
                        // This is a single selection. Compare hirarchy with our hierarchy and get node from itemid
                        if (Utilities.IsSameComObject(this, hierarchy)) {
                            yield return new VSITEMSELECTION() { itemid = selectionItemId, pHier = hierarchy };
                        }
                    } else if (multiItemSelect != null) {
                        // This is a multiple item selection.
                        // Get number of items selected and also determine if the items are located in more than one hierarchy

                        uint numberOfSelectedItems;
                        int isSingleHierarchyInt;
                        ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt));
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        // Now loop all selected items and add to the list only those that are selected within this hierarchy
                        if (!isSingleHierarchy || (isSingleHierarchy && Utilities.IsSameComObject(this, hierarchy))) {
                            Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
                            VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            uint flags = (isSingleHierarchy) ? (uint)__VSGSIFLAGS.GSI_fOmitHierPtrs : 0;
                            ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(flags, numberOfSelectedItems, vsItemSelections));

                            foreach (VSITEMSELECTION vsItemSelection in vsItemSelections) {
                                yield return vsItemSelection;
                            }
                        }
                    }
                }
            } finally {
                if (hierarchyPtr != IntPtr.Zero) {
                    Marshal.Release(hierarchyPtr);
                }
                if (selectionContainer != IntPtr.Zero) {
                    Marshal.Release(selectionContainer);
                }
            }
        }

        private int OpenWithDefaultEditor(uint selectionItemId) {
            Guid view = Guid.Empty;
            IVsWindowFrame frame;
            int hr = ((IVsProject)innerVsHierarchy).OpenItem(
                selectionItemId,
                ref view,
                IntPtr.Zero,
                out frame
            );
            if (ErrorHandler.Succeeded(hr)) {
                hr = frame.Show();
            }
            return hr;
        }

        private int OpenWithDjangoEditor(uint selectionItemId) {
            Guid ourEditor = typeof(DjangoEditorFactory).GUID;
            Guid view = Guid.Empty;
            IVsWindowFrame frame;
            int hr = ((IVsProject3)innerVsHierarchy).ReopenItem(
                selectionItemId,
                ref ourEditor,
                null,
                ref view,
                new IntPtr(-1),
                out frame
            );
            if (ErrorHandler.Succeeded(hr)) {
                hr = frame.Show();
            }
            return hr;
        }

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.PreviewInBrowser:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidDjangoCmdSet) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case PkgCmdIDList.cmdidStartNewApp:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }

            }

            return base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        protected override int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == VsMenus.guidVsUIHierarchyWindowCmds) {
                switch ((VSConstants.VsUIHierarchyWindowCmdIds)nCmdID) {
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick:
                        int res;
                        if (TryHandleRightClick(pvaIn, out res)) {
                            return res;
                        }
                        break;
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick:
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey:
                        // open the document if it's an HTML file
                        if (IsHtmlFile(innerVsHierarchy, itemid)) {
                            int hr = OpenWithDjangoEditor(itemid);

                            if (ErrorHandler.Succeeded(hr)) {
                                return hr;
                            }
                        }
                        break;

                }
            } else if (pguidCmdGroup == GuidList.guidDjangoCmdSet) {
                switch (nCmdID) {
                    case PkgCmdIDList.cmdidValidateDjangoApp:
                        ValidateDjangoApp(); ;
                        return VSConstants.S_OK;
                    case PkgCmdIDList.cmdidStartNewApp:
                        StartNewApp();
                        return VSConstants.S_OK;
                }
            }

            return base.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private void StartNewApp() {
            var dialog = new NewAppDialog();
            bool? res = dialog.ShowDialog();
            if (res != null && res.Value) {
                object projectObj;
                ErrorHandler.ThrowOnFailure(
                    innerVsHierarchy.GetProperty(
                        VSConstants.VSITEMID_ROOT,
                        (int)__VSHPROPID.VSHPROPID_ExtObject,
                        out projectObj
                    )
                );

                // TODO: Check if app already exists

                var project = projectObj as EnvDTE.Project;
                if (project != null) {
                    var newFolder = project.ProjectItems.AddFolder(dialog.ViewModel.Name);
                    var newAppFilesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Templates", "Files", "DjangoNewAppFiles");
                    foreach (string file in Directory.GetFiles(newAppFilesDir)) {
                        newFolder.Collection.AddFromTemplate(file, Path.GetFileName(file));
                    }
                }
            }
        }

        private void ValidateDjangoApp() {
            var proc = RunManageCommand("validate");
            if (proc != null) {
                var dialog = new WaitForValidationDialog(proc);

                ShowValidationDialog(dialog, proc);
            } else {
                MessageBox.Show("Could not find Python interpreter for project.");
            }
        }

        private Process RunManageCommand(string arguments) {
            var pyProj = innerVsHierarchy.GetPythonInterpreterFactory();
            if (pyProj != null) {
                var path = pyProj.Configuration.InterpreterPath;
                var psi = new ProcessStartInfo(path, "manage.py " + arguments);

                object projectDir;
                ErrorHandler.ThrowOnFailure(innerVsHierarchy.GetProperty(
                    (uint)VSConstants.VSITEMID.Root,
                    (int)__VSHPROPID.VSHPROPID_ProjectDir,
                    out projectDir)
                );

                if (projectDir != null) {
                    psi.WorkingDirectory = projectDir.ToString();

                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    return Process.Start(psi);
                }
            }
            return null;
        }

        private static void ShowValidationDialog(WaitForValidationDialog dialog, Process proc) {
            var curScheduler = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
            var receiver = new OutputDataReceiver(curScheduler, dialog);
            proc.OutputDataReceived += receiver.OutputDataReceived;
            proc.ErrorDataReceived += receiver.OutputDataReceived;

            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            // when the process exits allow the user to press ok, disable cancelling...
            ThreadPool.QueueUserWorkItem(x => {
                proc.WaitForExit();
                var task = System.Threading.Tasks.Task.Factory.StartNew(
                    () => dialog.EnableOk(),
                    default(CancellationToken),
                    System.Threading.Tasks.TaskCreationOptions.None,
                    curScheduler
                );
                task.Wait();
                if (task.Exception != null) {
                    Debug.Assert(false);
                    Debug.WriteLine(task.Exception);
                }
            });

            dialog.ShowDialog();
            dialog.SetText(receiver.Received.ToString());
        }

        class OutputDataReceiver {
            public readonly StringBuilder Received = new StringBuilder();
            private readonly TaskScheduler _scheduler;
            private readonly WaitForValidationDialog _dialog;

            public OutputDataReceiver(TaskScheduler scheduler, WaitForValidationDialog dialog) {
                _scheduler = scheduler;
                _dialog = dialog;
            }

            public void OutputDataReceived(object sender, DataReceivedEventArgs e) {
                Received.Append(e.Data);
                System.Threading.Tasks.Task.Factory.StartNew(
                    () => _dialog.SetText(Received.ToString()),
                    default(CancellationToken),
                    System.Threading.Tasks.TaskCreationOptions.None,
                    _scheduler
                );
            }
        }

        private bool TryHandleRightClick(IntPtr pvaIn, out int res) {
            Guid itemType = Guid.Empty;
            foreach (var vsItemSelection in GetSelectedItems()) {
                Guid typeGuid;
                ErrorHandler.ThrowOnFailure(vsItemSelection.pHier.GetGuidProperty(vsItemSelection.itemid, (int)__VSHPROPID.VSHPROPID_TypeGuid, out typeGuid));

                if (itemType == Guid.Empty) {
                    itemType = typeGuid;
                } else if (itemType != typeGuid) {
                    // we have multiple item types
                    itemType = Guid.Empty;
                    break;
                }
            }

            if (TryShowContextMenu(pvaIn, itemType, out res)) {
                return true;
            }

            return false;
        }

        private bool TryShowContextMenu(IntPtr pvaIn, Guid itemType, out int res) {
            if (itemType == PythonProjectGuid) {
                // multiple Python prjoect nodes selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_PROJNODE/*IDM_VS_CTXT_WEBPROJECT*/);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFile) {
                // multiple files selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_WEBITEMNODE);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFolder) {
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_WEBFOLDER);
                return true;
            }
            res = VSConstants.E_FAIL;
            return false;
        }

        private int ShowContextMenu(IntPtr pvaIn, int ctxMenu) {
            object variant = Marshal.GetObjectForNativeVariant(pvaIn);
            UInt32 pointsAsUint = (UInt32)variant;
            short x = (short)(pointsAsUint & 0x0000ffff);
            short y = (short)((pointsAsUint & 0xffff0000) / 0x10000);

            POINTS points = new POINTS();
            points.x = x;
            points.y = y;

            return ShowContextMenu(ctxMenu, VsMenus.guidSHLMainMenu, points);
        }

        /// <summary>
        /// Shows the specified context menu at a specified location.
        /// </summary>
        /// <param name="menuId">The context menu ID.</param>
        /// <param name="groupGuid">The GUID of the menu group.</param>
        /// <param name="points">The location at which to show the menu.</param>
        protected virtual int ShowContextMenu(int menuId, Guid menuGroup, POINTS points) {
            IVsUIShell shell = _package.GetService(typeof(SVsUIShell)) as IVsUIShell;

            Debug.Assert(shell != null, "Could not get the ui shell from the project");
            if (shell == null) {
                return VSConstants.E_FAIL;
            }
            POINTS[] pnts = new POINTS[1];
            pnts[0].x = points.x;
            pnts[0].y = points.y;
            return shell.ShowContextMenu(0, ref menuGroup, menuId, pnts, (Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)this);
        }

        /// <summary>
        /// This should first QI for (and keep a reference to) each interface we plan to call on the inner project
        /// and then call the base implementation to do the rest. Because the base implementation
        /// already keep a reference to the interfaces it override, we don't need to QI for those.
        /// </summary>
        protected override void SetInnerProject(object inner) {
            // The reason why we keep a reference to those is that doing a QI after being
            // aggregated would do the AddRef on the outer object.
            _innerVsProjectFlavorCfgProvider = inner as IVsProjectFlavorCfgProvider;

            // Ensure we have a service provider as this is required for menu items to work
            if (this.serviceProvider == null)
                this.serviceProvider = (System.IServiceProvider)this._package;

            // Now let the base implementation set the inner object
            base.SetInnerProject(inner);

            // Add our commands (this must run after we called base.SetInnerProject)
            _menuService = ((System.IServiceProvider)this).GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            /*if (mcs != null) {
                // Command to show the generated target file
                CommandID cmd = new CommandID(GuidList.guidProjectSubtypeCmdSet, PkgCmdIDList.cmdidShowTargetFile);
                MenuCommand menuCmd = new MenuCommand(new EventHandler(ShowTargetFile), cmd);
                menuCmd.Supported = true;
                menuCmd.Visible = true;
                menuCmd.Enabled = true;
                mcs.AddCommand(menuCmd);
            }*/
        }


        #endregion

        protected override int GetProperty(uint itemId, int propId, out object property) {
            switch ((__VSHPROPID)propId) {
                case __VSHPROPID.VSHPROPID_IconIndex:
                    // replace the default icon w/ our own icon for HTML files.
                    // We can't return an index into an image list that we own because
                    // the image list is owned by the root node.  So we just fail this
                    // call for HTML files, which causes a request for VSHPROPID_IconHandle
                    // where we give the actual icon.
                    if (IsHtmlFile(innerVsHierarchy, itemId)) {
                        property = 26;
                        return VSConstants.DISP_E_MEMBERNOTFOUND;
                    }
                    break;
                case __VSHPROPID.VSHPROPID_IconHandle:
                    if (IsHtmlFile(innerVsHierarchy, itemId)) {
                        property = (Images.Images[26] as Bitmap).GetHicon();
                        return VSConstants.S_OK;
                    }
                    break;
            }

            return base.GetProperty(itemId, propId, out property);
        }

        /// <summary>
        /// Gets an ImageHandler for the project node.
        /// </summary>
        public ImageList Images {
            get {
                if (_images == null) {
                    var imageStream = typeof(DjangoProject).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Django.Resources.imagelis.bmp");

                    ImageList imageList = new ImageList();
                    imageList.ColorDepth = ColorDepth.Depth24Bit;
                    imageList.ImageSize = new Size(16, 16);
                    Bitmap bitmap = new Bitmap(imageStream);
                    imageList.Images.AddStrip(bitmap);
                    imageList.TransparentColor = Color.Magenta;
                    _images = imageList;
                }

                return _images;
            }
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == GuidList.guidWebPackgeCmdId) {
                if (nCmdID == 0x101 /*  EnablePublishToWindowsAzureMenuItem*/) {

                    // We need to forward the command to the web publish package and let it handle it, while
                    // we listen for the project which is going to get added.  After the command succeds
                    // we can then go and update the newly added project so that it is setup appropriately for
                    // Python...
                    using (var listener = new DjangoAzureSolutionListener(this)) {
                        listener.Init();

                        var shell = (IVsShell)((System.IServiceProvider)this).GetService(typeof(SVsShell));
                        Guid webPublishPackageGuid = GuidList.guidWebPackageGuid;
                        IVsPackage package;

                        if (ErrorHandler.Succeeded(shell.LoadPackage(ref webPublishPackageGuid, out package))) {
                            var managedPack = package as IOleCommandTarget;
                            if (managedPack != null) {
                                int res = managedPack.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                                if (ErrorHandler.Succeeded(res)) {
                                    // update the users service definition file to include import...
                                    foreach (var project in listener.OpenedHierarchies) {
                                        UpdateAzureDeploymentProject(project);
                                    }
                                }


                                return res;
                            }
                        }
                    }
                }
            }

            return ((IOleCommandTarget)_menuService).Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private void UpdateAzureDeploymentProject(IVsHierarchy project) {
            object projKind;
            if (!ErrorHandler.Succeeded(project.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_TypeName, out projKind)) ||
                !(projKind is string) ||
                (string)projKind != "CloudComputingProjectType") {
                return;
            }

            var dteProject = project.GetProject();
            var serviceDef = dteProject.ProjectItems.Item("ServiceDefinition.csdef");
            if (serviceDef != null && serviceDef.FileCount == 1) {
                var filename = serviceDef.FileNames[0];
                UpdateServiceDefinition(filename);
            }
        }

        private static void UpdateServiceDefinition(string filename) {
            List<string> elements = new List<string>();
            XmlWriterSettings settings = new XmlWriterSettings() { Indent = true, IndentChars = " ", NewLineHandling = NewLineHandling.Entitize };
            using (var reader = XmlReader.Create(filename)) {
                using (var writer = XmlWriter.Create(filename + ".tmp", settings)) {
                    while (reader.Read()) {
                        switch (reader.NodeType) {
                            case XmlNodeType.Element:
                                // TODO: Switch to the code below when we can successfully install our module...
                                if (reader.Name == "Imports" &&
                                        elements.Count == 2 &&
                                        elements[0] == "ServiceDefinition" &&
                                        elements[1] == "WebRole") {
                                    // insert our Imports node
                                    writer.WriteStartElement("Startup");
                                    writer.WriteStartElement("Task");
                                    writer.WriteAttributeString("commandLine", "Microsoft.PythonTools.AzureSetup.exe");
                                    writer.WriteAttributeString("executionContext", "elevated");
                                    writer.WriteAttributeString("taskType", "simple");
                                    writer.WriteEndElement();
                                    writer.WriteEndElement();
                                }
                                writer.WriteStartElement(reader.Prefix, reader.Name, reader.NamespaceURI);
                                writer.WriteAttributes(reader, true);

                                if (!reader.IsEmptyElement) {
                                    /*
                                    if (reader.Name == "Imports" &&
                                        elements.Count == 2 &&
                                        elements[0] == "ServiceDefinition" &&
                                        elements[1] == "WebRole") {

                                        writer.WriteStartElement("Import");
                                        writer.WriteAttributeString("moduleName", "PythonTools");
                                        writer.WriteEndElement();
                                    }*/

                                    elements.Add(reader.Name);
                                } else {
                                    writer.WriteEndElement();
                                }
                                break;
                            case XmlNodeType.Text:
                                writer.WriteString(reader.Value);
                                break;
                            case XmlNodeType.EndElement:
                                writer.WriteFullEndElement();
                                elements.RemoveAt(elements.Count - 1);
                                break;
                            case XmlNodeType.XmlDeclaration:
                            case XmlNodeType.ProcessingInstruction:
                                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                                break;
                            case XmlNodeType.SignificantWhitespace:
                                writer.WriteWhitespace(reader.Value);
                                break;
                            case XmlNodeType.Attribute:
                                writer.WriteAttributes(reader, true);
                                break;
                            case XmlNodeType.CDATA:
                                writer.WriteCData(reader.Value);
                                break;
                            case XmlNodeType.Comment:
                                writer.WriteComment(reader.Value);
                                break;
                        }
                    }
                }
            }

            File.Delete(filename);
            File.Move(filename + ".tmp", filename);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidVenusCmdId) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x034: /* add app assembly folder */
                        case 0x035: /* add app code folder */
                        case 0x036: /* add global resources */
                        case 0x037: /* add local resources */
                        case 0x038: /* add web refs folder */
                        case 0x039: /* add data folder */
                        case 0x040: /* add browser folders */
                        case 0x041: /* theme */
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            }

            return ((IOleCommandTarget)_menuService).QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }
}
