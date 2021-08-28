﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.Lib.Data.External.Tables.Camelot
{
    public interface IApiConnection
    {
        string GetEndpointUrl();
        string GetApiKey();
        void DeclareDeadEndpoint(string url);
    }
}