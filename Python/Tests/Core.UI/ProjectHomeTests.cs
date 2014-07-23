﻿/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba).
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
using System.Text;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.Project.Automation;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Mouse = TestUtilities.UI.Mouse;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    [TestClass]
    public class ProjectHomeTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadRelativeProjects() {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeProjects.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            try {
                Assert.IsTrue(VsIdeTestHostContext.Dte.Solution.IsOpen, "The solution is not open");
                Assert.IsTrue(VsIdeTestHostContext.Dte.Solution.Projects.Count == 9, String.Format("Loading project resulted in wrong number of loaded projects, expected 9, received {0}", VsIdeTestHostContext.Dte.Solution.Projects.Count));

                foreach (var project in VsIdeTestHostContext.Dte.Solution.Projects.OfType<Project>()) {
                    var name = Path.GetFileName(project.FileName);
                    if (name.StartsWith("ProjectA")) {
                        // Should have ProgramA.py, Subfolder\ProgramB.py and Subfolder\Subsubfolder\ProgramC.py
                        var programA = project.ProjectItems.Item("ProgramA.py");
                        Assert.IsNotNull(programA);

                        var subfolder = project.ProjectItems.Item("Subfolder");
                        var programB = subfolder.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = subfolder.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else if (name.StartsWith("ProjectB")) {
                        // Should have ProgramB.py and Subsubfolder\ProgramC.py
                        var programB = project.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = project.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else if (name.StartsWith("ProjectSln")) {
                        // Should have ProjectHomeProjects\ProgramA.py, 
                        // ProjectHomeProjects\Subfolder\ProgramB.py and
                        // ProjectHomeProjects\Subfolder\Subsubfolder\ProgramC.py
                        var projectHome = project.ProjectItems.Item("ProjectHomeProjects");
                        var programA = projectHome.ProjectItems.Item("ProgramA.py");
                        Assert.IsNotNull(programA);

                        var subfolder = projectHome.ProjectItems.Item("Subfolder");
                        var programB = subfolder.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = subfolder.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else {
                        Assert.Fail("Wrong project file name", name);
                    }
                }
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddDeleteItem() {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeSingleProject.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            try {
                var project = VsIdeTestHostContext.Dte.Solution.Projects.OfType<Project>().Single();
                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                project.ProjectItems.AddFromTemplate(((Solution2)VsIdeTestHostContext.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                var newItem = project.ProjectItems.Item("TemplateItem.py");
                Assert.IsNotNull(newItem);
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\TemplateItem.py")));

                newItem.Delete();
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\TemplateItem.py")));
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddDeleteItem2() {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeSingleProject.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            try {
                var project = VsIdeTestHostContext.Dte.Solution.Projects.OfType<Project>().Single();
                var folder = project.ProjectItems.Item("Subfolder");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                folder.ProjectItems.AddFromTemplate(((Solution2)VsIdeTestHostContext.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                var newItem = folder.ProjectItems.Item("TemplateItem.py");
                Assert.IsNotNull(newItem);
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\TemplateItem.py")));

                newItem.Delete();
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\TemplateItem.py")));
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddDeleteFolder() {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeSingleProject.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            try {
                var project = VsIdeTestHostContext.Dte.Solution.Projects.OfType<Project>().Single();
                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                project.ProjectItems.AddFolder("NewFolder");

                var newFolder = project.ProjectItems.Item("NewFolder");
                Assert.IsNotNull(newFolder);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
                newFolder.Delete();
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddDeleteSubfolder() {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeSingleProject.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            try {
                var project = VsIdeTestHostContext.Dte.Solution.Projects.OfType<Project>().Single();
                var folder = project.ProjectItems.Item("Subfolder");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                folder.ProjectItems.AddFolder("NewFolder");

                var newFolder = folder.ProjectItems.Item("NewFolder");
                Assert.IsNotNull(newFolder);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
                newFolder.Delete();
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveProjectAs() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                try {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");

                    project.SaveAs(TestData.GetPath(@"TestData\ProjectHomeProjects\TempFile.pyproj"));

                    Assert.AreEqual(TestData.GetPath(@"TestData\HelloWorld\"),
                        ((OAProject)project).ProjectNode.ProjectHome);

                    VsIdeTestHostContext.Dte.Solution.SaveAs("HelloWorldRelocated.sln");
                } finally {
                    VsIdeTestHostContext.Dte.Solution.Close();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                try {
                    var project = app.OpenProject(@"TestData\HelloWorldRelocated.sln");

                    Assert.AreEqual("TempFile.pyproj", project.FileName);

                    Assert.AreEqual(TestData.GetPath(@"TestData\HelloWorld\"),
                        ((OAProject)project).ProjectNode.ProjectHome);
                } finally {
                    VsIdeTestHostContext.Dte.Solution.Close();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragDropTest() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenProject(@"TestData\DragDropRelocatedTest.sln");

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.FindItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest", "TestFolder", "SubItem.py");
                var point = folder.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Down(MouseButton.Left);

                var projectItem = window.FindItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest");
                point = projectItem.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Up(MouseButton.Left);

                Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest", "SubItem.py"));

                app.Dte.Solution.Close(true);
                try {
                    // Ensure file was moved and the path was updated correctly.
                    var project = app.OpenProject(@"TestData\DragDropRelocatedTest.sln");
                    foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                        Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
                    }
                } finally {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutPasteTest() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenProject(@"TestData\CutPasteRelocatedTest.sln");

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.FindItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest", "TestFolder", "SubItem.py");
                AutomationWrapper.Select(folder);
                app.ExecuteCommand("Edit.Cut");

                var projectItem = window.FindItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest");
                AutomationWrapper.Select(projectItem);
                app.ExecuteCommand("Edit.Paste");

                Assert.IsNotNull(window.WaitForItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest", "SubItem.py"));

                app.Dte.Solution.Close(true);
                try {
                    // Ensure file was moved and the path was updated correctly.
                    var project = app.OpenProject(@"TestData\CutPasteRelocatedTest.sln");
                    foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                        Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
                    }
                } finally {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }
}
