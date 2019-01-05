﻿// 
// PackageManagementConsoleHost.cs
// 
// Author:
//   Matt Ward <ward.matt@gmail.com>
// 
// Copyright (C) 2011-2014 Matthew Ward
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Scripting;
using MonoDevelop.Ide;
using MonoDevelop.PackageManagement;
using MonoDevelop.PackageManagement.Scripting;
using MonoDevelop.Projects;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace ICSharpCode.PackageManagement.Scripting
{
	internal class PackageManagementConsoleHost : IPackageManagementConsoleHost
	{
		RegisteredPackageSources registeredPackageSources;
		IPowerShellHostFactory powerShellHostFactory;
		IPowerShellHost powerShellHost;
		IMonoDevelopSolutionManager solutionManager;
		ISourceRepositoryProvider sourceRepositoryProvider;
		IPackageManagementAddInPath addinPath;
		IPackageManagementEvents packageEvents;
		ConsoleHostScriptRunner scriptRunner;
		string prompt = "PM> ";

		public PackageManagementConsoleHost (
			IPackageManagementEvents packageEvents,
			IMonoDevelopSolutionManager solutionManager,
			IPowerShellHostFactory powerShellHostFactory,
			IPackageManagementAddInPath addinPath)
		{
			this.registeredPackageSources = new RegisteredPackageSources (solutionManager);
			this.powerShellHostFactory = powerShellHostFactory;
			this.solutionManager = solutionManager;
			this.addinPath = addinPath;
			this.packageEvents = packageEvents;

			scriptRunner = new ConsoleHostScriptRunner ();
		}

		public PackageManagementConsoleHost (
			IPackageManagementEvents packageEvents)
			: this (
				packageEvents,
				new ConsoleHostSolutionManager (),
				new PowerShellHostFactory (),
				new PackageManagementAddInPath ())
		{
		}

		public PackageManagementConsoleHost (
			IPackageManagementEvents packageEvents,
			IPowerShellHostFactory powerShellHostFactory)
			: this (
				packageEvents,
				new ConsoleHostSolutionManager (),
				powerShellHostFactory,
				new PackageManagementAddInPath ())
		{
		}

		public bool IsRunning { get; private set; }
		public Project DefaultProject { get; set; }

		public SourceRepositoryViewModel ActivePackageSource {
			get { return registeredPackageSources.SelectedPackageSource; }
			set { registeredPackageSources.SelectedPackageSource = value; }
		}

		public IEnumerable<SourceRepositoryViewModel> PackageSources {
			get { return registeredPackageSources.PackageSources; }
		}

		public IScriptingConsole ScriptingConsole { get; set; }

		public bool IsSolutionOpen {
			get { return solutionManager.IsSolutionOpen; }
		}

		public CancellationToken Token {
			get { return CancellationToken.None; }
		}

		public IMonoDevelopSolutionManager SolutionManager {
			get { return solutionManager; }
		}

		public ISettings Settings {
			get { return solutionManager.Settings; }
		}

		public void Dispose ()
		{
			//			ShutdownConsole();
			//			
			//			if (thread != null) {
			//				if (thread.Join(100)) {
			//					thread = null;
			//					IsRunning = false;
			//				}
			//			}
		}

		public void Clear ()
		{
			ScriptingConsole.Clear ();
		}

		public void WritePrompt ()
		{
			ScriptingConsole.Write (prompt, ScriptingStyle.Prompt);
		}

		public void Run ()
		{
			PackageManagementBackgroundDispatcher.Dispatch (() => {
				RunSynchronous ();
				IsRunning = true;
			});
			//			thread = CreateThread(RunSynchronous);
			//			thread.Start();
		}

		//		protected virtual IThread CreateThread(ThreadStart threadStart)
		//		{
		//			return new PackageManagementThread(threadStart);
		//		}
		//		
		void RunSynchronous ()
		{
			InitPowerShell ();
			WriteInfoBeforeFirstPrompt ();
			InitializePackageScriptsForOpenSolution ();
			WritePrompt ();
			//			ProcessUserCommands();

			IdeApp.Workspace.SolutionLoaded += SolutionLoaded;
		}

		void InitPowerShell ()
		{
			CreatePowerShellHost ();
			AddModulesToImport ();
			//			powerShellHost.SetRemoteSignedExecutionPolicy();
			//			UpdateFormatting();
			RedefineClearHostFunction ();
			//			DefineTabExpansionFunction();
			UpdateWorkingDirectory ();
		}

		void CreatePowerShellHost ()
		{
			var clearConsoleHostCommand = new ClearPackageManagementConsoleHostCommand (this);
			powerShellHost =
				powerShellHostFactory.CreatePowerShellHost (
					this.ScriptingConsole,
					GetNuGetVersion (),
					clearConsoleHostCommand,
					new EnvDTE.DTE ());
		}

		protected virtual Version GetNuGetVersion ()
		{
			return NuGetVersion.Version;
		}

		void AddModulesToImport ()
		{
			string module = addinPath.CmdletsAssemblyFileName;
			powerShellHost.ModulesToImport.Add (module);
		}

		//		void UpdateFormatting()
		//		{
		//			IEnumerable<string> fileNames = addinPath.GetPowerShellFormattingFileNames();
		//			powerShellHost.UpdateFormatting(fileNames);
		//		}
		//		
		void RedefineClearHostFunction ()
		{
			//			string command = "function Clear-Host { $host.PrivateData.ClearHost() }";
			string command = "function Clear-Host { (Get-Host).PrivateData.ClearHost() }";
			powerShellHost.ExecuteCommand(command);
		}

		//		void DefineTabExpansionFunction()
		//		{
		//			string command =
		//				"function TabExpansion($line, $lastWord) {" +
		//				"    return New-Object PSObject -Property @{ NoResult = $true }" +
		//				"}";
		//			powerShellHost.ExecuteCommand(command);
		//		}

		void WriteInfoBeforeFirstPrompt ()
		{
			WriteNuGetVersionInfo ();
			WriteHelpInfo ();
			WriteLine ();
		}

		void WriteNuGetVersionInfo ()
		{
			string versionInfo = String.Format ("NuGet {0}", powerShellHost.Version);
			WriteLine (versionInfo);
		}

		void UpdateWorkingDirectory ()
		{
			string command = "Invoke-UpdateWorkingDirectory";
			powerShellHost.ExecuteCommand (command);
		}

		void InitializePackageScriptsForOpenSolution ()
		{
			if (IsSolutionOpen) {
				string command = "Invoke-InitializePackages";
				powerShellHost.ExecuteCommand (command);
			}
		}

		void WriteLine (string message)
		{
			ScriptingConsole.WriteLine (message, ScriptingStyle.Out);
		}

		void WriteLine ()
		{
			WriteLine (String.Empty);
		}

		void WriteHelpInfo ()
		{
			string helpInfo = GetHelpInfo ();
			WriteLine (helpInfo);
		}

		protected virtual string GetHelpInfo ()
		{
			return "Type 'get-help NuGet' for more information.";
		}

		public void ProcessUserInput (string line)
		{
			PackageManagementBackgroundDispatcher.Dispatch (() => {
				ProcessLine (line);
				WritePrompt ();
			});
		}

		void ProcessLine (string line)
		{
			string preprocessedLine = PashCommandLinePreprocessor.Process (line);
			powerShellHost.ExecuteCommand (preprocessedLine);
		}

		public string GetActivePackageSource (string source)
		{
			if (source != null) {
				return source;
			}
			return ActivePackageSource?.PackageSource?.Name;
		}

		string GetActiveProjectName (string projectName)
		{
			if (projectName != null) {
				return projectName;
			}
			return DefaultProject.Name;
		}

		public void ShutdownConsole ()
		{
			//			if (ScriptingConsole != null) {
			//				ScriptingConsole.Dispose();
			//			}
		}

		public void ExecuteCommand (string command)
		{
			PackageManagementBackgroundDispatcher.Dispatch (() => {
				powerShellHost.ExecuteCommand (command);
				WritePrompt ();
			});
		}

		public void SetDefaultRunspace ()
		{
			//			powerShellHost.SetDefaultRunspace();
		}

		public IConsoleHostFileConflictResolver CreateFileConflictResolver (FileConflictAction? fileConflictAction)
		{
			return new ConsoleHostFileConflictResolver (packageEvents, fileConflictAction);
		}

		public IDisposable CreateEventsMonitor (INuGetProjectContext context)
		{
			return new ConsoleHostPackageEventsMonitor (context, packageEvents);
		}

		public IEnumerable<NuGetProject> GetNuGetProjects ()
		{
			var task = solutionManager.GetNuGetProjectsAsync ();
			task.Wait ();
			return task.Result.ToList ();
		}

		public NuGetProject GetNuGetProject (string projectName)
		{
			string activeProjectName = GetActiveProjectName (projectName);
			var task = solutionManager.GetNuGetProjectAsync (activeProjectName);
			task.Wait ();
			return task.Result;
		}

		public IEnumerable<PackageSource> LoadPackageSources ()
		{
			sourceRepositoryProvider = solutionManager.CreateSourceRepositoryProvider ();
			return sourceRepositoryProvider.PackageSourceProvider?.LoadPackageSources ();
		}

		public SourceRepository CreateRepository (PackageSource source)
		{
			return sourceRepositoryProvider?.CreateRepository (source);
		}

		public IEnumerable<SourceRepository> GetRepositories ()
		{
			if (sourceRepositoryProvider != null)
				return sourceRepositoryProvider.GetRepositories ();

			return Enumerable.Empty<SourceRepository> ();
		}

		public void ReloadPackageSources ()
		{
			registeredPackageSources.ReloadSettings ();
		}

		public ConsoleHostNuGetPackageManager CreatePackageManager ()
		{
			var consoleHostSolutionManager = (ConsoleHostSolutionManager)solutionManager;
			return new ConsoleHostNuGetPackageManager (consoleHostSolutionManager.GetMonoDevelopSolutionManager ());
		}

		public Task ExecuteScriptAsync (
			PackageIdentity identity,
			string packageInstallPath,
			string scriptRelativePath,
			IDotNetProject project,
			INuGetProjectContext nuGetProjectContext,
			bool throwOnFailure)
		{
			return scriptRunner.ExecuteScriptAsync (
				identity,
				packageInstallPath,
				scriptRelativePath,
				project,
				nuGetProjectContext,
				throwOnFailure);
		}

		public void OnSolutionUnloaded ()
		{
			scriptRunner.Reset ();
		}

		void SolutionLoaded (object sender, SolutionEventArgs e)
		{
			ExecuteCommand ("Invoke-InitializePackages");
		}

		public bool TryMarkInitScriptVisited (PackageIdentity package, PackageInitPS1State initPS1State)
		{
			return scriptRunner.TryMarkVisited (package, initPS1State);
		}
	}
}
