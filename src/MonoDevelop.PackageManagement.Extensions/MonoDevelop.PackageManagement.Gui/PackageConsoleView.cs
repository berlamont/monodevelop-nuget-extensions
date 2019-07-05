﻿// 
// PackageConsoleView.cs
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using MonoDevelop.Components;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.PackageManagement.Scripting;
using NuGetConsole;

namespace MonoDevelop.PackageManagement
{
	enum LogLevel
	{
		Default,
		Error,
		Critical,
		Warning,
		Message,
		Info,
		Debug
	}

	class PackageConsoleView : ConsoleView, IScriptingConsole
	{
		const int DefaultMaxVisibleColumns = 160;

		int maxVisibleColumns = 0;
		int originalWidth = -1;

		TextTag debugTag;
		TextTag errorTag;
		TextTag warningTag;

		const int TabExpansionTimeout = 3; // seconds.
		readonly PackageConsoleCompletionWidget completionWidget;
		readonly CompletionListWindow completionWindow;
		CancellationTokenSource cancellationTokenSource;
		bool keyHandled;

		public PackageConsoleView ()
		{
			AddTags ();
			// HACK - to allow text to appear before first prompt.
			PromptString = String.Empty;
			base.Clear ();
			
			SetFont (IdeServices.FontService.MonospaceFont);

			completionWidget = new PackageConsoleCompletionWidget (this);
			completionWindow = new CompletionListWindow ();

			TextView.FocusInEvent += (o, args) => {
				TextViewFocused?.Invoke (this, args);
			};
			TextView.FocusOutEvent += TextViewFocusOutEvent;
			TextView.KeyReleaseEvent += TextViewKeyReleaseEvent;
		}

		void AddTags ()
		{
			errorTag = new TextTag ("error");
			errorTag.Background = "#dc3122";
			errorTag.Foreground = "white";
			errorTag.Weight = Pango.Weight.Bold;
			Buffer.TagTable.Add (errorTag);

			warningTag = new TextTag ("warning");
			warningTag.Foreground = "black";
			warningTag.Background = "yellow";
			Buffer.TagTable.Add (warningTag);

			debugTag = new TextTag ("debug");
			debugTag.Foreground = "darkgrey";
			Buffer.TagTable.Add (debugTag);
		}

		public event EventHandler TextViewFocused;
		public event EventHandler MaxVisibleColumnsChanged;

		void WriteOutputLine (string message, ScriptingStyle style)
		{
			WriteOutput (message + Environment.NewLine, GetLogLevel (style));
		}

		LogLevel GetLogLevel (ScriptingStyle style)
		{
			switch (style) {
			case ScriptingStyle.Error:
				return LogLevel.Error;
			case ScriptingStyle.Warning:
				return LogLevel.Warning;
			case ScriptingStyle.Debug:
				return LogLevel.Debug;
			default:
				return LogLevel.Default;
			}
		}
		
		public bool ScrollToEndWhenTextWritten { get; set; }
		
		public void SendLine (string line)
		{
		}
		
		public void SendText (string text)
		{
		}
		
		public void WriteLine ()
		{
			Runtime.RunInMainThread (() => {
				WriteOutput ("\n");
			}).Wait ();
		}
		
		public void WriteLine (string text, ScriptingStyle style)
		{
			Runtime.RunInMainThread (() => {
				if (style == ScriptingStyle.Prompt) {
					WriteOutputLine (text, style);
					ConfigurePromptString ();
					Prompt (true);
				} else {
					WriteOutputLine (text, style);
				}
			}).Wait ();
		}
		
		void ConfigurePromptString()
		{
			PromptString = "PM> ";
		}
		
		public void Write (string text, ScriptingStyle style)
		{
			Runtime.RunInMainThread (() => {
				if (style == ScriptingStyle.Prompt) {
					ConfigurePromptString ();
					Prompt (false);
				} else {
					WriteOutput (text);
				}
			}).Wait ();
		}

		public void WriteOutput (string line, LogLevel logLevel)
		{
			TextTag tag = GetTag (logLevel);
			TextIter start = Buffer.EndIter;

			if (tag == null) {
				Buffer.Insert (ref start, line);
			} else {
				Buffer.InsertWithTags (ref start, line, tag);
			}
			Buffer.PlaceCursor (Buffer.EndIter);
			TextView.ScrollMarkOnscreen (Buffer.InsertMark);
		}

