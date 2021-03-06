﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Cci.Extensions;
using System;
using System.Linq;

namespace Microsoft.Cci.Filters
{
    public class AttributeMarkedFilter : ICciFilter
    {
        private readonly string _attributeName;

        public AttributeMarkedFilter(string attributeName)
        {
            _attributeName = attributeName;
        }

        public virtual bool Include(INamespaceDefinition ns)
        {
            return IsNotMarkedWithAttribute(ns);
        }

        public virtual bool Include(ITypeDefinition type)
        {
            return IsNotMarkedWithAttribute(type);
        }

        public virtual bool Include(ITypeDefinitionMember member)
        {
            return IsNotMarkedWithAttribute(member);
        }

        public virtual bool Include(ICustomAttribute attribute)
        {
            return true;
        }

        private bool IsNotMarkedWithAttribute(IReference definition)
        {
            return !definition.Attributes.Any(a => String.Equals(a.Type.FullName(), _attributeName, StringComparison.Ordinal));
        }
    }
}
