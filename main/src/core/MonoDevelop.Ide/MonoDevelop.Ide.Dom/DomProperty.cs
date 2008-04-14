//
// DomProperty.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using MonoDevelop.Core.Gui;

namespace MonoDevelop.Ide.Dom
{
	public class DomProperty : AbstractMember, IProperty
	{
		protected bool isIndexer;
		
		public bool IsIndexer {
			get {
				return isIndexer;
			}
		}
		
		public virtual bool HasSet {
			get {
				return GetMethod != null;
			}
		}
		
		public virtual bool HasGet {
			get {
				return SetMethod != null;
			}
		}
		
		public virtual IMethod GetMethod {
			get {
				return LookupSpecialMethod ("get_");
			}
		}
		
		public virtual IMethod SetMethod {
			get {
				return LookupSpecialMethod ("set_");
			}
		}
		
		static readonly string[] iconTable = {Stock.Property, Stock.PrivateProperty, Stock.ProtectedProperty, Stock.InternalProperty};
		
		public override string StockIcon {
			get {
				return iconTable [ModifierToOffset (Modifiers)];
			}
		}
		
		protected DomProperty ()
		{
		}
		
		public DomProperty (string name)
		{
			base.name = name;
		}

		public override string ToString ()
		{
			return string.Format ("[DomProperty:Name={0}, Modifiers={1}, ReturnType={2}, Region={3}]",
			                      Name,
			                      Modifiers,
			                      ReturnType,
			                      Region);
		}
		
		
		public override object AcceptVisitior (IDomVisitor visitor, object data)
		{
			return visitor.Visit (this, data);
		}
	}
}
