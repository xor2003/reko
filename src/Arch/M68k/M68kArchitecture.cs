﻿#region License
/* 
 * Copyright (C) 1999-2015 John Källén.
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
using Reko.Core.Operators;
using Reko.Core.Rtl;
using Reko.Core.Serialization;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Reko.Arch.M68k
{
    [Designer("Reko.Arch.M68k.Design.M68kArchitectureDesigner,Reko.Arch.M68k.Design")]
    public class M68kArchitecture : ProcessorArchitecture
    {
        public M68kArchitecture()
        {
            InstructionBitSize = 16;
            FramePointerType = PrimitiveType.Pointer32;
            PointerType = PrimitiveType.Pointer32;
            WordWidth = PrimitiveType.Word32;
            CarryFlagMask = (uint)FlagM.CF;
            StackRegister = Registers.a7;
        }

        public override IEnumerable<MachineInstruction> CreateDisassembler(ImageReader rdr)
        {
            return M68kDisassembler.Create68020(rdr);
        }

        public IEnumerable<M68kInstruction> CreateDisassemblerImpl(ImageReader rdr)
        {
            return M68kDisassembler.Create68020(rdr);
        }

        public override IEqualityComparer<MachineInstruction> CreateInstructionComparer(Normalize norm)
        {
            throw new NotImplementedException();
        }

        public override ProcessorState CreateProcessorState()
        {
            return new M68kState(this);
        }

        public override BitSet CreateRegisterBitset()
        {
            return new BitSet((int)Registers.Max);
        }

        public override IEnumerable<Address> CreatePointerScanner(ImageMap map, ImageReader rdr, IEnumerable<Address> knownAddresses, PointerScannerFlags flags)
        {
            var knownLinAddresses = knownAddresses.Select(a => a.ToUInt32()).ToHashSet();
            return new M68kPointerScanner(rdr, knownLinAddresses, flags).Select(li => Address.Ptr32(li));
        }

        public override ImageReader CreateImageReader(LoadedImage image, Address addr)
        {
            return new BeImageReader(image, addr);
        }

        public override ImageReader CreateImageReader(LoadedImage image, ulong offset)
        {
            return new BeImageReader(image, offset);
        }

        public override RegisterStorage GetRegister(int i)
        {
            return Registers.GetRegister(i);
        }

        public override RegisterStorage GetRegister(string name)
        {
            var r = Registers.GetRegister(name);
            if (r == RegisterStorage.None)
                throw new ArgumentException(string.Format("'{0}' is not a register name.", name));
            return r;
        }

        public override RegisterStorage[] GetRegisters()
        {
            return Registers.regs;
        }

        public override bool TryGetRegister(string name, out RegisterStorage reg)
        {
            throw new NotImplementedException();
        }

        public override FlagGroupStorage GetFlagGroup(uint grf)
        {
            throw new NotImplementedException();
        }

        public override FlagGroupStorage GetFlagGroup(string name)
        {
            throw new NotImplementedException();
        }

        public override RegisterStorage GetSubregister(RegisterStorage reg, int offset, int width)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RtlInstructionCluster> CreateRewriter(ImageReader rdr, ProcessorState state, Frame frame, IRewriterHost host)
        {
            return new Rewriter(this, rdr, (M68kState)state, frame, host);
        }

        public override Expression CreateStackAccess(Frame frame, int offset, DataType dataType)
        {
            return new MemoryAccess(new BinaryExpression(
                Operator.IAdd, FramePointerType,
                frame.EnsureRegister(StackRegister), Constant.Word32(offset)),
                dataType);
        }

        public override Address MakeAddressFromConstant(Constant c)
        {
            return Address.Ptr32(c.ToUInt32());
        }

        public override Address ReadCodeAddress(int size, ImageReader rdr, ProcessorState state)
        {
            throw new NotImplementedException();
        }

        //$REVIEW: shouldn't this be flaggroup?
        private static RegisterStorage[] flagRegisters = {
            new RegisterStorage("C", 0, 0, PrimitiveType.Bool),
            new RegisterStorage("V", 0, 0, PrimitiveType.Bool),
            new RegisterStorage("Z", 0, 0, PrimitiveType.Bool),
            new RegisterStorage("N", 0, 0, PrimitiveType.Bool),
            new RegisterStorage("X", 0, 0, PrimitiveType.Bool),
        };

        public override string GrfToString(uint grf)
        {
            StringBuilder s = new StringBuilder();
            for (int r = 0; grf != 0; ++r, grf >>= 1)
            {
                if ((grf & 1) != 0)
                    s.Append(flagRegisters[r].Name);
            }
            return s.ToString();
        }

        public override bool TryParseAddress(string txtAddress, out Address addr)
        {
            return Address.TryParse32(txtAddress, out addr);
        }
    }
}