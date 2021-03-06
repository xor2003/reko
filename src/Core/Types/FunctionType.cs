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

using Reko.Core.Expressions;
using Reko.Core.Serialization;
using System;
using System.IO;
using System.Linq;

namespace Reko.Core.Types
{
    /// <summary>
    /// Models a function type. Note the similarity to ProcedureSignature: it's likely we'll want to merge these two.
    /// </summary>
	public class FunctionType : DataType
	{
        //$REVIEW: unify ProcedureSignature and FunctionType.
        public SerializedSignature Signature { get; private set; }

        public FunctionType(
            string name,
            Identifier returnValue,
            Identifier [] parameters,
            SerializedSignature sSig = null)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            if (returnValue == null)
                returnValue = new Identifier("", VoidType.Instance, null);
            this.ReturnValue = returnValue;
            this.Parameters = parameters;
            this.Signature = sSig;
        }

        public FunctionType(ProcedureSignature sig) : base()
        {
            this.ReturnValue = sig.ReturnValue;
            this.Parameters = sig.Parameters;
        }

        public Identifier ReturnValue { get; private set; }
        public Identifier [] Parameters { get; private set; }

        public override void Accept(IDataTypeVisitor v)
        {
            v.VisitFunctionType(this);
        }

        public override T Accept<T>(IDataTypeVisitor<T> v)
        {
            return v.VisitFunctionType(this);
        }

		public override DataType Clone()
		{
            Identifier ret = new Identifier("", ReturnValue.DataType.Clone(), ReturnValue.Storage);
            Identifier[] parameters = this.Parameters
                .Select(p => new Identifier(p.Name, p.DataType.Clone(), p.Storage))
                .ToArray();
            var ft = new FunctionType(Name, ret, parameters, Signature);
            return ft;
		}

		public override int Size
		{
			get { return 0; }
			set { ThrowBadSize(); }
		}
	}
}
