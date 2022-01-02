#region License
/*
 * Copyright (C) 2022 Stefano Moioli <smxdev4@gmail.com>
 * This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held liable for any damages arising from the use of this software.
 * Permission is granted to anyone to use this software for any purpose, including commercial applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *  1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */
#endregion
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FakeFTDI
{
	public static class ConsoleHelper
	{
		private const int STD_INPUT_HANDLE = -10;
		private const int STD_OUTPUT_HANDLE = -11;
		private const int STD_ERROR_HANDLE = -12;
		//private const int MY_CODE_PAGE = Encoding.Get;
		private static readonly int MY_CODE_PAGE = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;

		public static void CreateConsole() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return;
			AllocConsole();
			SetConsoleCtrlHandler(null, true);

			IntPtr stdin = GetStdHandle(STD_INPUT_HANDLE);
			IntPtr stdout = GetStdHandle(STD_OUTPUT_HANDLE);
			IntPtr stderr = GetStdHandle(STD_ERROR_HANDLE);

			SafeFileHandle safeInHandle = new SafeFileHandle(stdin, false);
			SafeFileHandle safeOutHandle = new SafeFileHandle(stdout, false);
			SafeFileHandle safeErrHandle = new SafeFileHandle(stderr, false);

			FileStream inStream = new FileStream(safeInHandle, FileAccess.Read);
			FileStream outStream = new FileStream(safeOutHandle, FileAccess.Write);
			FileStream errStream = new FileStream(safeErrHandle, FileAccess.Write);

			Encoding encoding = Encoding.GetEncoding(MY_CODE_PAGE);

			StreamReader standardInput = new StreamReader(inStream, encoding);
			StreamWriter standardOutput = new StreamWriter(outStream, encoding);
			StreamWriter standardError = new StreamWriter(errStream, encoding);

			standardOutput.AutoFlush = true;
			standardError.AutoFlush = true;

			Console.SetIn(standardInput);
			Console.SetOut(standardOutput);
			Console.SetError(standardError);
		}

		delegate bool HandlerRoutine(uint dwControlType);
		private const UInt32 StdOutputHandle = 0xFFFFFFF5;

		// P/Invoke required:
		[DllImport("kernel32")]
		static extern bool AllocConsole();
		[DllImport("kernel32")]
		public static extern bool FreeConsole();
		[DllImport("kernel32")]
		private static extern IntPtr GetStdHandle(int nStdHandle);
		[DllImport("kernel32")]
		private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);
		[DllImport("kernel32")]
		static extern bool SetConsoleCtrlHandler(HandlerRoutine HandlerRoutine, bool Add);
	}
}
