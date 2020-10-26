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
    internal class SignatureData : NamespaceAware, IXElementSerializer
    {
        public XElement Serialize()
        {
            XNamespace nsEbics = Namespaces.Ebics;

            return new XElement(nsEbics + XmlNames.SignatureData,
                new XAttribute(XmlNames.authenticate, true)
            );
        }
    }
}