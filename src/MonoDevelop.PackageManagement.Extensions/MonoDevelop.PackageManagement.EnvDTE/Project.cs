﻿// 
// Project.cs
// 
// Author:
//   Matt Ward <ward.matt@gmail.com>
// 
// Copyright (C) 2014 Matthew Ward
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace ICSharpCode.PackageManagement.EnvDTE
{
	public class Project : MarshalByRefObject//, global::EnvDTE.Project
	{
		IPackageManagementProjectService projectService;
		IPackageManagementFileService fileService;
		DTE dte;

		public Project (DotNetProject project)
			: this (
				project,
				new PackageManagementProjectService (),
				new PackageManagementFileService ())
		{
		}

		public Project (
			DotNetProject project,
			IPackageManagementProjectService projectService,
			IPackageManagementFileService fileService)
		{
			this.DotNetProject = project;
			this.projectService = projectService;
			this.fileService = fileService;
			
			CreateProperties ();
//			Object = new ProjectObject(this);
//			ProjectItems = new ProjectItems(this, this, fileService);
		}

		public Project ()
		{
		}

		void CreateProperties ()
		{
//			var propertyFactory = new ProjectPropertyFactory(this);
//			Properties = new Properties(propertyFactory);
		}

		public virtual string Name {
			get { return DotNetProject.Name; }
		}

		public virtual string UniqueName {
			get { return GetUniqueName(); }
		}

		string GetUniqueName()
		{
			return FileService.AbsoluteToRelativePath (DotNetProject.ParentSolution.BaseDirectory, FileName);
		}
		
		public virtual string FileName {
			get { return DotNetProject.FileName; }
		}

		public virtual string FullName {
			get { return FileName; }
		}
		
		//		public virtual object Object { get; private set; }
		//		public virtual global::EnvDTE.Properties Properties { get; private set; }
		//		public virtual global::EnvDTE.ProjectItems ProjectItems { get; private set; }

		//		public virtual global::EnvDTE.DTE DTE {
		public virtual DTE DTE {
			get {
				if (dte == null) {
					dte = new DTE(projectService, fileService);
				}
				return dte;
			}
		}

		public virtual string Type {
			get { return GetProjectType(); }
		}

		string GetProjectType()
		{
			return ProjectType.GetProjectType(DotNetProject);
		}

		public virtual string Kind {
			get { return GetProjectKind(); }
		}

		string GetProjectKind()
		{
			return new ProjectKind(this).Kind;
		}

		internal DotNetProject DotNetProject { get; private set; }
		
		//		public virtual void Save()
		//		{
		//			projectService.Save(DotNetProject);
		//		}
		//
		//		internal virtual void AddReference(string path)
		//		{
		//			if (!HasReference(path)) {
		//				var referenceItem = new ReferenceProjectItem(DotNetProject, path);
		//				AddProjectItemToMSBuildProject(referenceItem);
		//			}
		//		}
		//
		//		bool HasReference(string include)
		//		{
		//			foreach (ReferenceProjectItem reference in GetReferences()) {
		//				if (IsReferenceMatch(reference, include)) {
		//					return true;
		//				}
		//			}
		//			return false;
		//		}
		//
		//		void AddProjectItemToMSBuildProject(SD.ProjectItem projectItem)
		//		{
		//			projectService.AddProjectItem(DotNetProject, projectItem);
		//		}
		//
		//		internal IEnumerable<SD.ProjectItem> GetReferences()
		//		{
		//			return DotNetProject.GetItemsOfType(ItemType.Reference);
		//		}
		//
		//		bool IsReferenceMatch(ReferenceProjectItem reference, string include)
		//		{
		//			return String.Equals(reference.Include, include, StringComparison.InvariantCultureIgnoreCase);
		//		}
		//
		//		internal void RemoveReference(ReferenceProjectItem referenceItem)
		//		{
		//			projectService.RemoveProjectItem(DotNetProject, referenceItem);
		//		}
		//
		//		internal ProjectItem AddFileProjectItemUsingFullPath(string path)
		//		{
		//			string dependentUpon = GetDependentUpon(path);
		//			return AddFileProjectItemWithDependentUsingFullPath(path, dependentUpon);
		//		}
		//
		//		string GetDependentUpon(string path)
		//		{
		//			var dependentFile = new DependentFile(DotNetProject);
		//			FileProjectItem projectItem = dependentFile.GetParentFileProjectItem(path);
		//			if (projectItem != null) {
		//				return Path.GetFileName(projectItem.Include);
		//			}
		//			return null;
		//		}
		//
		//		internal ProjectItem AddFileProjectItemWithDependentUsingFullPath(string path, string dependentUpon)
		//		{
		//			FileProjectItem fileProjectItem = CreateFileProjectItemUsingFullPath(path);
		//			fileProjectItem.FileName = path;
		//			fileProjectItem.DependentUpon = dependentUpon;
		//			AddProjectItemToMSBuildProject(fileProjectItem);
		//			return new ProjectItem(this, fileProjectItem);
		//		}
		//
		//		FileProjectItem CreateFileProjectItemUsingPathRelativeToProject(string include)
		//		{
		//			ItemType itemType = GetDefaultItemType(include);
		//			return CreateFileProjectItemUsingPathRelativeToProject(itemType, include);
		//		}
		//
		//		ItemType GetDefaultItemType(string include)
		//		{
		//			return DotNetProject.GetDefaultItemType(Path.GetFileName(include));
		//		}
		//
		//		FileProjectItem CreateFileProjectItemUsingPathRelativeToProject(ItemType itemType, string include)
		//		{
		//			var fileItem = new FileProjectItem(DotNetProject, itemType) {
		//				Include = include
		//			};
		//			if (IsLink(include)) {
		//				fileItem.SetEvaluatedMetadata("Link", Path.GetFileName(include));
		//			}
		//			return fileItem;
		//		}
		//
		//		bool IsLink(string include)
		//		{
		//			return include.StartsWith("..");
		//		}
		//
		//		FileProjectItem CreateFileProjectItemUsingFullPath(string path)
		//		{
		//			string relativePath = GetRelativePath(path);
		//			return CreateFileProjectItemUsingPathRelativeToProject(relativePath);
		//		}
		//
		//		internal IList<string> GetAllPropertyNames()
		//		{
		//			var names = new List<string>();
		//			lock (DotNetProject.SyncRoot) {
		//				foreach (ProjectPropertyElement propertyElement in DotNetProject.MSBuildProjectFile.Properties) {
		//					names.Add(propertyElement.Name);
		//				}
		//				names.Add("OutputFileName");
		//			}
		//			return names;
		//		}
		//
		//		public virtual global::EnvDTE.CodeModel CodeModel {
		//			get { return new CodeModel(projectService.GetProjectContent(DotNetProject) ); }
		//		}
		//
		//		public virtual global::EnvDTE.ConfigurationManager ConfigurationManager {
		//			get { return new ConfigurationManager(this); }
		//		}
		//
		//		internal virtual string GetLowercaseFileExtension()
		//		{
		//			return Path.GetExtension(FileName).ToLowerInvariant();
		//		}
		//
		//		internal virtual void DeleteFile(string fileName)
		//		{
		//			fileService.RemoveFile(fileName);
		//		}
		//
		//		internal ProjectItem AddDirectoryProjectItemUsingFullPath(string directory)
		//		{
		//			AddDirectoryProjectItemsRecursively(directory);
		//			return DirectoryProjectItem.CreateDirectoryProjectItemFromFullPath(this, directory);
		//		}
		//
		//		void AddDirectoryProjectItemsRecursively(string directory)
		//		{
		//			string[] files = fileService.GetFiles(directory);
		//			string[] childDirectories = fileService.GetDirectories(directory);
		//			if (files.Any()) {
		//				foreach (string file in files) {
		//					AddFileProjectItemUsingFullPath(file);
		//				}
		//			} else if (!childDirectories.Any()) {
		//				AddDirectoryProjectItemToMSBuildProject(directory);
		//			}
		//
		//			foreach (string childDirectory in childDirectories) {
		//				AddDirectoryProjectItemsRecursively(childDirectory);
		//			}
		//		}
		//
		//		void AddDirectoryProjectItemToMSBuildProject(string directory)
		//		{
		//			FileProjectItem projectItem = CreateMSBuildProjectItemForDirectory(directory);
		//			AddProjectItemToMSBuildProject(projectItem);
		//		}
		//
		//		FileProjectItem CreateMSBuildProjectItemForDirectory(string directory)
		//		{
		//			return new FileProjectItem(DotNetProject, ItemType.Folder) {
		//				FileName = directory
		//			};
		//		}
		//
		//		internal string GetRelativePath(string path)
		//		{
		//			return FileUtility.GetRelativePath(DotNetProject.Directory, path);
		//		}
		//
		//		internal IProjectBrowserUpdater CreateProjectBrowserUpdater()
		//		{
		//			return projectService.CreateProjectBrowserUpdater();
		//		}
		//
		//		internal ICompilationUnit GetCompilationUnit(string fileName)
		//		{
		//			return fileService.GetCompilationUnit(fileName);
		//		}
		//
		//		internal void RemoveProjectItem(ProjectItem projectItem)
		//		{
		//			projectService.RemoveProjectItem(DotNetProject, projectItem.MSBuildProjectItem);
		//		}
		//
		//		internal ProjectItem FindProjectItem(string fileName)
		//		{
		//			SD.FileProjectItem item = DotNetProject.FindFile(fileName);
		//			if (item != null) {
		//				return new ProjectItem(this, item);
		//			}
		//			return null;
		//		}
		//
		//		internal IViewContent GetOpenFile(string fileName)
		//		{
		//			return fileService.GetOpenFile(fileName);
		//		}
		//
		//		internal void OpenFile(string fileName)
		//		{
		//			fileService.OpenFile(fileName);
		//		}
		//
		//		internal bool IsFileFileInsideProjectFolder(string filePath)
		//		{
		//			return FileUtility.IsBaseDirectory(DotNetProject.Directory, filePath);
		//		}
	}
}
