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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.PythonTools.Project
{
	
	[ComVisible(true)]
	public class FolderNode : HierarchyNode
	{
		private bool _isBeingCreated;

		#region ctors
		/// <summary>
		/// Constructor for the FolderNode
		/// </summary>
		/// <param name="root">Root node of the hierarchy</param>
		/// <param name="relativePath">relative path from root i.e.: "NewFolder1\\NewFolder2\\NewFolder3</param>
		/// <param name="element">Associated project element</param>
		public FolderNode(ProjectNode root, string relativePath, ProjectElement element)
			: base(root, element)
		{
            Utilities.ArgumentNotNull("relativePath", relativePath);

            if (Path.IsPathRooted(relativePath)) {
                relativePath = CommonUtils.GetRelativeDirectoryPath(root.ProjectHome, relativePath);
            }

            this.VirtualNodeName = CommonUtils.TrimEndSeparator(relativePath);
		}
		#endregion

		#region overridden properties
		public override int SortPriority
		{
			get { return DefaultSortOrderNode.FolderNode; }
		}

		/// <summary>
		/// This relates to the SCC glyph
		/// </summary>
		public override VsStateIcon StateIconIndex
		{
			get
			{
				// The SCC manager does not support being asked for the state icon of a folder (result of the operation is undefined)
				return VsStateIcon.STATEICON_NOSTATEICON;
			}
		}

        public override bool CanAddFiles {
            get {
                return true;
            }
        }

		#endregion

		#region overridden methods
		protected override NodeProperties CreatePropertiesObject()
		{
			return new FolderNodeProperties(this);
		}

		protected internal override void DeleteFromStorage(string path)
		{
			this.DeleteFolder(path);
		}

		/// <summary>
		/// Get the automation object for the FolderNode
		/// </summary>
		/// <returns>An instance of the Automation.OAFolderNode type if succeeded</returns>
		public override object GetAutomationObject()
		{
			if(this.ProjectMgr == null || this.ProjectMgr.IsClosed)
			{
				return null;
			}

			return new Automation.OAFolderItem(this.ProjectMgr.GetAutomationObject() as Automation.OAProject, this);
		}

		public override object GetIconHandle(bool open)
		{
			return this.ProjectMgr.ImageHandler.GetIconHandle(open ? (int)ProjectNode.ImageName.OpenFolder : (int)ProjectNode.ImageName.Folder);
		}
        
		/// <summary>
		/// Rename Folder
		/// </summary>
		/// <param name="label">new Name of Folder</param>
		/// <returns>VSConstants.S_OK, if succeeded</returns>
		public override int SetEditLabel(string label)
		{
			if (_isBeingCreated)
			{
				return FinishFolderAdd(label);
			}
			else
			{
                if (String.Equals(Path.GetFileName(CommonUtils.TrimEndSeparator(this.Url)), label, StringComparison.Ordinal))
				{
					// Label matches current Name
					return VSConstants.S_OK;
				}

                string newPath = CommonUtils.NormalizeDirectoryPath(Path.Combine(
                    Path.GetDirectoryName(CommonUtils.TrimEndSeparator(this.Url)), label));

				// Verify that No Directory/file already exists with the new name among current children
				for (HierarchyNode n = Parent.FirstChild; n != null; n = n.NextSibling)
				{
                    if (n != this && String.Equals(n.Caption, label, StringComparison.OrdinalIgnoreCase))
					{
                        return ShowFileOrFolderAlreadyExistsErrorMessage(newPath);
					}
				}

				// Verify that No Directory/file already exists with the new name on disk
				if (Directory.Exists(newPath) || File.Exists(newPath))
				{
                    return ShowFileOrFolderAlreadyExistsErrorMessage(newPath);
				}

				if (!ProjectMgr.Tracker.CanRenameItem(Url, newPath, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_Directory))
				{
					return VSConstants.S_OK;
				}
			}

			try
			{
                var oldTriggerFlag = this.ProjectMgr.EventTriggeringFlag;
                ProjectMgr.EventTriggeringFlag |= ProjectNode.EventTriggering.DoNotTriggerTrackerQueryEvents;
                try {
                    RenameFolder(label);
                } finally {
                    ProjectMgr.EventTriggeringFlag = oldTriggerFlag;
                }


				//Refresh the properties in the properties window
				IVsUIShell shell = this.ProjectMgr.GetService(typeof(SVsUIShell)) as IVsUIShell;
				Debug.Assert(shell != null, "Could not get the ui shell from the project");
				ErrorHandler.ThrowOnFailure(shell.RefreshPropertyBrowser(0));

				// Notify the listeners that the name of this folder is changed. This will
				// also force a refresh of the SolutionExplorer's node.
				this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);
			}
			catch(Exception e)
			{
				throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.RenameFolder, CultureInfo.CurrentUICulture), e.Message));
			}
			return VSConstants.S_OK;
		}

		private int FinishFolderAdd(string label)
		{
			// finish creation
			string filename = label.Trim();
			if (filename == "." || filename == "..")
			{
				// TODO: Report error, illegal filename
				NativeMethods.SetErrorDescription("{0} is an invalid filename.", filename);
				return VSConstants.E_FAIL;
			}

			var path = Path.Combine(ProjectMgr.GetBaseDirectoryForAddingFiles(Parent), label);
			if (path.Length > NativeMethods.MAX_PATH)
			{
				NativeMethods.SetErrorDescription("The path {0} is too long and the folder cannot be added.", path);
				return VSConstants.E_FAIL;
			}

			if (filename == Caption || this.Parent.FindChild(filename, false) == null)
			{
				if (ProjectMgr.QueryFolderAdd(Parent, this, path))
				{
					Directory.CreateDirectory(path);
					_isBeingCreated = false;
                    var relativePath = CommonUtils.GetRelativeDirectoryPath(
                        ProjectMgr.ProjectHome,
                        Path.Combine(Path.GetDirectoryName(CommonUtils.TrimEndSeparator(this.Url)), label));
					this.ItemNode.Rename(relativePath);
					this.VirtualNodeName = filename;

					this.OnItemDeleted();
					this.Parent.RemoveChild(this);
					this.ID = ProjectMgr.ItemIdMap.Add(this);
					this.Parent.AddChild(this);
				}
			}
			else
			{
				// Set error: folder already exists
				NativeMethods.SetErrorDescription("The folder {0} already exists.", filename);
				return VSConstants.E_FAIL;
			}
			return VSConstants.S_OK;
		}

		public override int MenuCommandId
		{
			get { return VsMenus.IDM_VS_CTXT_FOLDERNODE; }
		}

		public override Guid ItemTypeGuid
		{
			get
			{
				return VSConstants.GUID_ItemType_PhysicalFolder;
			}
		}

		public override string Url
		{
			get
			{
                return CommonUtils.NormalizeDirectoryPath(GetAbsoluteUrlFromMsbuild());
			}
		}

		public override string Caption
		{
			get
			{
				// it might have a backslash at the end... 
				// and it might consist of Grandparent\parent\this\
                return Path.GetFileName(CommonUtils.TrimEndSeparator(this.VirtualNodeName));
			}
		}

		public override string GetMkDocument()
		{
            Debug.Assert(!string.IsNullOrEmpty(this.Url), "No url specified for this node");
            Debug.Assert(Path.IsPathRooted(this.Url), "Url should not be a relative path");

			return this.Url;
		}

		/// <summary>
        /// Recursively walks the folder nodes and redraws the state icons
		/// </summary>
		protected internal override void UpdateSccStateIcons()
		{
			for(HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
			{
				child.UpdateSccStateIcons();
			}
		}

		protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
		{
			if(cmdGroup == VsMenus.guidStandardCommandSet97)
			{
				switch((VsCommands)cmd)
				{
					case VsCommands.Copy:
					case VsCommands.Paste:
					case VsCommands.Cut:
					case VsCommands.Rename:
						result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
						return VSConstants.S_OK;

					case VsCommands.NewFolder:
						result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
						return VSConstants.S_OK;
				}
			}
			else if(cmdGroup == VsMenus.guidStandardCommandSet2K)
			{
				if((VsCommands2K)cmd == VsCommands2K.EXCLUDEFROMPROJECT)
				{
					result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
					return VSConstants.S_OK;
				}
			}
			else
			{
				return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
			}
			return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
		}

		protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
		{
			if(deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage)
			{
				return this.ProjectMgr.CanProjectDeleteItems;
			}
			return false;
		}

        protected internal override void GetSccFiles(IList<string> files, IList<tagVsSccFilesFlags> flags) {
            for (HierarchyNode n = this.FirstChild; n != null; n = n.NextSibling) {
                n.GetSccFiles(files, flags);
            }
        }

        protected internal override void GetSccSpecialFiles(string sccFile, IList<string> files, IList<tagVsSccFilesFlags> flags) {
            for (HierarchyNode n = this.FirstChild; n != null; n = n.NextSibling) {
                n.GetSccSpecialFiles(sccFile, files, flags);
            }
        }

		#endregion

		#region virtual methods
		/// <summary>
		/// Override if your node is not a file system folder so that
		/// it does nothing or it deletes it from your storage location.
		/// </summary>
		/// <param name="path">Path to the folder to delete</param>
		public virtual void DeleteFolder(string path)
		{
			if(Directory.Exists(path))
				Directory.Delete(path, true);
		}

		/// <summary>
		/// creates the physical directory for a folder node
		/// Override if your node does not use file system folder
		/// </summary>
		public virtual void CreateDirectory()
		{
			if(Directory.Exists(this.Url) == false)
			{
				Directory.CreateDirectory(this.Url);
			}
		}
		/// <summary>
		/// Creates a folder nodes physical directory
		/// Override if your node does not use file system folder
		/// </summary>
		/// <param name="newName"></param>
		/// <returns></returns>
		public virtual void CreateDirectory(string newName)
		{
			if(String.IsNullOrEmpty(newName))
			{
				throw new ArgumentException(SR.GetString(SR.ParameterCannotBeNullOrEmpty, CultureInfo.CurrentUICulture), "newName");
			}

			// on a new dir && enter, we get called with the same name (so do nothing if name is the same
            string strNewDir = CommonUtils.GetAbsoluteDirectoryPath(Path.GetDirectoryName(CommonUtils.TrimEndSeparator(this.Url)), newName);

            if(!CommonUtils.IsSameDirectory(this.Url, strNewDir))
			{
				if(Directory.Exists(strNewDir))
				{
					throw new InvalidOperationException(SR.GetString(SR.DirectoryExistError, CultureInfo.CurrentUICulture));
				}
				Directory.CreateDirectory(strNewDir);
			}
		}

		/// <summary>
		/// Rename the physical directory for a folder node
		/// Override if your node does not use file system folder
		/// </summary>
		/// <returns></returns>
		public virtual void RenameDirectory(string newPath)
		{
			if(Directory.Exists(this.Url))
			{
				if(Directory.Exists(newPath))
				{
                    ShowFileOrFolderAlreadyExistsErrorMessage(newPath);
				}

				Directory.Move(this.Url, newPath);
			}
		}
		#endregion

		#region helper methods

        // Made public for IronStudio directory based projects:
		public virtual void RenameFolder(string newName)
		{
			// Do the rename (note that we only do the physical rename if the leaf name changed)
			string newPath = Path.Combine(this.Parent.VirtualNodeName, newName);
            if(!String.Equals(Path.GetFileName(VirtualNodeName), newName, StringComparison.Ordinal))
			{
                this.RenameDirectory(CommonUtils.GetAbsoluteDirectoryPath(this.ProjectMgr.ProjectHome, newPath));
			}
			this.VirtualNodeName = newPath;

			this.ItemNode.Rename(VirtualNodeName);

			// Let all children know of the new path
			for(HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
			{
				FolderNode node = child as FolderNode;

				if(node == null)
				{
					child.SetEditLabel(child.GetEditLabel());
				}
				else
				{
					node.RenameFolder(node.Caption);
				}
			}

			// Some of the previous operation may have changed the selection so set it back to us
			IVsUIHierarchyWindow uiWindow = UIHierarchyUtilities.GetUIHierarchyWindow(this.ProjectMgr.Site, SolutionExplorer);
			ErrorHandler.ThrowOnFailure(uiWindow.ExpandItem(this.ProjectMgr, this.ID, EXPANDFLAGS.EXPF_SelectItem));

		}

		/// <summary>
		/// Show error message if not in automation mode, otherwise throw exception
		/// </summary>
		/// <param name="newPath">path of file or folder already existing on disk</param>
		/// <returns>S_OK</returns>
        private int ShowFileOrFolderAlreadyExistsErrorMessage(string newPath)
		{
			//A file or folder with the name '{0}' already exists on disk at this location. Please choose another name.
			//If this file or folder does not appear in the Solution Explorer, then it is not currently part of your project. To view files which exist on disk, but are not in the project, select Show All Files from the Project menu.
			string errorMessage = (String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileOrFolderAlreadyExists, CultureInfo.CurrentUICulture), newPath));
			if(!Utilities.IsInAutomationFunction(this.ProjectMgr.Site))
			{
				string title = null;
				OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
				OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
				OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
				VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, errorMessage, icon, buttons, defaultButton);
				return VSConstants.S_OK;
			}
			else
			{
				throw new InvalidOperationException(errorMessage);
			}
		}

		internal void OnCancelLabelEdit()
		{
			if (_isBeingCreated)
			{
				// finish the creation
				ErrorHandler.ThrowOnFailure(SetEditLabel(VirtualNodeName));
			}
		}

		internal bool IsBeingCreated
		{
			get
			{
				return _isBeingCreated;
			}
			set
			{
				_isBeingCreated = value;
			}
		}

		#endregion
	}
}
