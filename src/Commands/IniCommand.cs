﻿/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using EbicsNet.Exceptions;
using EbicsNet.Parameters;
using EbicsNet.Responses;
using EbicsNet.Xml;

namespace EbicsNet.Commands
{
    internal class IniCommand : GenericCommand<IniResponse>
    {
        private static readonly ILogger s_logger = EbicsLogging.CreateLogger<IniCommand>();

        internal IniParams Params { private get; set; }
        internal override string OrderType => "INI";
        internal override string OrderAttribute => "DZNNN";
        internal override TransactionType TransactionType => TransactionType.Upload;
        internal override IList<XmlDocument> Requests => CreateRequests();
        internal override XmlDocument InitRequest => null;
        internal override XmlDocument ReceiptRequest => null;

        private IList<XmlDocument> CreateRequests()
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    var reqs = new List<XmlDocument>();
                    var userSigData = new SignaturePubKeyOrderData
                    {
                        PartnerId = Config.User.PartnerId,
                        UserId = Config.User.UserId,
                        SignKeys = Config.User.SignKeys,
                        Namespaces = Namespaces
                    };

                    s_logger.LogDebug("User signature data:\n{data}", userSigData.Serialize().ToString());

                    var compressed =
                        Compress(
                            Encoding.UTF8.GetBytes(userSigData.Serialize().ToString(SaveOptions.DisableFormatting)));
                    var b64encoded = Convert.ToBase64String(compressed);

                    var req = new EbicsUnsecuredRequest
                    {
                        StaticHeader = new StaticHeader
                        {
                            HostId = Config.User.HostId,
                            PartnerId = Config.User.PartnerId,
                            UserId = Config.User.UserId,
                            SecurityMedium = Params.SecurityMedium,
                            Namespaces = Namespaces,
                            OrderDetails = new OrderDetails
                            {
                                OrderType = OrderType,
                                OrderAttribute = OrderAttribute,
                                Namespaces = Namespaces
                            }
                        },
                        MutableHeader = new MutableHeader
                        {
                            Namespaces = Namespaces
                        },
                        Body = new Body
                        {
                            Namespaces = Namespaces,
                            DataTransfer = new DataTransfer
                            {
                                OrderData = b64encoded,
                                Namespaces = Namespaces
                            }
                        },
                        Version = Config.Version,
                        Revision = Config.Revision,
                        Namespaces = Namespaces
                    };

                    reqs.Add(req.Serialize().ToXmlDocument());
                    return reqs;
                }
                catch (EbicsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CreateRequestException($"can't create requests for {OrderType}", ex);
                }
            }
        }
    }
}