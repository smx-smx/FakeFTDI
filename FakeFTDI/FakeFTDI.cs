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
using RGiesecke.DllExport;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FakeFTDI
{
	public enum FtResult : int {
		OK,
		INVALID_HANDLE,
		DEVICE_NOT_FOUND,
		DEVICE_NOT_OPENED,
		IO_ERROR,
		INSUFFICIENT_RESOURCES,
		INVALID_PARAMETER,
		INVALID_BAUD_RATE,
		DEVICE_NOT_OPENED_FOR_ERASE,
		DEVICE_NOT_OPENED_FOR_WRITE,
		FAILED_TO_WRITE_DEVICE,
		EEPROM_READ_FAILED,
		EEPROM_WRITE_FAILED,
		EEPROM_ERASE_FAILED,
		EEPROM_NOT_PRESENT,
		EEPROM_NOT_PROGRAMMED,
		INVALID_ARGS,
		NOT_SUPPORTED,
		OTHER_ERROR
	}

	[Flags]
	public enum FtListFlags : uint
	{
		NumberOnly = 0x80000000,
		ByIndex = 0x40000000,
		All = 0x20000000
	}

	[Flags]
	public enum FtOpenFlags : uint
	{
		BySerialNumber = 1,
		ByDescription = 2,
		ByLocation = 4
	}

	class FakeFTDI
	{
		private static readonly IntPtr FAKE_HANDLE = new IntPtr(0x0000F00D);
		private static readonly StreamWriter log;
		const bool USE_SIGROK = true;

		static FakeFTDI() {
			ConsoleHelper.CreateConsole();
			Console.WriteLine("==== FakeFTDI v0.1 by Smx ;) ====");
			Console.WriteLine("=================================");

			log = new StreamWriter(new FileStream("FakeFTDI.log",
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.Read));
			log.BaseStream.SetLength(0);
		}

		private static byte[] MakeCString(string str) {
			byte[] buf = new byte[str.Length + 1];
			byte[] bytes = Encoding.ASCII.GetBytes(str);
			Array.Copy(bytes, buf, bytes.Length);
			return buf;
		}

		static void Trace(){
			string caller = new StackTrace()
				.GetFrame(1).GetMethod().Name;
			Console.WriteLine($"[{caller}]");
		}

		[DllExport("FT_Close", CallingConvention.StdCall)]
		public static FtResult FT_Close(IntPtr ftHandle){
			Trace();
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}
			return FtResult.OK;
		}
		[DllExport("FT_GetStatus", CallingConvention.StdCall)]
		public static FtResult FT_GetStatus(
			IntPtr ftHandle,
			IntPtr dwRxBytes, IntPtr dwTxBytes,
			IntPtr dwEventDWord
		){
			Trace();
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}
			return FtResult.OK;
		}

		[DllExport("FT_ListDevices", CallingConvention.StdCall)]
		public static FtResult FT_ListDevices(IntPtr pvArg1, IntPtr pvArg2, uint flags){
			Trace();

			FtListFlags listFlags = (FtListFlags)flags;
			FtOpenFlags openFlags = (FtOpenFlags)flags;

			if (!(listFlags.HasFlag(FtListFlags.ByIndex)
				&& openFlags.HasFlag(FtOpenFlags.ByDescription))
			){
				// not yet implemented
				return FtResult.OTHER_ERROR;
			}
			/*
			* the parameter pvArg1 is interpreted as the index of the device,
			* and the parameter pvArg2 is interpreted as a pointer to a buffer
			* to contain the appropriate string
			* Indexes are zero-based
			*/
			int index = pvArg1.ToInt32();

			if(index != 0){
				// not yet implemented
				return FtResult.DEVICE_NOT_FOUND;
			}

			byte[] deviceDescription = MakeCString("Mstar USB Debug Tool A");
			Marshal.Copy(deviceDescription, 0, pvArg2, deviceDescription.Length);
			return FtResult.OK;
		}


		[DllExport("FT_OpenEx", CallingConvention.StdCall)]
		public static FtResult FT_OpenEx(IntPtr pArg1, uint flags, IntPtr pHandle){
			Trace();

			FtOpenFlags openFlags = (FtOpenFlags)flags;
			if (!openFlags.HasFlag(FtOpenFlags.ByDescription)){
				// not yet implemented
				return FtResult.OTHER_ERROR;
			}

			Marshal.WriteIntPtr(pHandle, FAKE_HANDLE);
			return FtResult.OK;
		}

		[DllExport("FT_Read", CallingConvention.StdCall)]
		public static FtResult FT_Read(
			IntPtr ftHandle, IntPtr lpBuffer,
			uint nBufferSize, IntPtr lpBytesReturned
		){
			Trace();
			Console.WriteLine($"Read {nBufferSize}");
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}
		
			Marshal.WriteInt32(lpBytesReturned, (int)nBufferSize);
			return FtResult.OK;
		}

		[DllExport("FT_SetBitMode", CallingConvention.StdCall)]
		public static FtResult FT_SetBitMode(
			IntPtr ftHandle, byte ucMask, byte ucEnable
		) {
			Trace();
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}
			return FtResult.OK;
		}

		[DllExport("FT_SetLatencyTimer", CallingConvention.StdCall)]
		public static FtResult FT_SetLatencyTimer(IntPtr ftHandle, byte ucLatency){
			Trace();
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}
			return FtResult.OK;
		}

		private static void ClockBit(int d) {
			log.BaseStream.Write(new byte[] {
				// set data
				(byte)(d << 1),
				// assert clock
 				(byte)((d << 1) | 1),
				// deassert clock
				(byte)(d << 1)
			}, 0, 3);
		}

		[DllExport("FT_Write", CallingConvention.StdCall)]
		public static FtResult FT_Write(
			IntPtr ftHandle, IntPtr lpBuffer,
			uint nBufferSize, IntPtr lpBytesWritten
		) {
			Trace();
			Console.WriteLine($"Write {nBufferSize}");
			if (ftHandle != FAKE_HANDLE){
				return FtResult.INVALID_HANDLE;
			}

			byte[] buf = new byte[nBufferSize];
			Marshal.Copy(lpBuffer, buf, 0, buf.Length);

			FTCommandParser ftp = new FTCommandParser(buf);
			var commands = ftp.ParseBuffer().ToArray();
			foreach (var cmd in commands) {
				Console.WriteLine(cmd.ToString());
			}

			if (USE_SIGROK) {
				foreach (var cmd in commands) {
					if (cmd is FTSetAdBusCommand c1) {
						int SCL = c1.PinValues[0] ? 1 : 0;
						int SDA = c1.PinValues[1] ? 1 : 0;
						byte sample = (byte)((SCL & 1) | ((SDA & 1) << 1));
						byte[] buffer = new byte[] { sample };
						log.BaseStream.Write(buffer, 0, 1);
						log.Flush();
					} else if(cmd is FTClockByteOutCommand c2) {
						for (int i=0; i<8; i++) {
							int b = (c2.Byte >> (7-i)) & 1;
							ClockBit(b);
						}
						log.Flush();
					} else if(cmd is FTClockBitCommand c3) {
						int d = (c3.Value) ? 1 : 0;
						ClockBit(d);
						log.Flush();
					}
				}
			} else { 
				log.WriteLine($"== FT_Write({nBufferSize})");
				log.WriteLine(buf.HexDump());
				//log.BaseStream.Write(buf, 0, buf.Length);
				log.Flush();
			}

			Marshal.WriteInt32(lpBytesWritten, buf.Length);
			return FtResult.OK;
		}
	}
}
