﻿/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

using System.Xml.Linq;

namespace EbicsNet.Xml
{
    internal class EmptyOrderParams : NamespaceAware, IXElementSerializer
    {
        public XElement Serialize()
        {
            XNamespace ns = Namespaces.Ebics;
            return new XElement(ns + XmlNames.StandardOrderParams);
        }
    }
}