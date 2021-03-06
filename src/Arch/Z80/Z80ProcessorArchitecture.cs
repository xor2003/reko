﻿#region License
/* 
 * Copyright (C) 1999-2016 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Machine;
using Reko.Core.Rtl;
using Reko.Core.Serialization;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Arch.Z80
{
    public class Z80ProcessorArchitecture : ProcessorArchitecture
    {
        public Z80ProcessorArchitecture()
        {
            this.InstructionBitSize = 8;
            this.FramePointerType = PrimitiveType.Ptr16;
            this.PointerType = PrimitiveType.Ptr16;
            this.WordWidth = PrimitiveType.Word16;
            this.StackRegister = Registers.sp;
            this.CarryFlagMask = (uint)FlagM.CF;
        }

        public override IEnumerable<MachineInstruction> CreateDisassembler(ImageReader imageReader)
        {
            return new Z80Disassembler(imageReader);
        }

        public override ImageReader CreateImageReader(MemoryArea image, Address addr)
        {
            return new LeImageReader(image, addr);
        }

        public override ImageReader CreateImageReader(MemoryArea image, Address addrBegin, Address addrEnd)
        {
            return new LeImageReader(image, addrBegin, addrEnd);
        }

        public override ImageReader CreateImageReader(MemoryArea image, ulong offset)
        {
            return new LeImageReader(image, offset);
        }

        public override ImageWriter CreateImageWriter()
        {
            return new LeImageWriter();
        }

        public override ImageWriter CreateImageWriter(MemoryArea mem, Address addr)
        {
            return new LeImageWriter(mem, addr);
        }

        public override IEqualityComparer<MachineInstruction> CreateInstructionComparer(Normalize norm)
        {
            throw new NotImplementedException();
        }

        public override ProcessorState CreateProcessorState()
        {
            return new Z80ProcessorState(this);
        }

        public override IEnumerable<Address> CreatePointerScanner(SegmentMap map, ImageReader rdr, IEnumerable<Address> knownLinAddresses, PointerScannerFlags flags)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RtlInstructionCluster> CreateRewriter(ImageReader rdr, ProcessorState state, Frame frame, IRewriterHost host)
        {
            return new Z80Rewriter(this, rdr, state, frame, host);
        }

        public override RegisterStorage GetRegister(int i)
        {
            throw new NotImplementedException();
        }

        public override RegisterStorage GetRegister(string name)
        {
            throw new NotImplementedException();
        }

        public override RegisterStorage[] GetRegisters()
        {
            return Registers.All;
        }

        public override bool TryGetRegister(string name, out RegisterStorage reg)
        {
            throw new NotImplementedException();
        }

        public override RegisterStorage GetSubregister(RegisterStorage reg, int offset, int width)
        {
            if (offset == 0 && reg.BitSize == (ulong)width)
                return reg;
            Dictionary<uint, RegisterStorage> dict;
            if (!Registers.SubRegisters.TryGetValue(reg, out dict))
                return null;
            RegisterStorage subReg;
            if (!dict.TryGetValue((uint)(offset + width * 16), out subReg))
                return null;
            return subReg;
        }

        public override RegisterStorage GetWidestSubregister(RegisterStorage reg, HashSet<RegisterStorage> regs)
        {
            ulong mask = regs.Where(b => b.OverlapsWith(reg)).Aggregate(0ul, (a, r) => a | r.BitMask);
            Dictionary<uint, RegisterStorage> subregs;
            if ((mask & reg.BitMask) == reg.BitMask)
                return reg;
            RegisterStorage rMax = null;
            if (Registers.SubRegisters.TryGetValue(reg, out subregs))
            {
                foreach (var subreg in subregs.Values)
                {
                    if ((subreg.BitMask & mask) == subreg.BitMask &&
                        (rMax == null || subreg.BitSize > rMax.BitSize))
                    {
                        rMax = subreg;
                    }
                }
            }
            return rMax;
        }

        public override FlagGroupStorage GetFlagGroup(uint grf)
        {
            throw new NotImplementedException();
        }

        public override FlagGroupStorage GetFlagGroup(string name)
        {
            throw new NotImplementedException();
        }

        public override Core.Expressions.Expression CreateStackAccess(Frame frame, int cbOffset, DataType dataType)
        {
            throw new NotImplementedException();
        }

        public override Address MakeAddressFromConstant(Constant c)
        {
            return Address.Ptr16(c.ToUInt16());
        }

        public override Address ReadCodeAddress(int size, ImageReader rdr, ProcessorState state)
        {
            throw new NotImplementedException();
        }

		public override string GrfToString(uint grf)
		{
			StringBuilder s = new StringBuilder();
			for (int r = Registers.S.Number; grf != 0; ++r, grf >>= 1)
			{
				if ((grf & 1) != 0)
					s.Append(Registers.GetRegister(r).Name);
			}
			return s.ToString();
		}

        public override bool TryParseAddress(string txtAddress, out Address addr)
        {
            return Address.TryParse16(txtAddress, out addr);
        }
    }

    public static class Registers
    {
        public static readonly RegisterStorage b = new RegisterStorage("b", 1, 0, PrimitiveType.Byte);
        public static readonly RegisterStorage c = new RegisterStorage("c", 1, 8, PrimitiveType.Byte);
        public static readonly RegisterStorage d = new RegisterStorage("d", 2, 0, PrimitiveType.Byte);
        public static readonly RegisterStorage e = new RegisterStorage("e", 2, 8, PrimitiveType.Byte);
        public static readonly RegisterStorage h = new RegisterStorage("h", 3, 0, PrimitiveType.Byte);
        public static readonly RegisterStorage l = new RegisterStorage("l", 3, 0, PrimitiveType.Byte);
        public static readonly RegisterStorage a = new RegisterStorage("a", 0, 0, PrimitiveType.Byte);

        public static readonly RegisterStorage bc = new RegisterStorage("bc", 1, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage de = new RegisterStorage("de", 2, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage hl = new RegisterStorage("hl", 3, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage sp = new RegisterStorage("sp", 4, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage ix = new RegisterStorage("ix", 5, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage iy = new RegisterStorage("iy", 6, 0, PrimitiveType.Word16);
        public static readonly RegisterStorage af = new RegisterStorage("af", 0, 0, PrimitiveType.Word16);

        public static readonly FlagRegister f = new FlagRegister("f", PrimitiveType.Byte);

        public static readonly RegisterStorage i = new RegisterStorage("i", 8, 0, PrimitiveType.Byte);
        public static readonly RegisterStorage r = new RegisterStorage("r", 9, 0, PrimitiveType.Byte);

        public static readonly RegisterStorage S = new RegisterStorage("S", 20, 0, PrimitiveType.Bool);
        public static readonly RegisterStorage Z = new RegisterStorage("Z", 21, 0, PrimitiveType.Bool);
        public static readonly RegisterStorage P = new RegisterStorage("P", 22, 0, PrimitiveType.Bool);
        public static readonly RegisterStorage C = new RegisterStorage("C", 23, 0, PrimitiveType.Bool);

        internal static RegisterStorage[] All;
        internal static Dictionary<RegisterStorage, Dictionary<uint, RegisterStorage>> SubRegisters;

        static Registers()
        {
            All = new RegisterStorage[] {
             b ,
             c ,
             d ,
             e ,

             h ,
             l ,
             a ,
             null,
               
             bc,
             de,
             hl,
             sp,
             ix,
             iy,
             af,
             null,

             i ,
             r ,
             null,
             null,

             S ,
             Z ,
             P ,
             C ,
            };

            SubRegisters = new Dictionary<
                RegisterStorage, 
                Dictionary<uint, RegisterStorage>>
            {
                {
                    bc, new Dictionary<uint, RegisterStorage>
                    {
                        { 0x08, Registers.c },
                        { 0x88, Registers.b },
                    }
                },
                {
                    de, new Dictionary<uint, RegisterStorage>
                    {
                        { 0x08, Registers.e },
                        { 0x88, Registers.d },
                    }
                },
                {
                    hl, new Dictionary<uint, RegisterStorage>
                    {
                        { 0x08, Registers.l },
                        { 0x88, Registers.h },
                    }
                }
            };
        }

        internal static RegisterStorage GetRegister(int r)
        {
            return All[r];
        }
    }

    [Flags]
    public enum FlagM : byte
    {
        SF = 1,             // sign
        ZF = 2,             // zero
        PF = 4,             // overflow / parity
        CF = 8,             // carry
    }

    public enum CondCode
    {
        nz,
        z,
        nc,
        c,
        po,
        pe,
        p,
        m,
    }
}