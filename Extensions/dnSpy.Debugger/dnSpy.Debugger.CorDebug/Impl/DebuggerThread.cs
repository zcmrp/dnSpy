﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;
using System.Windows.Threading;
using dndbg.Engine;

namespace dnSpy.Debugger.CorDebug.Impl {
	sealed class DebuggerThread {
		Dispatcher Dispatcher { get; set; }

		readonly Thread debuggerThread;
		volatile bool terminate;
		AutoResetEvent callDispatcherRunEvent;
		readonly string threadName;

		public DebuggerThread(string threadName) {
			this.threadName = threadName;
			var autoResetEvent = new AutoResetEvent(false);
			callDispatcherRunEvent = new AutoResetEvent(false);
			debuggerThread = new Thread(() => DebuggerThreadProc(autoResetEvent));
			debuggerThread.IsBackground = true;
			debuggerThread.SetApartmentState(ApartmentState.STA);
			debuggerThread.Start();

			try {
				autoResetEvent.WaitOne();
				autoResetEvent.Dispose();
			}
			catch {
				Terminate();
				throw;
			}
		}

		void DebuggerThreadProc(AutoResetEvent autoResetEvent) {
			Thread.CurrentThread.Name = threadName;
			Dispatcher = Dispatcher.CurrentDispatcher;
			autoResetEvent.Set();

			callDispatcherRunEvent.WaitOne();
			callDispatcherRunEvent.Close();
			callDispatcherRunEvent = null;

			if (!terminate)
				Dispatcher.Run();
		}

		internal void CallDispatcherRun() => callDispatcherRunEvent.Set();

		internal void Terminate() {
			terminate = true;
			try { callDispatcherRunEvent?.Set(); } catch (ObjectDisposedException) { }
			if (Dispatcher != null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
				Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
		}

		public IDebugMessageDispatcher CreateDebugMessageDispatcher() => new WpfDebugMessageDispatcher(Dispatcher);

		public bool HasShutdownStarted => Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished;
		public void VerifyAccess() => Dispatcher.VerifyAccess();
		const DispatcherPriority DispPriority = DispatcherPriority.Send;
		public T Invoke<T>(Func<T> callback) => Dispatcher.Invoke(callback, DispPriority);
		public void Invoke(Action callback) => Dispatcher.Invoke(callback, DispPriority);
		public void BeginInvoke(Action callback) {
			if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
				Dispatcher.BeginInvoke(DispPriority, callback);
		}
	}
}
