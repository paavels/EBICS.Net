/*
 * NetEbics -- .NET Core EBICS Client Library
 * (c) Copyright 2018 Bjoern Kuensting
 *
 * This file is subject to the terms and conditions defined in
 * file 'LICENSE.txt', which is part of this source code package.
 */

using System;

namespace EbicsNet.Parameters
{
    public enum StaOrderType {
        STA,

        // ZKB specific https://testplattform.zkb.ch/help/ZKB_Test_Platform_User_Manual.pdf
        Z01,
        Z53,
        Z54,
        ZS2,
        ZS3,
        ZS4,
        ZQR,
        ZRF,
        XTD
    }

    public class StaParams: Params
    {
        public StaOrderType OrderType { get; set; } = StaOrderType.STA;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}