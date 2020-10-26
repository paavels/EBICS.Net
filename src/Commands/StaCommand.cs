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
    internal class StaCommand : GenericCommand<StaResponse>
    {
        private static readonly ILogger s_logger = EbicsLogging.CreateLogger<StaCommand>();
        private string _transactionId;
        private int _numSegments;
        private int _initSegment;
        private bool _initLastSegment;
        private string[] _orderData;

        internal StaParams Params { private get; set; }
        internal override string OrderType { get; set; }
        internal override string OrderAttribute => "DZHNN";
        internal override TransactionType TransactionType => TransactionType.Download;
        internal override IList<XmlDocument> Requests => CreateRequests();
        internal override XmlDocument InitRequest => CreateInitRequest();
        internal override XmlDocument ReceiptRequest => CreateReceiptRequest();

        internal override DeserializeResponse Deserialize(string payload)
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    var dr = base.Deserialize(payload);
                    var doc = XDocument.Parse(payload);
                    var xph = new XPathHelper(doc, Namespaces);
                    
                    if (dr.HasError || dr.IsRecoverySync)
                    {
                        return dr;
                    }

                    // do signature validation here

                    switch (dr.Phase)
                    {
                        case TransactionPhase.Initialisation:
                            _transactionId = dr.TransactionId;
                            _numSegments = dr.NumSegments;
                            _initSegment = dr.SegmentNumber;
                            _initLastSegment = dr.LastSegment;
                            _orderData = new string[_numSegments];
                            if (_numSegments > 0)
                            {
                                Response.BinaryData = Decompress(DecryptOrderData(xph)); 
                                _orderData[dr.SegmentNumber - 1] = Encoding.UTF8.GetString(Response.BinaryData);
                            }
                            Response.Data = string.Join("", _orderData);
                            break;
                        case TransactionPhase.Transfer:
                            _orderData[dr.SegmentNumber - 1] =
                                Encoding.UTF8.GetString(Decompress(DecryptOrderData(xph)));
                            Response.Data = string.Join("", _orderData);
                            break;
                    }

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

        private IList<XmlDocument> CreateRequests()
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    if (_initLastSegment)
                    {
                        s_logger.LogDebug("lastSegment is {lastSegment}. Not creating any transfer requests",
                            _initLastSegment);
                        return null;
                    }

                    var reqs = new List<XmlDocument>();

                    for (var i = 1; i < _numSegments; i++)
                    {
                        s_logger.LogDebug("Creating transfer request {no}", i);
                        var req = new EbicsRequest
                        {
                            Namespaces = Namespaces,
                            Version = Config.Version,
                            Revision = Config.Revision,
                            StaticHeader = new StaticHeader
                            {
                                HostId = Config.User.HostId,
                                TransactionId = _transactionId,
                            },
                            MutableHeader = new MutableHeader
                            {
                                Namespaces = Namespaces,
                                TransactionPhase = "Transfer",
                                SegmentNumber = i + _initSegment,
                                LastSegment = i + _initSegment == _numSegments
                            },
                            Body = new Body
                            {
                                Namespaces = Namespaces
                            }
                        };

                        reqs.Add(AuthenticateXml(req.Serialize().ToXmlDocument(), null, null));
                    }

                    return reqs;
                }
                catch (EbicsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CreateRequestException($"can't create {OrderType} requests", ex);
                }
            }
        }

        private XmlDocument CreateReceiptRequest()
        {
            using (new MethodLogger(s_logger))
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
                    throw new CreateRequestException($"can't create {OrderType} receipt request", ex);
                }
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
                    throw new CreateRequestException($"can't create {OrderType} init request", ex);
                }
            }
        }
    }
}