		TextTag GetTag (LogLevel logLevel)
		{
			switch (logLevel) {
				case LogLevel.Critical:
				case LogLevel.Error:
					return errorTag;
				case LogLevel.Warning:
					return warningTag;
				case LogLevel.Debug:
					return debugTag;
				default:
					return null;
			}
		}

		public string ReadLine (int autoIndentSize)
		{
			throw new NotImplementedException();
		}
		
		public string ReadFirstUnreadLine ()
		{
			throw new NotImplementedException();
		}
		
		public int GetMaximumVisibleColumns ()
		{
			if (maxVisibleColumns > 0) {
				return maxVisibleColumns;
			}
			return DefaultMaxVisibleColumns;
		}

		void IScriptingConsole.Clear ()
		{
			Runtime.RunInMainThread (() => {
				ClearWithoutPrompt ();
			});
		}

		public void ClearWithoutPrompt ()
		{
			Buffer.Text = "";

			// HACK: Clear scriptLines string. This is done in ConsoleView's Clear method but we do
			// not want a prompt to be displayed. Should investigate to see if Clear can be called
			// instead and fix whatever the problem was with the prompt.
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			FieldInfo field = typeof (ConsoleView).GetField ("scriptLines", flags);
			if (field != null) {
				field.SetValue (this, string.Empty);
			}
		}

