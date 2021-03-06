#region License
/* 
 * Copyright (C) 1999-2016 John K�ll�n.
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
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Typing;
using Reko.UnitTests.Fragments;
using Reko.UnitTests.Mocks;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Reko.Core.Serialization;

namespace Reko.UnitTests.Typing
{
    [TestFixture]
    public class TypedExpressionRewriterTests : TypingTestBase
    {
        private TypedExpressionRewriter ter;
        private ExpressionNormalizer aen;
        private EquivalenceClassBuilder eqb;
        private TraitCollector coll;
        private DataTypeBuilder dtb;
        private TypeVariableReplacer tvr;
        private TypeTransformer trans;
        private ComplexTypeNamer ctn;
        private List<StructureField> userDefinedGlobals;
        private Dictionary<string, ProcedureSignature> userDefinedProcedures;
        private Dictionary<Address, ImageSegment> imageSegments;

        [SetUp]
        public void Setup()
        {
            userDefinedGlobals = new List<StructureField>();
            userDefinedProcedures = new Dictionary<string, ProcedureSignature>();
            imageSegments = new Dictionary<Address, ImageSegment>();
        }

        protected override void RunTest(Program program, string outputFile)
        {
            using (FileUnitTester fut = new FileUnitTester(outputFile))
            {
                fut.TextWriter.WriteLine("// Before ///////");
                DumpProgram(program, fut.TextWriter);

                SetupPreStages(program);
                aen.Transform(program);
                eqb.Build(program);
#if OLD
                coll = new TraitCollector(program.TypeFactory, program.TypeStore, dtb, program);
                coll.CollectProgramTraits(program);
#else
                var coll = new TypeCollector(program.TypeFactory, program.TypeStore, program, null);
                coll.CollectTypes();
#endif
                program.TypeStore.BuildEquivalenceClassDataTypes(program.TypeFactory);
                program.TypeStore.Dump();
                tvr.ReplaceTypeVariables();
                trans.Transform();
                ctn.RenameAllTypes(program.TypeStore);
                ter = new TypedExpressionRewriter(program, null);
                try
                {
                    ter.RewriteProgram(program);
                }
                catch (Exception ex)
                {
                    fut.TextWriter.WriteLine("** Exception **");
                    fut.TextWriter.WriteLine(ex);
                }
                finally
                {
                    fut.TextWriter.WriteLine("// After ///////");
                    DumpProgAndStore(program, fut);
                }
            }
        }

        protected void RunStringTest(Action<ProcedureBuilder> pb, string expectedOutput)
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("test", pb);
            pm.BuildProgram();
            RunStringTest(pm.Program, expectedOutput);
        }

        protected void RunStringTest(Program program, string expectedOutput)
        {
            var sw = new StringWriter();
            sw.WriteLine("// Before ///////");
            DumpProgram(program, sw);

            SetupPreStages(program);
            aen.Transform(program);
            eqb.Build(program);
#if OLD
            coll = new TraitCollector(program.TypeFactory, program.TypeStore, dtb, program);
            coll.CollectProgramTraits(program);
#else
            var coll = new TypeCollector(program.TypeFactory, program.TypeStore, program, null);
            coll.CollectTypes();
#endif
            program.TypeStore.BuildEquivalenceClassDataTypes(program.TypeFactory);
            tvr.ReplaceTypeVariables();
            trans.Transform();
            ctn.RenameAllTypes(program.TypeStore);
            program.TypeStore.Dump();

            var ter = new TypedExpressionRewriter(program, null);
            try
            {
                ter.RewriteProgram(program);
            }
            catch (Exception ex)
            {
                sw.WriteLine("** Exception **");
                sw.WriteLine(ex);
                throw;
            }
            finally
            {
                sw.WriteLine("// After ///////");
                DumpProgram(program, sw);
            }
            var sActual = sw.ToString();
            if (expectedOutput != sActual)
            {
                Debug.Print(sActual);
                Assert.AreEqual(expectedOutput, sActual);
            }
        }

        private ProgramBuilder CreateProgramBuilder(uint linearAddress, int size)
        {
            return new ProgramBuilder(
                new MemoryArea(Address.Ptr32(linearAddress), new byte[size]));
        }

        private void DumpProgram(Program program, TextWriter tw)
        {
            foreach (Procedure proc in program.Procedures.Values)
            {
                proc.Write(false, tw);
                tw.WriteLine();
            }
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

        public void SetupPreStages(Program program)
        {
            foreach (var f in userDefinedGlobals)
            {
                program.GlobalFields.Fields.Add(f);
            }
            foreach (var s in imageSegments.Values)
            {
                program.SegmentMap.Segments.Add(s.Address, s);
            }
            aen = new ExpressionNormalizer(program.Platform.PointerType);
            eqb = new EquivalenceClassBuilder(program.TypeFactory, program.TypeStore);
            dtb = new DataTypeBuilder(program.TypeFactory, program.TypeStore, program.Platform);
            tvr = new TypeVariableReplacer(program.TypeStore);
            trans = new TypeTransformer(program.TypeFactory, program.TypeStore, program);
            ctn = new ComplexTypeNamer();
        }

        private void SetType(Expression e, DataType t)
        {
            e.DataType = t;
        }

        private void Given_GlobalVariable(uint linAddress, string name, DataType dt)
        {
            userDefinedGlobals.Add(new StructureField((int)linAddress, dt, name));
        }

        private ExternalProcedure Given_Procedure(string name, params DataType [] argTypes)
        {
            var sig = new ProcedureSignature(
                null,
                argTypes.Select((argType, i) => new Identifier(
                        "arg" + i, 
                        argType,
                        new StackArgumentStorage(i * 4, argType)))
                    .ToArray());
            return new ExternalProcedure(name, sig);
        }

        [Test]
        public void TerComplex()
        {
            Program program = new Program();
            program.SegmentMap = new SegmentMap(Address.Ptr32(0x0010000));
            program.Architecture = new FakeArchitecture();
            program.Platform = new DefaultPlatform(null, program.Architecture);
            SetupPreStages(program);
            Identifier id = new Identifier("v0", PrimitiveType.Word32, null);
            Expression cmp = MemLoad(id, 4, PrimitiveType.Word32);

            program.Globals.Accept(eqb);
            cmp.Accept(aen);
            cmp.Accept(eqb);
            coll = new TraitCollector(program.TypeFactory, program.TypeStore, dtb, program);
            cmp.Accept(coll);
            dtb.BuildEquivalenceClassDataTypes();

            tvr.ReplaceTypeVariables();
            trans.Transform();
            ctn.RenameAllTypes(program.TypeStore);

            ter = new TypedExpressionRewriter(program, null);
            cmp = cmp.Accept(ter);
            Assert.AreEqual("v0->dw0004", cmp.ToString());
        }

        [Test]
        public void TerPtrPtrInt()
        {
            var mock = CreateProgramBuilder(0x0010000, 0x1000);
            mock.Add(new PtrPtrIntMock());
            RunTest(mock.BuildProgram(), "Typing/TerPtrPtrInt.txt");
        }

        [Test]
       [Ignore("scanning-development")]
        public void TerUnionIntReal()
        {
            var mock = CreateProgramBuilder(0x10000, 0x1000);
            mock.Add(new UnionIntRealMock());
            RunTest(mock.BuildProgram(), "Typing/TerUnionIntReal.txt");
        }

        [Test]
        public void TerConstantUnion()
        {
            var mock = CreateProgramBuilder(0x10000, 0x1000);
            mock.Add(new ConstantUnionMock());
            RunTest(mock.BuildProgram(), "Typing/TerConstantUnion.txt");
        }

        [Test]
        public void TerConstants()
        {
            var arch = new FakeArchitecture();
            Program program = new Program(
                new SegmentMap(Address.Ptr32(0x10000)),
                arch,
                new DefaultPlatform(null, arch));
            SetupPreStages(program);
            Constant r = Constant.Real32(3.0F);
            Constant i = Constant.Int32(1);
            Identifier x = new Identifier("x", PrimitiveType.Word32, null);
            Assignment ass = new Assignment(x, r);
            TypeVariable tvR = r.TypeVariable = program.TypeFactory.CreateTypeVariable();
            TypeVariable tvI = i.TypeVariable = program.TypeFactory.CreateTypeVariable();
            TypeVariable tvX = x.TypeVariable = program.TypeFactory.CreateTypeVariable();
            program.TypeStore.TypeVariables.AddRange(new TypeVariable[] { tvR, tvI, tvX });
            UnionType u = program.TypeFactory.CreateUnionType(null, null, new DataType[] { r.DataType, i.DataType });
            tvR.OriginalDataType = r.DataType;
            tvI.OriginalDataType = i.DataType;
            tvX.OriginalDataType = x.DataType;
            tvR.DataType = u;
            tvI.DataType = u;
            tvX.DataType = u;
            ctn.RenameAllTypes(program.TypeStore);
            var ter = new TypedExpressionRewriter(program, null);
            Instruction instr = ter.TransformAssignment(ass);
            Assert.AreEqual("x.u0 = 3.0F", instr.ToString());
        }

        [Test]
        public void TerVector()
        {
            ProgramBuilder mock = new ProgramBuilder();
            mock.Add(new VectorFragment());
            RunTest(mock.BuildProgram(), "Typing/TerVector.txt");
        }

        [Test]
        public void TerGlobalVariables()
        {
            ProgramBuilder mock = CreateProgramBuilder(0x10000000, 0x1000);
            mock.Add(new GlobalVariablesMock());
            RunTest(mock.BuildProgram(), "Typing/TerGlobalVariables.txt");
        }

        [Test]
        public void TerSegmentedMemoryPointer()
        {
            ProgramBuilder mock = new ProgramBuilder();
            mock.Add(new SegmentedMemoryPointerMock());
            RunTest(mock.BuildProgram(), "Typing/TerSegmentedMemoryPointer.txt");
        }

        [Test]
        public void TerSegMemPtr2()
        {
            ProgramBuilder mock = new ProgramBuilder();
            mock.Add(new SegmentedMemoryPointerMock2());
            RunTest(mock.BuildProgram(), "Typing/TerSegMemPtr2.txt");
        }

        [Test]
        public void TerSegMem3()
        {
            ProgramBuilder mock = new ProgramBuilder();
            mock.Add(new SegMem3Mock());
            RunTest(mock.BuildProgram(), "Typing/TerSegMem3.txt");
        }

        [Test]
        public void TerArrayConstantPointers()
        {
            var pp = CreateProgramBuilder(0x00123000,4000);
            pp.Add("Fn", m =>
            {
                Identifier a = m.Local32("a");
                Identifier i = m.Local32("i");
                m.Assign(a, 0x00123456);		// array pointer
                m.Store(m.IAdd(a, m.IMul(i, 8)), m.Int32(42));
            });
            RunTest(pp.BuildProgram(), "Typing/TerArrayConstantPointers.txt");
        }

        [Test]
        public void TerReg00008()
        {
            RunTest16("Fragments/regressions/r00008.asm", "Typing/TerReg00008.txt");
        }

        [Test]
        [Ignore("scanning-development")]
        public void TerReg00011()
        {
            RunTest16("Fragments/regressions/r00011.asm", "Typing/TerReg00011.txt");
        }

        [Ignore("scanning-development")]
        [Test]
        public void TerReg00012()
        {
            RunTest16("Fragments/regressions/r00012.asm", "Typing/TerReg00012.txt");
        }

        [Test]
        public void TerAddNonConstantToPointer()
        {
            ProgramBuilder prog = new ProgramBuilder();
            prog.Add("proc1", m =>
            {
                Identifier i = m.Local16("i");
                Identifier p = m.Local16("p");

                m.Store(p, m.Word16(4));
                m.Store(m.IAdd(p, 4), m.Word16(4));
                m.Assign(p, m.IAdd(p, i));
            });
            RunTest(prog.BuildProgram(), "Typing/TerAddNonConstantToPointer.txt");
        }

        [Test]
        public void TerSignedCompare()
        {
            ProgramBuilder prog = new ProgramBuilder();
            prog.Add("proc1", m =>
            {
                Identifier p = m.Local32("p");
                Identifier ds = m.Local16("ds");
                ds.DataType = PrimitiveType.SegmentSelector;
                Identifier ds2 = m.Local16("ds2");
                ds2.DataType = PrimitiveType.SegmentSelector;
                m.Assign(ds2, ds);
                m.Store(
                    m.SegMem(PrimitiveType.Bool, ds, m.Word16(0x5400)),
                    m.Lt(m.SegMemW(ds, m.Word16(0x5404)), m.Word16(20)));
                m.Store(m.SegMemW(ds2, m.Word16(0x5404)), m.Word16(0));
            });
            RunTest(prog.BuildProgram(), "Typing/TerSignedCompare.txt");
        }

        [Test]
        public void TerDereferenceSignedCompare()
        {
            ProgramBuilder prog = CreateProgramBuilder(0x5000, 0x1000);
            prog.Add("proc1", m =>
            {
                Identifier p = m.Local32("p");
                Identifier ds = m.Local16("ds");
                ds.DataType = PrimitiveType.SegmentSelector;
                Identifier ds2 = m.Local16("ds2");
                ds2.DataType = PrimitiveType.SegmentSelector;
                m.Assign(ds2, ds);
                m.Store(
                    m.SegMem(PrimitiveType.Bool, ds, m.Word16(0x5400)),
                    m.Lt(
                        m.SegMemW(ds, m.IAdd(m.SegMemW(ds, m.Word16(0x5404)), 4)),
                        m.Word16(20)));
                m.Store(m.SegMemW(ds2, m.IAdd(m.SegMemW(ds2, m.Word16(0x5404)), 4)), m.Word16(0));
                m.Return();
            });
            RunTest(prog.BuildProgram(), "Typing/TerDereferenceSignedCompare.txt");
        }

        [Test]
        public void TerFlatDereferenceSignedCompare()
        {
            ProgramBuilder prog = CreateProgramBuilder(0x5400, 0x1000);
            prog.Add("proc1", m =>
            {
                Identifier ds = m.Local32("ds");
                Identifier ds2 = m.Local32("ds2");
                m.Assign(ds2, ds);
                m.Store(
                    m.IAdd(ds, m.Word32(0x5400)),
                    m.Lt(
                        m.LoadW(m.IAdd(m.LoadDw(m.IAdd(ds, m.Word32(0x5404))), 4)),
                        m.Word16(20)));
                m.Store(m.IAdd(m.LoadDw(m.IAdd(ds2, m.Word32(0x5404))), 4), m.Word16(0));
            });
            RunTest(prog.BuildProgram(), "Typing/TerFlatDereferenceSignedCompare.txt");
        }

        [Test]
        public void TerComparison()
        {
            ProgramBuilder prog = new ProgramBuilder();
            prog.Add("proc1", m =>
            {
                Identifier p = m.Local32("p");
                Expression fetch = m.Load(new Pointer(new StructureType("foo", 8), 4), m.IAdd(p, 4));
                m.Assign(m.LocalBool("f"), m.Lt(fetch, m.Word32(0x00001028)));
            });
            RunTest(prog.BuildProgram(), "Typing/TerComparison.txt");
        }

        [Test]
        public void TerUnionConstants()
        {
            ProgramBuilder prog = new ProgramBuilder();
            prog.Add("proc1", m =>
            {
                Identifier bx = m.Local16("bx");
                m.Assign(bx, m.Shr(bx, 2));     // makes bx unsigned uint16
                m.Assign(m.LocalBool("f"), m.Lt(bx, 4));    // makes bx also signed; assembler bug, but forces a union.
                m.Assign(bx, m.Word16(4));          // what type should 4 have?
            });
            RunTest(prog.BuildProgram(), "Typing/TerUnionConstants.txt");
        }

        [Test]
        public void TerOffsetInArrayLoop()
        {
            ProgramBuilder pm = new ProgramBuilder();
            pm.Add("proc1", m =>
            {
                var ds = m.Local16("ds");
                var cx = m.Local16("cx");
                var di = m.Local16("di");
                m.Assign(di, 0);
                m.Label("lupe");
                m.SegStore(ds, m.IAdd(di, 0x5388), m.Word16(0));
                m.Assign(di, m.IAdd(di, 2));
                m.Assign(cx, m.ISub(cx, 1));
                m.BranchIf(m.Ne(cx, 0), "lupe");
                m.Return();
            });
            RunTest(pm, "Typing/TerOffsetInArrayLoop.txt");
        }

        [Test]
        public void TerSegmentedLoadLoad()
        {
            ProgramBuilder pm = new ProgramBuilder();
            pm.Add("proc1", m =>
            {
                var ds = m.Local(PrimitiveType.SegmentSelector, "ds");
                var bx = m.Local(PrimitiveType.Word16, "bx");
                m.SegStore(ds, m.Word16(0x300), m.SegMemW(ds, m.SegMemW(ds, bx)));
                m.Return();
            });
            RunTest(pm, "Typing/TerSegmentedLoadLoad.txt");
        }

        [Test]
        public void TerIntelIndexedAddressingMode()
        {
            ProgramBuilder m = new ProgramBuilder();
            m.Add(new IntelIndexedAddressingMode());
            RunTest(m.BuildProgram(), "Typing/TerIntelIndexedAddressingMode.txt");
        }

        [Test]
        [Ignore("scanning-development")]
        public void TerReg00016()
        {
            RunHexTest("fragments/regressions/r00016.dchex", "Typing/TerReg00016.txt");
        }

        [Test]
        [Ignore("scanning-development")]
        public void TerReg00017()
        {
            RunTest32("Fragments/regressions/r00017.asm", "Typing/TerReg00017.txt");
        }

        [Test]
        public void TerCallTable()
        {
            var pb = new ProgramBuilder();
            pb.Add(new IndirectCallFragment());
            RunTest(pb.BuildProgram(), "Typing/TerCallTable.txt");
        }

        [Test]
        //[Ignore("scanning-development")]
        public void TerSegmentedCall()
        {
            var pb = new ProgramBuilder();
            pb.Add(new SegmentedCallFragment());
            RunTest(pb.BuildProgram(), "Typing/TerSegmentedCall.txt");
        }

        [Test]
        public void TerPointerChain()
        {
            var pb = new ProgramBuilder();
            pb.Add(new PointerChainFragment());
            RunTest(pb.BuildProgram(), "Typing/TerPointerChain.txt");
        }

        [Test]
        public void TerStaggeredArrays()
        {
            ProgramBuilder prog = new ProgramBuilder();
            prog.Add(new StaggeredArraysFragment());
            RunTest(prog.BuildProgram(), "Typing/TerStaggeredArrays.txt");
        }

        [Test]
        public void TerArrayLoopMock()
        {
            var pb = CreateProgramBuilder(0x04000000, 0x9000);
            pb.Add(new ArrayLoopMock());
            RunTest(pb, "Typing/TerArrayLoopMock.txt");
        }

        [Test]
        public void TerArrayExpression()
        {
            var m = new ProgramBuilder();
            m.Add(new ArrayExpressionFragment());
            RunTest(m.BuildProgram(), "Typing/TerArrayExpression.txt");
        }

        [Test]
        public void TerDeclaration()
        {
            ProgramBuilder pm = new ProgramBuilder();
            pm.Add("proc1", m =>
            {
                var ax = m.Reg16("ax", 0);
                var rand = new ExternalProcedure(
                    "rand",
                    new ProcedureSignature(
                        new Identifier("ax", PrimitiveType.Int16, ax.Storage),
                        new Identifier[0]));
                m.Declare(ax, m.Fn(rand));
                m.Store(m.Word16(0x1300), ax);
                m.Return();
            });
            RunTest(pm, "Typing/TerDeclaration.txt");
        }

        [Test]
        public void TerShortArray()
        {
            var pm = CreateProgramBuilder(0x00001000, 0x1000);
            pm.Add("proc1", m =>
                {
                    var ebp = m.Reg32("ebp", 4);
                    var esp = m.Reg32("esp", 5);
                    var ecx = m.Reg32("ecx", 1);
                    var eax = m.Reg32("eax", 0);

                    m.Assign(ebp, m.ISub(m.Frame.FramePointer, 4));
                    m.Assign(eax, m.LoadDw(m.IAdd(ebp, 0x0C)));
                    m.Assign(ecx, m.LoadDw(m.IAdd(ebp, 0x08)));
                    m.Assign(eax, m.Cast(PrimitiveType.Int32, m.Load(PrimitiveType.Int16,
                        m.IAdd(ecx, m.IMul(eax, 2)))));
                    m.Store(m.Word32(0x1234), eax);
                });
            RunTest(pm, "Typing/TerShortArray.txt");
        }

        [Test]
        public void TerArray()
        {
            var pm = CreateProgramBuilder(0x00F000, 0x2000);
            pm.Add("proc1", m =>
            {
                var eax = m.Reg32("eax", 0);
                var ecx = m.Reg32("ecx", 1);
                var eax_2 = m.Reg32("eax_2", 0);

                // eax_2 = (int32) ecx[eax];
                m.Assign(
                    eax_2,
                    m.Cast(PrimitiveType.Int32,
                    m.Load(PrimitiveType.Int16,
                        m.IAdd(
                            ecx,
                            m.IAdd(
                                m.IMul(eax, 2),
                                100)))));
                m.Store(m.Word32(0x010000), eax_2);
            });
            RunTest(pm, "Typing/TerArray.txt");
        }

        [Test]
        public void Ter2Integer()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var eax = m.Reg32("eax", 0);
                m.Store(m.Word32(0x01000), eax);
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	Mem0[0x00001000:word32] = eax
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	globals->dw1000 = eax
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void Ter2PtrToInt16()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var eax = m.Reg32("eax", 0);
                m.Store(m.Word32(0x01000), m.LoadW(eax));
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	Mem0[0x00001000:word16] = Mem0[eax:word16]
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	globals->w1000 = *eax
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void Ter2PtrToStruct()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var eax = m.Reg32("eax", 0);
                m.Declare(eax, null);
                m.Store(m.Word32(0x01000), m.LoadW(eax));
                m.Store(m.Word32(0x01002), m.LoadW(m.IAdd(eax, 2)));
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	word32 eax
	Mem0[0x00001000:word16] = Mem0[eax:word16]
	Mem0[0x00001002:word16] = Mem0[eax + 0x00000002:word16]
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	struct Eq_2 * eax
	globals->w1000 = eax->w0000
	globals->w1002 = eax->w0002
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void Ter2ThreeStarProgrammer()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var eax1 = m.Reg32("eax1", 0);
                var eax2 = m.Reg32("eax2", 0);
                var eax3 = m.Reg32("eax3", 0);
                m.Declare(eax1, null);
                m.Assign(eax2, m.LoadDw(eax1));
                m.Assign(eax3, m.LoadDw(eax2));
                m.Store(m.Word32(0x01004), m.Load(PrimitiveType.Real32, eax3));
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	word32 eax1
	eax2 = Mem0[eax1:word32]
	eax3 = Mem0[eax2:word32]
	Mem0[0x00001004:real32] = Mem0[eax3:real32]
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	real32 *** eax1
	eax2 = *eax1
	eax3 = *eax2
	globals->r1004 = *eax3
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void Ter2LinkedList()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var r1 = m.Reg32("r1", 1);
                m.Declare(r1, null);
                m.Assign(r1, m.LoadDw(r1));
                m.Store(m.Word32(0x01004), m.Load(
                    PrimitiveType.Char,
                    m.IAdd(
                        m.LoadDw(
                            m.LoadDw(r1)),
                        4)));
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	word32 r1
	r1 = Mem0[r1:word32]
	Mem0[0x00001004:char] = Mem0[Mem0[Mem0[r1:word32]:word32] + 0x00000004:char]
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	struct Eq_2 * r1
	r1 = r1->ptr0000
	globals->b1004 = r1->ptr0000->ptr0000->b0004
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void Ter2AddrOfLinkedList()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
            {
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);
                m.Declare(r1, null);
                m.Declare(r2, null);
                m.Assign(r1, m.LoadDw(r1));
                m.Store(m.Word32(0x01004), m.Load(
                    PrimitiveType.Char,
                    m.IAdd(
                        m.LoadDw(
                            m.LoadDw(r1)),
                        4)));
                m.Assign(r2, m.IAdd(r1, 4));
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	word32 r1
	word32 r2
	r1 = Mem0[r1:word32]
	Mem0[0x00001004:char] = Mem0[Mem0[Mem0[r1:word32]:word32] + 0x00000004:char]
	r2 = r1 + 0x00000004
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	struct Eq_2 * r1
	ptr32 r2
	r1 = r1->ptr0000
	globals->b1004 = r1->ptr0000->ptr0000->b0004
	r2 = &r1->b0004
proc1_exit:

";
            #endregion
            RunStringTest(pm.BuildProgram(), sExp);
        }

        [Test]
        public void TerStruct()
        {
            var sExp =
            #region Expected
@"// Before ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	eax = Mem0[0x00001200:word32]
	Mem0[eax:word32] = eax
	Mem0[eax + 0x00000004:word32] = eax
test_exit:

// After ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	eax = globals->ptr1200
	eax->ptr0000 = eax
	eax->ptr0004 = eax
test_exit:

";
            #endregion

            RunStringTest(m =>
            {
                var eax = m.Reg32("eax", 0);
                m.Assign(eax, m.LoadDw(m.Word32(0x1200)));
                m.Store(eax, eax);
                m.Store(m.IAdd(eax, 4), eax);
            },sExp);
        }

        [Test]
        public void TerDeclaration2()
        {
            var sExp = @"// Before ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	word32 foo = 0x00000001
test_exit:

// After ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	word32 foo = 0x00000001
test_exit:

";
            RunStringTest(m =>
            {
                m.Declare(PrimitiveType.Word32, "foo", m.Word32(1));
            }, sExp);
        }

        [Test(Description = "Tests that user-provided global names are used in the output")]
        public void TerNamedGlobal()
        {
            var sExp =
            #region Expected
@"// Before ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	func(0x00001000)
test_exit:

// After ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	func(globals->arrayBlobs)
test_exit:

";
            #endregion

            var sBlob = new StructureType("blob_t", 16);
            var func = Given_Procedure("func", new Pointer(sBlob, 4));
            Given_GlobalVariable(
                0x0001000,
                "arrayBlobs",
                new ArrayType(
                    new TypeReference(sBlob), 5));
            RunStringTest(m =>
            {
                m.SideEffect(m.Fn(func, m.Word32(0x1000)));
            }, sExp);
        }

        [Test(Description = "Rewrite constants with segment selector type ")]
        public void TerSelector()
        {
            var sExp =
            #region Expected

@"// Before ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	ds = 0x1234
	Mem0[ds:0x0010:word32] = 0x00010004
test_exit:

// After ///////
// test
// Return size: 0
void test()
test_entry:
	// succ:  l1
l1:
	ds = seg1234
	ds->dw0010 = 0x00010004
test_exit:

"
;
            #endregion

            var seg = new ImageSegment(
                "1234",
                new MemoryArea(Address.SegPtr(0x1234, 0), new byte[0x100]),
                AccessMode.ReadWriteExecute);
            seg.Identifier = Identifier.CreateTemporary("seg1234", PrimitiveType.SegmentSelector);
            imageSegments.Add(seg.Address, seg);
            RunStringTest(m =>
            {
                var ds = m.Frame.CreateTemporary("ds", PrimitiveType.SegmentSelector);
                m.Assign(ds, Constant.Create(ds.DataType, 0x1234));
                m.SegStore(ds, m.Word16(0x10), m.Word32(0x010004));
            }, sExp);
        }

        [Test]
        public void TerNestedStructsPtr()
        {
            var pm = CreateProgramBuilder(0x1000, 0x1000);
            pm.Add("proc1", m =>
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
            });
            var sExp =
            #region Expected String
@"// Before ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	word32 eax = Mem0[ptrArg04:word32]
	word32 ecx = Mem0[eax:word32]
proc1_exit:

// After ///////
// proc1
// Return size: 0
void proc1()
proc1_entry:
	// succ:  l1
l1:
	strInner * eax = ptrArg04->strAttr00
	real32 ecx = eax->innerAttr00
proc1_exit:

";
            #endregion
            RunTest(pm.BuildProgram(), "Typing/TerNestedStructsPtr.txt");
        }
    }
}
