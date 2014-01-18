﻿#region License
/* 
 * Copyright (C) 1999-2014 John Källén.
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
using Decompiler.ImageLoaders.Hunk;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Decompiler.UnitTests.ImageLoaders.Hunk
{
    [TestFixture]
    public class HunkLoaderTests
    {
        [Test]
        public void Hunk_LoadFile()
        {
            var bytes = File.ReadAllBytes(
                FileUnitTester.MapTestPath("../UnitTests/Arch/M68k/images/FIBO"));
            var ldr = new HunkLoader(null, bytes);
            ldr.Load(new Address(0x10000));
        }
    }
}