		protected override void OnSizeAllocated (Rectangle allocation)
		{
			base.OnSizeAllocated (allocation);

			int windowWidth = Allocation.Width;
			if (originalWidth == windowWidth) {
				return;
			}

			int originalMaxVisibleColumns = maxVisibleColumns;
			if (windowWidth > 0) {
				using (var layout = new Pango.Layout (PangoContext)) {
					layout.FontDescription = IdeServices.FontService.MonospaceFont;
					layout.SetText ("W");
					layout.GetSize (out int characterWidth, out int _);
					if (characterWidth > 0) {
						double characterPixelWidth = characterWidth / Pango.Scale.PangoScale;
						maxVisibleColumns = (int)(windowWidth / characterPixelWidth);
					} else {
						maxVisibleColumns = DefaultMaxVisibleColumns;
					}
				}
			} else {
				maxVisibleColumns = DefaultMaxVisibleColumns;
			}

			originalWidth = windowWidth;
			if (originalMaxVisibleColumns != maxVisibleColumns) {
				MaxVisibleColumnsChanged?.Invoke (this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Not sure why Copy command does not work with the keyboard shortcut. If the currently focused
		/// window is a TextView then it should work. The Immediate Pad does not have this problem and
		/// it does not have its own Copy command handler. It seems that the IdeApp.Workebench.RootWindow
		/// is the TextArea for the text editor not the pad.
		/// </summary>
		[CommandHandler (EditCommands.Copy)]
		void CopyText ()
		{
			// This is based on what the DefaultCopyCommandHandler does.
			var clipboard = Gtk.Clipboard.Get (Gdk.Atom.Intern ("CLIPBOARD", false));
			TextView.Buffer.CopyClipboard (clipboard);
		}

		/// <summary>
		/// Tab key pressed
		///	  If caret in read - only region ignore
		///	  If completion list window active select item and close window
		///	  Else trigger completion async
		///
		/// Trigger completion
		///	  If completion list window shown end completion session
		///
		///	  Get caret position and current line text
		///	  GetExpansionsAsync passing caret position and line text with cancellation token that times out
		///	  If one item returned use it
		///	  Else start completion session and show window
		/// 
		/// https://github.com/NuGet/NuGet.Client/blob/3803820961f4d61c06d07b179dab1d0439ec0d91/src/NuGet.Clients/NuGet.Console/WpfConsole/WpfConsoleKeyProcessor.cs
		/// </summary>
		protected override bool ProcessKeyPressEvent (KeyPressEventArgs args)
		{
			var keyChar = (char)args.Event.Key;
			ModifierType modifier = args.Event.State;
			Gdk.Key key = args.Event.Key;

			if ((key == Gdk.Key.Down || key == Gdk.Key.Up)) {
				keyChar = '\0';
			}

			if (completionWindow.Visible) {
				if (key == Gdk.Key.Return) {
					// AutoSelect is off and the completion window will only
					// complete if tab is pressed. So cheat by converting return
					// into Tab. Otherwise the command will be run.
					key = Gdk.Key.Tab;
					keyChar = '\t';
				}
				var descriptor = KeyDescriptor.FromGtk (key, keyChar, modifier);
				keyHandled = completionWindow.PreProcessKeyEvent (descriptor);
				if (keyHandled) {
					return true;
				}
			}

			if (cancellationTokenSource != null) {
				CancelCurrentCompletion ();
			}

			if (key != Gdk.Key.Tab) {
				return base.ProcessKeyPressEvent (args);
			}

			int caretIndex = completionWidget.Position;
			TriggerCompletionAsync (InputLine, caretIndex).Ignore ();

			return true;
		}

		void CancelCurrentCompletion ()
		{
			cancellationTokenSource.Cancel ();
			cancellationTokenSource.Dispose ();
			cancellationTokenSource = null;
		}

		async Task TriggerCompletionAsync (string line, int caretIndex)
		{
			cancellationTokenSource = new CancellationTokenSource (TabExpansionTimeout * 1000);

			SimpleExpansion simpleExpansion = await TryGetExpansionsAsync (
				line,
				caretIndex,
				cancellationTokenSource.Token);

			if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested) {
				return;
			}

			if (simpleExpansion?.Expansions == null) {
				return;
			}

			if (simpleExpansion.Expansions.Count == 1) {
				ReplaceTabExpansion (simpleExpansion);
				return;
			}

			CompletionDataList list = CreateCompletionList (simpleExpansion);

			var context = completionWidget.CreateCodeCompletionContext (simpleExpansion.Start);

			completionWindow.ShowListWindow ('\0', list, completionWidget, context);
		}

		static CompletionDataList CreateCompletionList (SimpleExpansion simpleExpansion)
		{
			var list = new CompletionDataList {
				AutoSelect = false
			};

			foreach (string expansion in simpleExpansion.Expansions) {
				list.Add (new CompletionData (expansion));
			}

			list.AddKeyHandler (new PackageConsoleCompletionKeyHandler ());

			return list;
		}

		async Task<SimpleExpansion> TryGetExpansionsAsync (
			string line,
			int caretIndex,
			CancellationToken token)
		{
			try {
				return await CommandExpansion.GetExpansionsAsync (line, caretIndex, token);
			} catch (OperationCanceledException) {
				return null;
			} catch (Exception ex) {
				LoggingService.LogError ("GetExpansionsAsync error.", ex);
				return null;
			}
		}

		void ReplaceTabExpansion (SimpleExpansion expansion)
		{
			TextIter start = Buffer.GetIterAtOffset (InputLineBegin.Offset + expansion.Start);
			TextIter end = Buffer.GetIterAtOffset (start.Offset + expansion.Length);

			Buffer.Delete (ref start, ref end);
			Buffer.Insert (ref start, expansion.Expansions [0]);
		}

		void TextViewFocusOutEvent (object sender, FocusOutEventArgs args)
		{
			HideWindow ();
		}

		void TextViewKeyReleaseEvent (object sender, KeyReleaseEventArgs args)
		{
			if (keyHandled || !completionWindow.Visible)
				return;

			var keyChar = (char)args.Event.Key;
			ModifierType modifier = args.Event.State;
			Gdk.Key key = args.Event.Key;

			var descriptor = KeyDescriptor.FromGtk (key, keyChar, modifier);
			completionWindow.PostProcessKeyEvent (descriptor);
		}

		void HideWindow ()
		{
			completionWindow.HideWindow ();
		}

		internal TextView GetTextView ()
		{
			return TextView;
		}

		protected override void UpdateInputLineBegin ()
		{
			completionWidget?.OnUpdateInputLineBegin ();
			base.UpdateInputLineBegin ();
		}

		public ICommandExpansion CommandExpansion { get; set; }

		TaskCompletionSource<string> userInputTask;

		public Task<string> PromptForInput (string message)
		{
			return Runtime.RunInMainThread (() => {
				string originalPromptString = PromptString;
				try {
					PromptString = message;
					Prompt (false);

					userInputTask = new TaskCompletionSource<string> ();
					return userInputTask.Task;
				} finally {
					PromptString = originalPromptString;
				}
			});
		}

		protected override void ProcessInput (string line)
		{
			if (userInputTask != null) {
				// Waiting for user input. Bypass the usual processing.
				WriteOutput ("\n");

				userInputTask.TrySetResult (line);
				userInputTask = null;
			} else {
				base.ProcessInput (line);
			}
		}

		public void StopWaitingForPromptInput ()
		{
			if (userInputTask != null) {
				userInputTask.TrySetCanceled ();
				userInputTask = null;
				WriteOutput ("\n");
			}
		}
	}
}
