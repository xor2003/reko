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

using NUnit.Framework;
using Reko.Typing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reko.Core;
using Reko.Core.Types;
using Reko.UnitTests.Mocks;

namespace Reko.UnitTests.Typing
{
    [TestFixture]
    public class TypeCollectorTests : TypingTestBase
    {
        protected override void RunTest(Program program, string outputFile)
        {
            FileUnitTester fut = null;
            try
            {
                fut = new FileUnitTester(outputFile);
                var factory = program.TypeFactory;
                var store = program.TypeStore;

                var aen = new ExpressionNormalizer(program.Platform.PointerType);
                var eqb = new EquivalenceClassBuilder(factory, store);

                var tyco = new TypeCollector(factory, store, program, null);

                aen.Transform(program);
                eqb.Build(program);
                tyco.CollectTypes();
            }
            catch (Exception ex)
            {
                fut.TextWriter.WriteLine(ex.Message);
                fut.TextWriter.WriteLine(ex.StackTrace);
                throw;
            }
            finally
            {
                DumpProgAndStore(program, fut);
                fut.Dispose();
            }
        }

        private void RunTest(Action<ProcedureBuilder> doBuild, string outputFile)
        {
            var pb = new ProgramBuilder();
            pb.Add("proc1", doBuild);
            var program = pb.BuildProgram();
            RunTest(program, outputFile);
        }

        private void DumpProgAndStore(Program prog, FileUnitTester fut)
        {
            foreach (Procedure proc in prog.Procedures.Values)
            {
                proc.Write(false, fut.TextWriter);
                fut.TextWriter.WriteLine();
            }

            prog.TypeStore.Write(fut.TextWriter);
            fut.AssertFilesEqual();
        }

        [Test]
        public void TycoMemStore()
        {
            RunTest(Fragments.MemStore, "Typing/TycoMemStore.txt");
        }

        [Test]
        public void TycoIndexedDisplacement()
        {
            RunTest(m =>
            {
                var ds = m.Temp(PrimitiveType.SegmentSelector, "ds");
                var bx = m.Temp(PrimitiveType.Word16, "bx");
                var si = m.Temp(PrimitiveType.Int16, "si");
                m.Assign(bx, m.SegMemW(ds, m.Word16(0xC00)));
                m.SegStore(ds, m.IAdd(
                                m.IAdd(bx, 10),
                                si),
                           m.Byte(0xF8));
            }, "Typing/TycoIndexedDisplacement.txt");
        }

        [Test]
        public void TycoNestedStructsPtr()
        {
            RunTest(m =>
            {
                var eax = m.Reg32("eax", 0);
                var ecx = m.Reg32("ecx", 1);
                var strInner = new StructureType("strInner", 8, true)
                {
                    Fields = {
                        { 0, PrimitiveType.Real32, "innerAttr00" },
                        { 4, PrimitiveType.Int32, "innerAttr04" },
                    }
                };
                var str = new StructureType("str", 8, true)
                {
                    Fields = {
                        { 0, new Pointer(strInner, 4), "strAttr00" },
                        { 4, PrimitiveType.Int32, "strAttr04" },
                    }
                };
                var v = m.Frame.EnsureStackArgument(4, new Pointer(str, 4));
                m.Declare(eax, m.Load(PrimitiveType.Word32, v));
                m.Declare(ecx, m.Load(PrimitiveType.Word32, eax));
            }, "Typing/TycoNestedStructsPtr.txt");
        }
    }
}
