﻿#region License
/* 
 * Copyright (C) 1999-2010 John Källén.
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

using Decompiler.Core;
using Decompiler.Core.Expressions;
using Decompiler.Core.Rtl;
using Decompiler.Core.Lib;
using Decompiler.Core.Machine;
using Decompiler.Core.Operators;
using Decompiler.Core.Types;
using System;
using System.Collections.Generic;
using System.Text;
using IEnumerable = System.Collections.IEnumerable;
using IEnumerator = System.Collections.IEnumerator;

namespace Decompiler.Arch.Intel
{
    /// <summary>
    /// Rewrites x86 instructions into a stream of low-level RTL-like instructions.
    /// </summary>
    public partial class X86Rewriter : Rewriter, Rewriter2
    {
        private IRewriterHost2 host;
        private IntelArchitecture arch;
        private Frame frame;
        private LookaheadEnumerator<DisassembledInstruction> dasm;
        private RtlEmitter emitter;
        private OperandRewriter2 orw;
        private DisassembledInstruction di;
        private IntelState state;

        [Obsolete("Phasing out old rewriter")]
        public X86Rewriter(IProcedureRewriter prw)
            : base(prw)
        {
        }

        public X86Rewriter(
            IntelArchitecture arch,
            IRewriterHost2 host,
            IntelState state,
            ImageReader rdr, 
            Frame frame)
            : base(null)
        {
            this.host = host;
            this.arch = arch;
            this.frame = frame;
            this.state = state;
            this.dasm = new LookaheadEnumerator<DisassembledInstruction>(CreateDisassemblyStream(rdr, arch.WordWidth));
        }

        protected virtual IEnumerable<DisassembledInstruction> CreateDisassemblyStream(ImageReader rdr, PrimitiveType defaultWordSize)
        {
            var d = new IntelDisassembler(rdr, defaultWordSize);
            while (rdr.IsValid)
            {
                var addr = d.Address;
                var instr = d.Disassemble();
                var length = (uint)(d.Address - addr);
                yield return new DisassembledInstruction(addr, instr, length);
            }
        }

        #region old Rewriter overrides; delete when done.
        public override void GrowStack(int bytes)
        {
            throw new NotImplementedException();
        }

        public override void ConvertInstructions(MachineInstruction[] instrs, Address[] addrs, uint[] deadOutFlags, Address addrEnd, CodeEmitter emitter)
        {
            throw new NotImplementedException();
        }

        public override void EmitCallAndReturn(Procedure callee)
        {
            throw new NotImplementedException();
        }
        #endregion

        public IEnumerator<RtlInstruction> GetEnumerator()
        {
            while (dasm.MoveNext())
            {
                di = dasm.Current;
                var instrs = new List<RtlInstruction>();
                emitter = new RtlEmitter(di.Address, di.Length, instrs);
                orw = new OperandRewriter2(arch, frame);
                switch (di.Instruction.code)
                {
                default:
                    throw new NotImplementedException(string.Format("Intel opcode {0} not supported yet.", di.Instruction.code));
                case Opcode.adc: RewriteAdcSbb(BinaryOperator.Add); break;
                case Opcode.add: RewriteAddSub(BinaryOperator.Add); break;
                case Opcode.and: RewriteLogical(BinaryOperator.And); break;
                case Opcode.bswap: RewriteBswap(); break;
                case Opcode.call: RewriteCall(di.Instruction.op1, di.Instruction.op1.Width); break;
                case Opcode.cmp: RewriteCmp(); break;
                case Opcode.dec: RewriteIncDec(-1); break;
                case Opcode.enter: RewriteEnter(); break;
                case Opcode.@in: RewriteIn(); break;
                case Opcode.inc: RewriteIncDec(1); break;
                case Opcode.@int: RewriteInt(); break;
                case Opcode.jmp: RewriteJmp(); break;
                case Opcode.ja: RewriteConditionalGoto(ConditionCode.UGT, di.Instruction.op1); break;
                case Opcode.jbe: RewriteConditionalGoto(ConditionCode.ULE, di.Instruction.op1); break;
                case Opcode.jc: RewriteConditionalGoto(ConditionCode.ULT, di.Instruction.op1); break;
                case Opcode.jge: RewriteConditionalGoto(ConditionCode.GE, di.Instruction.op1); break;
                case Opcode.jg: RewriteConditionalGoto(ConditionCode.GT, di.Instruction.op1); break;
                case Opcode.jl: RewriteConditionalGoto(ConditionCode.LT, di.Instruction.op1); break;
                case Opcode.jle: RewriteConditionalGoto(ConditionCode.LE, di.Instruction.op1); break;
                case Opcode.jnc: RewriteConditionalGoto(ConditionCode.UGE, di.Instruction.op1); break;
                case Opcode.jno: RewriteConditionalGoto(ConditionCode.NO, di.Instruction.op1); break;
                case Opcode.jns: RewriteConditionalGoto(ConditionCode.NS, di.Instruction.op1); break;
                case Opcode.jnz: RewriteConditionalGoto(ConditionCode.NE, di.Instruction.op1); break;
                case Opcode.jo: RewriteConditionalGoto(ConditionCode.OV, di.Instruction.op1); break;
                case Opcode.jpe: RewriteConditionalGoto(ConditionCode.PE, di.Instruction.op1); break;
                case Opcode.jpo: RewriteConditionalGoto(ConditionCode.PO, di.Instruction.op1); break;
                case Opcode.js: RewriteConditionalGoto(ConditionCode.SG, di.Instruction.op1); break;
                case Opcode.jz: RewriteConditionalGoto(ConditionCode.EQ, di.Instruction.op1); break;
                case Opcode.lea: RewriteLea(); break;
                case Opcode.loop: RewriteLoop(0, ConditionCode.EQ); break;
                case Opcode.mov: RewriteMov(); break;
                case Opcode.neg: RewriteNeg(); break;
                case Opcode.or: RewriteLogical(BinaryOperator.Or); break;
                case Opcode.push: RewritePush(); break;
                case Opcode.pop: RewritePop(); break;
                case Opcode.ret: RewriteRet(); break;
                case Opcode.sbb: RewriteAdcSbb(BinaryOperator.Sub); break;
                case Opcode.sub: RewriteAddSub(BinaryOperator.Sub); break;
                case Opcode.test: RewriteTest(); break;
                case Opcode.xor: RewriteLogical(BinaryOperator.Xor); break;
                }
                    
                foreach (var ri in instrs)
                {
                    yield return ri;
                }
            }
        }

        public Expression PseudoProc(string name, PrimitiveType retType, params Expression[] args)
        {
            PseudoProcedure ppp = host.EnsurePseudoProcedure(name, retType, args.Length);
            return PseudoProc(ppp, retType, args);
        }

        public Expression PseudoProc(PseudoProcedure ppp, PrimitiveType retType, params Expression[] args)
        {
            if (args.Length != ppp.Arity)
                throw new ArgumentOutOfRangeException(
                    string.Format("Pseudoprocedure {0} expected {1} arguments, but was passed {2}.",
                    ppp.Name,
                    ppp.Arity,
                    args.Length));

            return emitter.Fn(new ProcedureConstant(arch.PointerType, ppp), retType, args);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Breaks up very common case of x86:
        /// <code>
        ///		op [memaddr], reg
        /// </code>
        /// into the equivalent:
        /// <code>
        ///		tmp := [memaddr] op reg;
        ///		store([memaddr], tmp);
        /// </code>
        /// </summary>
        /// <param name="opDst"></param>
        /// <param name="src"></param>
        /// <param name="forceBreak">if true, forcibly splits the assignments in two if the destination is a memory store.</param>
        /// <returns></returns>
        public RtlAssignment EmitCopy(MachineOperand opDst, Expression src, bool forceBreak)
        {
            Expression dst = SrcOp(opDst);
            Identifier idDst = dst as Identifier;
            if (idDst != null || !forceBreak)
            {
                MemoryAccess acc = dst as MemoryAccess;
                if (acc != null)
                {
                    return emitter.Assign(acc, src);
                }
                else
                {
                    return emitter.Assign(idDst, src);
                }
            }
            else
            {
                Identifier tmp = frame.CreateTemporary(opDst.Width);
                emitter.Assign(tmp, src);
                MemoryAccess ea = orw.CreateMemoryAccess((MemoryOperand)opDst, state);
                return emitter.Assign(ea, tmp);
            }
        }




        private Expression SrcOp(MachineOperand opSrc)
        {
            return orw.Transform(opSrc, opSrc.Width, state);
        }

        private Expression SrcOp(MachineOperand opSrc, PrimitiveType dstWidth)
        {
            return orw.Transform(opSrc, dstWidth, state);
        }
    }
}