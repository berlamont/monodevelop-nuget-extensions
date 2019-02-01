﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using MonoDevelop.PackageManagement.PowerShell.EnvDTE;
using NuGet.Common;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using MonoDevelop.PackageManagement.PowerShell.ConsoleHost.Core;
using System.Threading;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
	[Cmdlet (VerbsData.Update, "Package", DefaultParameterSetName = "All")]
	public class UpdatePackageCommand : PackageActionBaseCommand
	{
		//UninstallationContext uninstallcontext;
		string id;
		string projectName;
		bool idSpecified;
		bool projectSpecified;
		bool versionSpecifiedPrerelease;
		bool allowPrerelease;
		NuGetVersion nugetVersion;

		[Parameter (Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Project")]
		[Parameter (ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "All")]
		[Parameter (ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Reinstall")]
		public override string Id {
			get { return id; }
			set {
				id = value;
				idSpecified = true;
			}
		}

		[Parameter (Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "All")]
		[Parameter (Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "Project")]
		[Parameter (Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "Reinstall")]
		public override string ProjectName {
			get { return projectName; }
			set {
				projectName = value;
				projectSpecified = true;
			}
		}

		[Parameter (Position = 2, ParameterSetName = "Project")]
		[ValidateNotNullOrEmpty]
		public override string Version { get; set; }

		[Parameter]
		[Alias ("ToHighestPatch")]
		public SwitchParameter Safe { get; set; }

		[Parameter]
		public SwitchParameter ToHighestMinor { get; set; }

		[Parameter (Mandatory = true, ParameterSetName = "Reinstall")]
		[Parameter (ParameterSetName = "All")]
		public SwitchParameter Reinstall { get; set; }

		List<Project> DTEProjects { get; set; }

		public bool IsVersionEnum { get; set; }

		protected override void Preprocess ()
		{
			base.Preprocess ();
			ParseUserInputForVersion ();
			if (!projectSpecified) {
				 Task.Run (async () => {
					var projects = await SolutionManager.GetAllProjectsAsync ();
					DTEProjects = projects.Select (project => (Project)project).ToList ();
				}).Wait ();
			} else {
				DTEProjects = new List<Project> { DTEProject };
			}
		}

		protected override void ProcessRecordCore ()
		{
			Preprocess ();

			WarnIfParametersAreNotSupported ();

			// Update-Package without ID specified
			if (!idSpecified) {
				Task.Run (UpdateOrReinstallAllPackagesAsync).Forget ();
			}
			// Update-Package with Id specified
			else {
				Task.Run (UpdateOrReinstallSinglePackageAsync).Forget ();
			}

			WaitAndLogPackageActions ();
		}

		protected override void WarnIfParametersAreNotSupported ()
		{
			if (Source != null) {
				var projectNames = string.Join (",", DTEProjects.Select (p => p.UniqueName));
				if (!string.IsNullOrEmpty (projectNames)) {
					var warning = string.Format (
						CultureInfo.CurrentUICulture,
						"The '{0}' parameter is not respected for the transitive package management based project(s) {1}. The enabled sources in your NuGet configuration will be used.",
						nameof (Source),
						projectNames);
					Log (MessageLevel.Warning, warning);
				}
			}
		}

		/// <summary>
		/// Update or reinstall all packages installed to a solution. For Update-Package or Update-Package -Reinstall.
		/// </summary>
		async Task UpdateOrReinstallAllPackagesAsync ()
		{
			try {
				//using (var sourceCacheContext = new SourceCacheContext ()) {
				//	var resolutionContext = new ResolutionContext (
				//		GetDependencyBehavior (),
				//		allowPrerelease,
				//		false,
				//		DetermineVersionConstraints (),
				//		new GatherCache (),
				//		sourceCacheContext);

				//	// if the source is explicitly specified we will use exclusively that source otherwise use ALL enabled sources
				//	var actions = await PackageManager.PreviewUpdatePackagesAsync (
				//		Projects,
				//		resolutionContext,
				//		this,
				//		PrimarySourceRepositories,
				//		PrimarySourceRepositories,
				//		Token);

				//	if (!actions.Any ()) {
				//		_status = NuGetOperationStatus.NoOp;
				//	} else {
				//		_packageCount = actions.Select (action => action.PackageIdentity.Id).Distinct ().Count ();
				//	}

				//	await ExecuteActions (actions, sourceCacheContext);
				//}
			} catch (Exception ex) {
				Log (MessageLevel.Error, ExceptionUtilities.DisplayMessage (ex));
			} finally {
				BlockingCollection.Add (new ExecutionCompleteMessage ());
			}
		}

		/// <summary>
		/// Update or reinstall a single package installed to a solution. For Update-Package -Id or Update-Package -Id
		/// -Reinstall.
		/// </summary>
		async Task UpdateOrReinstallSinglePackageAsync ()
		{
			try {
				await PreviewAndExecuteUpdateActionsForSinglePackage ();
			} catch (Exception ex) {
				Log (MessageLevel.Error, ExceptionUtilities.DisplayMessage (ex));
			} finally {
				BlockingCollection.Add (new ExecutionCompleteMessage ());
			}
		}

		/// <summary>
		/// Preview update actions for single package
		/// </summary>
		async Task PreviewAndExecuteUpdateActionsForSinglePackage ()
		{
			if (WhatIf.IsPresent) {
				var actionsList = await DTEProject.PreviewUpdatePackageAsync (
					Id,
					nugetVersion?.ToNormalizedString (),
					GetDependencyBehavior (),
					allowPrerelease,
					DetermineVersionConstraints (),
					PrimarySourceRepositories,
					Token);
				if (actionsList.IsPackageInstalled) {
					PreviewNuGetPackageActions (actionsList.Actions);
				} else {
					Log (MessageLevel.Error, "'{0}' was not installed in any project. Update failed.", Id);
				}
			} else {
				bool updated = await DTEProject.UpdatePackageAsync (
					Id,
					nugetVersion?.ToNormalizedString (),
					GetDependencyBehavior (),
					allowPrerelease,
					DetermineVersionConstraints (),
					PrimarySourceRepositories,
					Token);
				if (!updated) {
					Log (MessageLevel.Error, "'{0}' was not installed in any project. Update failed.", Id);
				}
			}
			//var actions = Enumerable.Empty<NuGetProjectAction> ();

			//using (var sourceCacheContext = new SourceCacheContext ()) {
			//	var resolutionContext = new ResolutionContext (
			//		GetDependencyBehavior (),
			//		allowPrerelease,
			//		false,
			//		DetermineVersionConstraints (),
			//		new GatherCache (),
			//		sourceCacheContext);

			//	// If -Version switch is specified
			//	if (!string.IsNullOrEmpty (Version)) {
			//		actions = await PackageManager.PreviewUpdatePackagesAsync (
			//			new PackageIdentity (Id, PowerShellCmdletsUtility.GetNuGetVersionFromString (Version)),
			//			Projects,
			//			resolutionContext,
			//			this,
			//			PrimarySourceRepositories,
			//			EnabledSourceRepositories,
			//			Token);
			//	} else {
			//		actions = await PackageManager.PreviewUpdatePackagesAsync (
			//			Id,
			//			Projects,
			//			resolutionContext,
			//			this,
			//			PrimarySourceRepositories,
			//			EnabledSourceRepositories,
			//			Token);
			//	}

			//	await ExecuteActions (actions, sourceCacheContext);
			//}
		}

		/// <summary>
		/// Parse user input for -Version switch
		/// </summary>
		void ParseUserInputForVersion ()
		{
			if (!string.IsNullOrEmpty (Version)) {
				// If Version is prerelease, automatically allow prerelease (i.e. append -Prerelease switch).
				nugetVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString (Version);
				if (nugetVersion.IsPrerelease) {
					versionSpecifiedPrerelease = true;
				}
			}
			allowPrerelease = IncludePrerelease.IsPresent || versionSpecifiedPrerelease;
		}

		///// <summary>
		///// Uninstallation Context for Update-Package -Reinstall command
		///// </summary>
		//public UninstallationContext UninstallContext {
		//	get {
		//		uninstallcontext = new UninstallationContext (false, Reinstall.IsPresent);
		//		return uninstallcontext;
		//	}
		//}

		/// <summary>
		/// Return dependecy behavior for Update-Package command.
		/// </summary>
		protected override DependencyBehavior GetDependencyBehavior ()
		{
			// Return DependencyBehavior.Highest for Update-Package
			if (!idSpecified
				&& !Reinstall.IsPresent) {
				return DependencyBehavior.Highest;
			}

			return base.GetDependencyBehavior ();
		}

		/// <summary>
		/// Determine the UpdateConstraints based on the command line arguments
		/// </summary>
		VersionConstraints DetermineVersionConstraints ()
		{
			if (Reinstall.IsPresent) {
				return VersionConstraints.ExactMajor | VersionConstraints.ExactMinor | VersionConstraints.ExactPatch | VersionConstraints.ExactRelease;
			} else if (Safe.IsPresent) {
				return VersionConstraints.ExactMajor | VersionConstraints.ExactMinor;
			} else if (ToHighestMinor.IsPresent) {
				return VersionConstraints.ExactMajor;
			} else {
				return VersionConstraints.None;
			}
		}
	}
}