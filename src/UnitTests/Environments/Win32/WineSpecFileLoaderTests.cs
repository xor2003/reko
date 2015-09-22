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

using NUnit.Framework;
using Reko.Arch.X86;
using Reko.Environments.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.UnitTests.Environments.Win32
{
    [TestFixture]
    public class WineSpecFileLoaderTests
    {
        private Win16Platform platform;
        private WineSpecFileLoader wsfl;

        private void CreateWineSpecFileLoader(string filename, string contents)
        {
            this.platform = new Win16Platform(null, new X86ArchitectureProtected16());
            wsfl = new WineSpecFileLoader(null, filename, Encoding.ASCII.GetBytes(contents));
        }

        [Test]
        public void Wsfl_Comment()
        {
            CreateWineSpecFileLoader("foo.spec",
                " # comment");
            var lib = wsfl.Load(platform);
            Assert.AreEqual(0, lib.ServicesByVector.Count);
            Assert.AreEqual(0, lib.ServicesByName.Count);
        }

        [Test]
        public void Wsfl_Line()
        {
            CreateWineSpecFileLoader("foo.spec",
                " 624 pascal SetFastQueue(long long) SetFastQueue16\n");
            var lib = wsfl.Load(platform);
            Assert.AreEqual(1, lib.ServicesByVector.Count);
            Assert.AreEqual(0, lib.ServicesByName.Count);
            var svc = lib.ServicesByVector[624];
            Assert.AreEqual("SetFastQueue", svc.Name);
            Assert.AreEqual(
                "void ()(Stack word32 arg0, Stack word32 arg4)\r\n// stackDelta: 0; fpuStackDelta: 0; fpuMaxParam: -1\r\n",
                svc.Signature.ToString());
        }
    }
}
