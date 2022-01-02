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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FakeFTDI
{
	public enum FtI2CCommand : byte
	{
		/// <summary>
		/// clock bytes out MSB first on
		/// clock falling edge
		/// </summary>
		ClockBytesOutMSBFalling = 0x11,

		/// <summary>
		/// clock data byte in on clk rising edge
		/// </summary>
		ClockByteInMSBRising = 0x20,

		/// <summary>
		/// clock data bits out on clk falling edge
		/// </summary>
		ClockBitOut = 0x13,

		SetADBus = 0x80,
		ReadDataBitsLowByte = 0x81,
		DisableInternalLoopback = 0x85,
		SetClockDivisor = 0x86,
		SendAnswerImmediate = 0x87,
	}

	public enum FTDirection
	{
		In,
		Out
	}

	public interface FTCommand { }

	public class FTIgnoredCommand : FTCommand { }

	public class FTClockBitCommand : FTCommand
	{
		public bool Value { get; private set; }

		public FTClockBitCommand(bool value) {
			this.Value = value;
		}

		public override string ToString() {
			return $"ClockBit({Value})";
		}
	}

	public class FTClockByteInCommand : FTCommand
	{ }

	public class FTClockByteOutCommand : FTCommand
	{
		public byte Byte { get; private set; }
		public FTClockByteOutCommand(byte b) {
			Byte = b;
		}

		public override string ToString() {
			return $"ClockByteOut(0x{Byte:X2})";
		}
	}

	public class FTCommandParser : IDisposable
	{
		private BinaryReader rdr;

		public FTCommandParser(byte[] buf) {
			this.rdr = new BinaryReader(new MemoryStream(buf));
		}

		private FtI2CCommand ReadCommand() {
			byte cmd = rdr.ReadByte();
			if(!Enum.IsDefined(typeof(FtI2CCommand), cmd)) {
				throw new InvalidDataException($"Unrecognized command {cmd:X2}");
			}

			return (FtI2CCommand)cmd;
		}

		private void Discard(int amount) {
			rdr.ReadBytes(amount);
		}

		private FTClockByteInCommand ParseClockByteInMSB() {
			ushort length = rdr.ReadUInt16();
			// length of 0 means 1 byte
			if (length != 0x00) {
				throw new NotSupportedException("only 1 byte supported");
			}
			return new FTClockByteInCommand();
		}

		private FTClockByteOutCommand ParseClockBytesOutMSB() {
			ushort length = rdr.ReadUInt16();
			// length of 0 means 1 byte
			if (length != 0x00) {
				throw new NotSupportedException("only 1 byte supported");
			}
			byte b = rdr.ReadByte();

			return new FTClockByteOutCommand(b);
		}

		private FTClockBitCommand ParseClockBit() {
			byte length = rdr.ReadByte();
			// length of 0 means 1 byte
			if (length != 0x00) {
				throw new NotSupportedException("only 1 byte supported");
			}
			byte value = rdr.ReadByte();

			bool logicValue;
			if(value == 0xFF) {
				// 1 : NACK, we're stopping an I2C Transaction here
				logicValue = true;
			} else {
				// 0 : ACK, we're starting an I2C Transaction here
				logicValue = false;
			}

			return new FTClockBitCommand(logicValue);
		}

		private FTSetAdBusCommand ParseSetAdBus() {
			byte pinData = rdr.ReadByte();
			byte pinDirection = rdr.ReadByte();
			return new FTSetAdBusCommand(pinData, pinDirection);
		}

		private FTCommand ParseCommand() {
			FtI2CCommand cmd = ReadCommand();
			switch (cmd) {
				case FtI2CCommand.ReadDataBitsLowByte:
					return new FTReadDataBitsLowByteCommand();
				case FtI2CCommand.SendAnswerImmediate:
					return new FTSendAnswerImmediateCommand();
				case FtI2CCommand.DisableInternalLoopback:
					return new FTDisableInternalLoopbackCommand();
				case FtI2CCommand.SetClockDivisor:
					Discard(2);
					return new FTSetClockDivisorCommand();
				case FtI2CCommand.SetADBus:
					return ParseSetAdBus();
				case FtI2CCommand.ClockBytesOutMSBFalling:
					return ParseClockBytesOutMSB();
				case FtI2CCommand.ClockBitOut:
					return ParseClockBit();
				case FtI2CCommand.ClockByteInMSBRising:
					return ParseClockByteInMSB();
			}
			return null;
		}

		public IEnumerable<FTCommand> ParseBuffer() {
			while (rdr.BaseStream.Position < rdr.BaseStream.Length) {
				FTCommand cmd = ParseCommand();
				if (cmd == null) break;
				//if (cmd is FTIgnoredCommand) continue;
				yield return cmd;
			}
		}

		public void Dispose() {
			rdr.Dispose();
		}
	}

	public class FTDisableInternalLoopbackCommand : FTCommand { }

	public class FTSendAnswerImmediateCommand : FTCommand { }

	public class FTReadDataBitsLowByteCommand : FTCommand { }

	public class FTSetClockDivisorCommand : FTCommand { }

	public class FTSetAdBusCommand : FTCommand
	{
		public bool[] PinValues = new bool[8];
		public FTDirection[] PinDirections = new FTDirection[8];

		public FTSetAdBusCommand(byte pinData, byte pinDirection) {
			for(int i=0; i<8; i++) {
				bool value = ((pinData >> i) & 1) == 1;
				FTDirection dir = ((pinDirection >> i) & 1) == 1 ? FTDirection.Out : FTDirection.In;

				PinValues[i] = value;
				PinDirections[i] = dir;
			}
		}

		public FTDirection GetDirection(int pinNum) {
			return PinDirections[pinNum];
		}

		public bool GetValue(int pinNum) {
			return PinValues[pinNum];
		}

		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			var delim = Environment.NewLine + "\t";
			var pinList = string.Join(delim, Enumerable.Range(0, 7)
				.Select(i => {
					string dir = PinDirections[i] == FTDirection.In ? "in" : "out";
					if(PinDirections[i] == FTDirection.In) {
						return $"AD{i}[{dir}]";
					}
					string value = PinValues[i] ? "1" : "0";
					return $"AD{i}[{dir}] -> {value}";
				}));
			return "SetADBus {" + delim + pinList + Environment.NewLine + "}";
		}
	}
}
