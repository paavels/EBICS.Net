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
using EbicsNet.Handler;
using EbicsNet.Parameters;
using EbicsNet.Responses;
using EbicsNet.Xml;

namespace EbicsNet.Commands
{
    internal class PtkCommand : GenericCommand<PtkResponse>
    {
        private static readonly ILogger s_logger = EbicsLogging.CreateLogger<PtkCommand>();
        private string _transactionId;

        internal PtkParams Params { private get; set; }
        internal override TransactionType TransactionType => TransactionType.Download;
        internal override IList<XmlDocument> Requests => null;
        internal override XmlDocument InitRequest => CreateInitRequest();
        internal override XmlDocument ReceiptRequest => CreateReceiptRequest();
        internal override string OrderType => "PTK";
        internal override string OrderAttribute => "DZHNN";

        internal override DeserializeResponse Deserialize(string payload)
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    var dr = base.Deserialize(payload);
                    var doc = XDocument.Parse(payload);
                    var xph = new XPathHelper(doc, Namespaces);
                    var sb = new StringBuilder();
                    
                    if (dr.HasError || dr.IsRecoverySync)
                    {
                        return dr;
                    }

                    // do signature validation here

                    if (dr.Phase != TransactionPhase.Initialisation)
                    {
                        return dr;
                    }

                    sb.Append(Response.Data ?? "").Append(Encoding.UTF8.GetString(Decompress(DecryptOrderData(xph))));
                    Response.Data = sb.ToString();
                    _transactionId = dr.TransactionId;

                    return dr;
                }
                catch (EbicsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new DeserializationException($"can't deserialize {OrderType} command", ex, payload);
                }
            }
        }

        private XmlDocument CreateReceiptRequest()
        {
            try
            {
                var receiptReq = new EbicsRequest
                {
                    Version = Config.Version,
                    Revision = Config.Revision,
                    Namespaces = Namespaces,
                    StaticHeader = new StaticHeader
                    {
                        Namespaces = Namespaces,
                        HostId = Config.User.HostId,
                        TransactionId = _transactionId
                    },
                    MutableHeader = new MutableHeader
                    {
                        Namespaces = Namespaces,
                        TransactionPhase = "Receipt"
                    },
                    Body = new Body
                    {
                        Namespaces = Namespaces,
                        TransferReceipt = new TransferReceipt
                        {
                            Namespaces = Namespaces,
                            ReceiptCode = "0"
                        }
                    }
                };

                return AuthenticateXml(receiptReq.Serialize().ToXmlDocument(), null, null);
            }
            catch (EbicsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CreateRequestException($"can't create receipt request for {OrderType}", ex);
            }
        }

        private XmlDocument CreateInitRequest()
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    var initReq = new EbicsRequest
                    {
                        StaticHeader = new StaticHeader
                        {
                            Namespaces = Namespaces,
                            HostId = Config.User.HostId,
                            PartnerId = Config.User.PartnerId,
                            UserId = Config.User.UserId,
                            SecurityMedium = Params.SecurityMedium,
                            Nonce = CryptoUtils.GetNonce(),
                            Timestamp = CryptoUtils.GetUtcTimeNow(),
                            BankPubKeyDigests = new BankPubKeyDigests
                            {
                                Namespaces = Namespaces,
                                Bank = Config.Bank,
                                DigestAlgorithm = s_digestAlg
                            },
                            OrderDetails = new OrderDetails
                            {
                                Namespaces = Namespaces,
                                OrderAttribute = OrderAttribute,
                                OrderType = OrderType,
                                StandardOrderParams = new StartEndDateOrderParams
                                {
                                    Namespaces = Namespaces,
                                    StartDate = Params.StartDate,
                                    EndDate = Params.EndDate
                                }
                            }
                        },
                        MutableHeader = new MutableHeader
                        {
                            Namespaces = Namespaces,
                            TransactionPhase = "Initialisation"
                        },
                        Body = new Body
                        {
                            Namespaces = Namespaces
                        },
                        Namespaces = Namespaces,
                        Version = Config.Version,
                        Revision = Config.Revision,
                    };

                    return AuthenticateXml(initReq.Serialize().ToXmlDocument(), null, null);
                }
                catch (EbicsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CreateRequestException($"can't create init request for {OrderType}", ex);
                }
            }
        }
    }
}