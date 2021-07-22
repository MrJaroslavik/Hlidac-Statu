﻿using HlidacStatu.Lib.Analytics;
using System.Collections.Generic;
using HlidacStatu.Datastructures.Graphs;

namespace HlidacStatu.Entities
{
    public partial class Osoba
    {
        public partial class Statistics
        {
            public class RegistrSmluv
            {

                public string OsobaNameId { get; set; }
                public Relation.AktualnostType Aktualnost { get; set; }
                public Smlouva.SClassification.ClassificationsTypes? Obor { get; set; } = null;

                public Dictionary<string, StatisticsSubjectPerYear<Smlouva.Statistics.Data>> StatniFirmy { get; set; }
                public Dictionary<string, StatisticsSubjectPerYear<Smlouva.Statistics.Data>> SoukromeFirmy { get; set; }

                //migrace: prasárna
                public string[] _neziskovkyIcos = null;
                public StatisticsPerYear<Smlouva.Statistics.Data> _soukromeFirmySummary = null;
                public StatisticsPerYear<Smlouva.Statistics.Data> _statniFirmySummary = null;
                public StatisticsPerYear<Smlouva.Statistics.Data> _neziskovkySummary = null;
                

            }
            

        }
    }
}