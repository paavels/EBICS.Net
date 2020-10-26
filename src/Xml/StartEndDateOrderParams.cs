﻿/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

using System;
using System.Xml.Linq;

namespace EbicsNet.Xml
{
    internal class StartEndDateOrderParams : NamespaceAware, IXElementSerializer
    {
        internal DateTime StartDate { private get; set; }
        internal DateTime EndDate { private get; set; }

        public XElement Serialize()
        {
            XNamespace nsEbics = Namespaces.Ebics;
            return new XElement(nsEbics + XmlNames.StandardOrderParams,
                new XElement(nsEbics + XmlNames.DateRange,
                    new XElement(nsEbics + XmlNames.Start, StartDate.ToString("yyyy-MM-dd")),
                    new XElement(nsEbics + XmlNames.End, EndDate.ToString("yyyy-MM-dd"))
                )
            );
        }
    }
}