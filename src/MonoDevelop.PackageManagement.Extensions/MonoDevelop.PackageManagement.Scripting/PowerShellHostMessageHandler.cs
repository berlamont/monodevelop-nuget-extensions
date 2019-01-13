﻿//
// PowerShellHostMessageHandler.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2019 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using ICSharpCode.Scripting;
using MonoDevelop.Core;
using MonoDevelop.PackageManagement.PowerShell.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace MonoDevelop.PackageManagement.Scripting
{
	class PowerShellHostMessageHandler
	{
		readonly IScriptingConsole scriptingConsole;

		public PowerShellHostMessageHandler (IScriptingConsole scriptingConsole)
		{
			this.scriptingConsole = scriptingConsole;
		}

		[JsonRpcMethod (Methods.LogName)]
		public void OnLogMessage (JToken arg)
		{
			try {
				var logMessage = arg.ToObject<LogMessageParams> ();
				switch (logMessage.Level) {
					case LogLevel.Error:
						scriptingConsole.WriteLine (logMessage.Message, ScriptingStyle.Error);
						break;
					case LogLevel.Warning:
						scriptingConsole.WriteLine (logMessage.Message, ScriptingStyle.Warning);
						break;
					case LogLevel.Verbose:
						scriptingConsole.WriteLine (logMessage.Message, ScriptingStyle.Out);
						break;
					case LogLevel.Debug:
						scriptingConsole.WriteLine (logMessage.Message, ScriptingStyle.Debug);
						break;
					default:
						scriptingConsole.WriteLine (logMessage.Message, ScriptingStyle.Out);
						break;
				}
			} catch (Exception ex) {
				LoggingService.LogError ("OnLogMessage error: {0}", ex);
			}
		}

		[JsonRpcMethod (Methods.ClearHostName)]
		public void OnClearHost ()
		{
			scriptingConsole.Clear ();
		}
	}
}
