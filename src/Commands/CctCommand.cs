﻿/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    internal class CctCommand : GenericCommand<CctResponse>
    {
        private static readonly ILogger s_logger = EbicsLogging.CreateLogger<CctCommand>();
        private readonly byte[] _transactionKey;
        private XmlDocument _initReq;
        private IList<string> _segments;
        private string _transactionId;

        internal CctParams Params { private get; set; }
        internal override string OrderType => "CCT";
        internal override string OrderAttribute => "OZHNN";
        internal override TransactionType TransactionType => TransactionType.Upload;
        internal override IList<XmlDocument> Requests => CreateUploadRequests(_segments);

        internal override XmlDocument InitRequest
        {
            get
            {
                (_initReq, _segments) = CreateInitRequest();
                return _initReq;
            }
        }

        internal override XmlDocument ReceiptRequest => null;

        public CctCommand()
        {
            _transactionKey = CryptoUtils.GetTransactionKey();
            s_logger.LogDebug("Transaction Key: {key}", CryptoUtils.Print(_transactionKey));
        }

        internal override DeserializeResponse Deserialize(string payload)
        {
            try
            {
                using (new MethodLogger(s_logger))
                {
                    var dr = base.Deserialize(payload);
                    var doc = XDocument.Parse(payload);

                    if (dr.HasError || dr.IsRecoverySync)
                    {
                        return dr;
                    }

                    if (dr.Phase == TransactionPhase.Initialisation)
                    {
                        _transactionId = dr.TransactionId;
                    }

                    return dr;
                }
            }
            catch (EbicsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DeserializationException($"Can't deserialize {OrderType} response", ex, payload);
            }
        }

        private XDocument CreateCctDoc()
        {
            XNamespace ns = Namespaces.Cct;
            var sum = 0m;
            var trxCount = 0;
            foreach (var pi in Params.PaymentInfos)
            {
                trxCount += pi.CreditTransferTransactionInfos.Count();
                foreach (var cti in pi.CreditTransferTransactionInfos)
                {
                    if (!decimal.TryParse(cti.Amount, NumberStyles.Currency, CultureInfo.InvariantCulture,
                        out var amount))
                    {
                        throw new CreateRequestException(
                            $"Invalid amount in CreditTransferInfo of command {OrderType}");
                    }

                    sum += amount;
                }
            }

            var xmlPayInfos = new List<XElement>();
            foreach (var pi in Params.PaymentInfos)
            {
                var piid = CryptoUtils.GetNonce();
                var ctrlSum = 0m;
                var xmlCtis = new List<XElement>();

                foreach (var cti in pi.CreditTransferTransactionInfos)
                {
                    var endToEndID = cti.EndToEndId ?? "NOTPROVIDED";

                    if (!decimal.TryParse(cti.Amount, NumberStyles.Currency, CultureInfo.InvariantCulture,
                        out var amount))
                    {
                        throw new CreateRequestException(
                            $"Invalid amount in CreditTransferInfo of command {OrderType}");
                    }

                    ctrlSum += amount;

                    var xmlCti = new XElement(ns + XmlNames.CdtTrfTxInf,
                        new XElement(ns + XmlNames.PmtId,
                            new XElement(ns + XmlNames.EndToEndId, endToEndID)
                        ),
                        new XElement(ns + XmlNames.Amt,
                            new XElement(ns + XmlNames.InstdAmt,
                                new XAttribute(XmlNames.Ccy, cti.CurrencyCode),
                                amount.ToString("F2", CultureInfo.InvariantCulture)
                            )
                        ),
                        new XElement(ns + XmlNames.CdtrAgt,
                            new XElement(ns + XmlNames.FinInstnId,
                                new XElement(ns + XmlNames.BIC,
                                    cti.CreditorAgent
                                )
                            )
                        ),
                        new XElement(ns + XmlNames.Cdtr,
                            new XElement(ns + XmlNames.Nm,
                                cti.CreditorName
                            )
                        ),
                        new XElement(ns + XmlNames.CdtrAcct,
                            new XElement(ns + XmlNames.Id,
                                new XElement(ns + XmlNames.IBAN,
                                    cti.CreditorAccount
                                )
                            )
                        ),
                        new XElement(ns + XmlNames.RmtInf,
                            new XElement(ns + XmlNames.Ustrd,
                                cti.RemittanceInfo
                            )
                        )
                    );
                    xmlCtis.Add(xmlCti);
                }

                var xmlPayInfo = new XElement(ns + XmlNames.PmtInf,
                    new XElement(ns + XmlNames.PmtInfId, piid),
                    new XElement(ns + XmlNames.PmtMtd, "TRF"),
                    new XElement(ns + XmlNames.BtchBookg, pi.BatchBooking.ToString().ToLower()),
                    new XElement(ns + XmlNames.NbOfTxs, pi.CreditTransferTransactionInfos.Count().ToString()),
                    new XElement(ns + XmlNames.CtrlSum, ctrlSum.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(ns + XmlNames.PmtTpInf,
                        new XElement(ns + XmlNames.SvcLvl,
                            new XElement(ns + XmlNames.Cd, "SEPA")
                        )
                    ),
                    new XElement(ns + XmlNames.ReqdExctnDt, pi.ExecutionDate),
                    new XElement(ns + XmlNames.Dbtr,
                        new XElement(ns + XmlNames.Nm, pi.DebtorName)
                    ),
                    new XElement(ns + XmlNames.DbtrAcct,
                        new XElement(ns + XmlNames.Id,
                            new XElement(ns + XmlNames.IBAN,
                                pi.DebtorAccount
                            )
                        )
                    ),
                    new XElement(ns + XmlNames.DbtrAgt,
                        new XElement(ns + XmlNames.FinInstnId,
                            new XElement(ns + XmlNames.BIC,
                                pi.DebtorAgent
                            )
                        )
                    ),
                    new XElement(ns + XmlNames.ChrgBr, "SLEV")
                );

                xmlCtis.ForEach(x => xmlPayInfo.Add(x));
                xmlPayInfos.Add(xmlPayInfo);
            }

            var doc = new XElement(ns + XmlNames.Document,
                new XElement(ns + XmlNames.CstmrCdtTrfInitn,
                    new XElement(ns + XmlNames.GrpHdr,
                        new XElement(ns + XmlNames.MsgId, CryptoUtils.GetNonce()),
                        new XElement(ns + XmlNames.CreDtTm, CryptoUtils.GetUtcTimeNow()),
                        new XElement(ns + XmlNames.NbOfTxs, trxCount.ToString()),
                        new XElement(ns + XmlNames.CtrlSum, sum.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(ns + XmlNames.InitgPty,
                            new XElement(ns + XmlNames.Nm, Params.InitiatingParty)
                        )
                    )
                )
            );

            xmlPayInfos.ForEach(x => doc.Element(ns + XmlNames.CstmrCdtTrfInitn)?.Add(x));
            return new XDocument(doc);
        }

        private string FormatCctXml(XDocument doc)
        {
            var xmlStr = doc.ToString(SaveOptions.DisableFormatting);
            xmlStr = xmlStr.Replace("\n", "");
            xmlStr = xmlStr.Replace("\r", "");
            xmlStr = xmlStr.Replace("\t", "");
            return xmlStr;
        }

        private XElement CreateUserSigData(XDocument doc)
        {
            var xmlStr = FormatCctXml(doc);

            var signedXmlStr = SignData(Encoding.UTF8.GetBytes(xmlStr), Config.User.SignKeys);

            var userSigData = new UserSignatureData
            {
                Namespaces = Namespaces,
                OrderSignatureData = new OrderSignatureData
                {
                    Namespaces = Namespaces,
                    PartnerId = Config.User.PartnerId,
                    UserId = Config.User.UserId,
                    SignatureValue = signedXmlStr,
                    SignKeys = Config.User.SignKeys
                }
            };

            return userSigData.Serialize();
        }

        private IList<XmlDocument> CreateUploadRequests(IList<string> segments)
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    return segments.Select((segment, i) => new EbicsRequest
                        {
                            Namespaces = Namespaces,
                            Version = Config.Version,
                            Revision = Config.Revision,
                            StaticHeader = new StaticHeader
                            {
                                Namespaces = Namespaces,
                                HostId = Config.User.HostId,
                                TransactionId = _transactionId
                            },
                            MutableHeader = new MutableHeader
                            {
                                Namespaces = Namespaces,
                                TransactionPhase = "Transfer",
                                SegmentNumber = i + 1,
                                LastSegment = (i + 1 == segments.Count)
                            },
                            Body = new Body
                            {
                                Namespaces = Namespaces,
                                DataTransfer = new DataTransfer
                                {
                                    Namespaces = Namespaces,
                                    OrderData = segment
                                }
                            }
                        }
                    ).Select(req => AuthenticateXml(req.Serialize().ToXmlDocument(), null, null)).ToList();
                }
                catch (EbicsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new CreateRequestException($"can't create {OrderType} upload requests", ex);
                }
            }
        }

        private (XmlDocument request, IList<string> segments) CreateInitRequest()
        {
            using (new MethodLogger(s_logger))
            {
                try
                {
                    XNamespace nsEBICS = Namespaces.Ebics;

                    var cctDoc = CreateCctDoc();
                    s_logger.LogDebug("Created {OrderType} document:\n{doc}", OrderType, cctDoc.ToString());

                    var userSigData = CreateUserSigData(cctDoc);
                    s_logger.LogDebug("Created user signature data:\n{data}", userSigData.ToString());

                    var userSigDataXmlStr = userSigData.ToString(SaveOptions.DisableFormatting);
                    var userSigDataComp = Compress(Encoding.UTF8.GetBytes(userSigDataXmlStr));
                    var userSigDataEnc = EncryptAes(userSigDataComp, _transactionKey);

                    var cctDocXmlStr = FormatCctXml(cctDoc);
                    var cctDocComp = Compress(Encoding.UTF8.GetBytes(cctDocXmlStr));
                    var cctDocEnc = EncryptAes(cctDocComp, _transactionKey);
                    var cctDocB64 = Convert.ToBase64String(cctDocEnc);

                    var segments = Segment(cctDocB64);

                    s_logger.LogDebug("Number of segments: {segments}", segments.Count);

                    var initReq = new EbicsRequest
                    {
                        Namespaces = Namespaces,
                        Version = Config.Version,
                        Revision = Config.Revision,
                        StaticHeader = new StaticHeader
                        {
                            Namespaces = Namespaces,
                            HostId = Config.User.HostId,
                            Nonce = CryptoUtils.GetNonce(),
                            Timestamp = CryptoUtils.GetUtcTimeNow(),
                            PartnerId = Config.User.PartnerId,
                            UserId = Config.User.UserId,
                            SecurityMedium = Params.SecurityMedium,
                            NumSegments = segments.Count,
                            OrderDetails = new OrderDetails
                            {
                                Namespaces = Namespaces,
                                OrderType = OrderType,
                                OrderAttribute = OrderAttribute,
                                StandardOrderParams = new EmptyOrderParams
                                {
                                    Namespaces = Namespaces
                                },
                            },
                            BankPubKeyDigests = new BankPubKeyDigests
                            {
                                Namespaces = Namespaces,
                                DigestAlgorithm = s_digestAlg,
                                Bank = Config.Bank
                            }
                        },
                        MutableHeader = new MutableHeader
                        {
                            Namespaces = Namespaces,
                            TransactionPhase = "Initialisation"
                        },
                        Body = new Body
                        {
                            Namespaces = Namespaces,
                            DataTransfer = new DataTransfer
                            {
                                Namespaces = Namespaces,
                                DataEncryptionInfo = new DataEncryptionInfo
                                {
                                    Namespaces = Namespaces,
                                    EncryptionPubKeyDigest = new EncryptionPubKeyDigest
                                    {
                                        Namespaces = Namespaces,
                                        Bank = Config.Bank,
                                        DigestAlgorithm = s_digestAlg
                                    },
                                    TransactionKey = Convert.ToBase64String(EncryptRsa(_transactionKey))
                                },
                                SignatureData = new SignatureData
                                {
                                    Namespaces = Namespaces
                                }
                            }
                        }
                    };

                    var doc = initReq.Serialize();
                    doc.Descendants(nsEBICS + XmlNames.SignatureData).FirstOrDefault()
                        ?.Add(Convert.ToBase64String(userSigDataEnc));
                    return (request: AuthenticateXml(doc.ToXmlDocument(), null, null), segments: segments);
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