/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

namespace EbicsNet.Responses
{
    public class StaResponse: Response
    {
        public string Data { get; internal set; }
        public byte[] BinaryData { get; internal set; }
    }
}