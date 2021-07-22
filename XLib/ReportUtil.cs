﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Devmasters.Enums;
using HlidacStatu.Entities;
using HlidacStatu.Entities.Entities.Analysis;
using HlidacStatu.Lib.Analytics;
using HlidacStatu.Repositories;
using HlidacStatu.Repositories.Searching;
using HlidacStatu.Util;
using HlidacStatu.XLib.Render;
using Consts = HlidacStatu.Util.Consts;

namespace HlidacStatu.XLib
{
    public static class ReportUtil
    {

        public static List<ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column> ComplexStatisticDefaultReportColumns<T>(RenderData.MaxScale scale,
            int? minDateYear = null, int? maxDateYear = null, string query = null)
        {
            minDateYear = minDateYear ?? DataPerYear.UsualFirstYear;
            maxDateYear = maxDateYear ?? DateTime.Now.Year;

            var coreColumns = new List<ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column>();

            coreColumns.Add(
                new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                {
                    Id = "Title",
                    Name = "Plátci",
                    HtmlRender = (s) =>
                    {
                        var f = Firmy.Get(s.Key);
                        string html = string.Format("<a href='{0}'>{1}</a>", f.GetUrl(false), f.Jmeno);
                        if (!string.IsNullOrEmpty(query))
                        {
                            html += $" /<span class='small'>ukázat&nbsp;<a href='/hledat?q={WebUtility.UrlEncode(Query.ModifyQueryAND("ico:" + f.ICO, query))}'>smlouvy</a></span>/";
                        }
                        return html;
                    },
                    OrderValueRender = (s) => Firmy.GetJmeno(s.Key),
                    ValueRender = (s) => ("\"" + Firmy.GetJmeno(s.Key) + "\""),
                    TextRender = (s) => Firmy.GetJmeno(s.Key)
                });

            for (int y = minDateYear.Value; y <= maxDateYear.Value; y++)
            {
                int year = y;
                coreColumns.Add(
                    new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                    {
                        Id = "Cena_Y_" + year,
                        Name = $"Smlouvy {year} v {scale.ToNiceDisplayName()} Kč",
                        HtmlRender = (s) => RenderData.ShortNicePrice(s.Value[year].CelkovaHodnotaSmluv, mena: "", html: true, showDecimal: RenderData.ShowDecimalVal.Show, exactScale: scale, hideSuffix: true),
                        OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value[year].CelkovaHodnotaSmluv),
                        TextRender = (s) => RenderData.ShortNicePrice(s.Value[year].CelkovaHodnotaSmluv, mena: "", html: false, showDecimal: RenderData.ShowDecimalVal.Show, exactScale: scale, hideSuffix: true),
                        ValueRender = (s) => s.Value[year].CelkovaHodnotaSmluv.ToString("F0",HlidacStatu.Util.Consts.enCulture),
                        CssClass = "number"
                    }
                    );
                coreColumns.Add(
                    new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                    {
                        Id = "Pocet_Y_" + year,
                        Name = $"Počet smluv v {year} ",
                        HtmlRender = (s) => RenderData.NiceNumber(s.Value[year].PocetSmluv, html: true),
                        OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value[year].PocetSmluv),
                        TextRender = (s) => RenderData.NiceNumber(s.Value[year].PocetSmluv, html: false),
                        ValueRender = (s) => s.Value[year].PocetSmluv.ToString("F0", HlidacStatu.Util.Consts.enCulture),
                        CssClass = "number"
                    }
                    );
                coreColumns.Add(
                        new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                        {
                            Id = "PercentBezCeny_Y_" + year,
                            Name = $"Smluv bez ceny za {year} v %",
                            HtmlRender = (s) => s.Value[year].PercentSmluvBezCeny.ToString("P2"),
                            OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value[year].PercentSmluvBezCeny),
                            ValueRender = (s) => (s.Value[year].PercentSmluvBezCeny * 100).ToString(Consts.enCulture),
                            TextRender = (s) => s.Value[year].PercentSmluvBezCeny.ToString("P2"),
                            CssClass = "number"
                        });
                coreColumns.Add(
                        new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                        {
                            Id = "PercentSPolitiky_Y_" + year,
                            Name = $"% smluv s politiky v {year} ",
                            HtmlRender = (s) => s.Value[year].PercentSmluvPolitiky.ToString("P2"),
                            OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value[year].PercentSmluvPolitiky),
                            ValueRender = (s) => (s.Value[year].PercentSmluvPolitiky * 100).ToString(Consts.enCulture),
                            TextRender = (s) => s.Value[year].PercentSmluvPolitiky.ToString("P2"),
                            CssClass = "number"
                        });
                coreColumns.Add(
                        new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                        {
                            Id = "SumKcSPolitiky_Y_" + year,
                            Name = $"Hodnota smluv s politiky za {year}",
                            HtmlRender = (s) => HlidacStatu.Util.RenderData.NicePrice(s.Value[year].SumKcSmluvSponzorujiciFirmy),
                            OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value[year].SumKcSmluvSponzorujiciFirmy),
                            ValueRender = (s) => (s.Value[year].SumKcSmluvSponzorujiciFirmy).ToString("F0",Consts.enCulture),
                            TextRender = (s) => s.Value[year].SumKcSmluvSponzorujiciFirmy.ToString("F0", HlidacStatu.Util.Consts.enCulture),
                            CssClass = "number"
                        });

                if (year > minDateYear)
                {
                    coreColumns.Add(
                            new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                            {
                                Id = "CenaChangePercent_Y_" + year,
                                Name = $"Změna hodnoty smlouvy {year - 1}-{year}",
                                HtmlRender = (s) => s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage.HasValue?
                                    RenderData.ChangeValueSymbol(s.Value.ChangeBetweenYears(year,m=>m.CelkovaHodnotaSmluv).percentage.Value, true) :
                                    "",
                                OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage ?? 0),
                                ValueRender = (s) => s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage.HasValue ?
                                    (s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage.Value * 100).ToString(Consts.enCulture) :
                                    "",
                                TextRender = (s) => s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage.HasValue ?
                                    (s.Value.ChangeBetweenYears(year, m => m.CelkovaHodnotaSmluv).percentage.Value).ToString("P2") :
                                    "",
                                CssClass = "number"
                            }
                        );
                }
            };
            coreColumns.Add(
                new ReportDataSource<KeyValuePair<string, StatisticsPerYear<Smlouva.Statistics.Data>>>.Column()
                {
                    Id = "CenaCelkem",
                    Name = $"Smlouvy 2016-{DateTime.Now.Year} v {scale.ToNiceDisplayName()} Kč",
                    HtmlRender = (s) => RenderData.ShortNicePrice(s.Value.Sum(m=>m.CelkovaHodnotaSmluv), mena: "", html: true, showDecimal: RenderData.ShowDecimalVal.Show, exactScale: scale, hideSuffix: true),
                    ValueRender = (s) => (s.Value.Sum(m=>m.CelkovaHodnotaSmluv)).ToString("F0", Consts.enCulture),
                    OrderValueRender = (s) => RenderData.OrderValueFormat(s.Value.Sum(m => m.CelkovaHodnotaSmluv)),
                    CssClass = "number"
                });

            return coreColumns;
        }

        public static ReportDataSource<SponzoringRepo.Strany.StranaPerYear> RenderPerYearsTable(IEnumerable<SponzoringRepo.Strany.StranaPerYear> dataPerYear)
        {
            ReportDataSource<SponzoringRepo.Strany.StranaPerYear> rokyTable = new ReportDataSource<SponzoringRepo.Strany.StranaPerYear>(
                new[]
                {
                    new ReportDataSource<SponzoringRepo.Strany.StranaPerYear>.Column()
                    {
                        Name = "Rok",
                        HtmlRender = (s) => { return s.Rok.ToString(); }
                    },
                    new ReportDataSource<SponzoringRepo.Strany.StranaPerYear>.Column()
                    {
                        Name = "Sponzoring osob",
                        HtmlRender = (s) =>
                        {
                            SponzoringRepo.Strany.StranaPerYear data = (SponzoringRepo.Strany.StranaPerYear) s;
                            if (data.Osoby.Num > 0)
                                return string.Format(@"{0}, počet darů: {1} za {2}",
                                    Sponsors.GetStranaSponzoringHtmlLink(data.Strana, data.Rok, Sponsors.SponzoringDataType.Osoby),
                                    data.Osoby.Num, Util.RenderData.NicePrice(data.Osoby.Sum, "výši neznáme"));
                            else
                                return "";
                        }
                    },
                    new ReportDataSource<SponzoringRepo.Strany.StranaPerYear>.Column()
                    {
                        Name = "Sponzoring firem",
                        HtmlRender = (s) =>
                        {
                            SponzoringRepo.Strany.StranaPerYear data = (SponzoringRepo.Strany.StranaPerYear) s;
                            if (data.Firmy.Num > 0)
                                return string.Format(@"{0}, počet darů: {1} za {2}",
                                    Sponsors.GetStranaSponzoringHtmlLink(data.Strana, data.Rok, Sponsors.SponzoringDataType.Firmy),
                                    data.Firmy.Num, Util.RenderData.NicePrice(data.Firmy.Sum, "výši neznáme"));
                            else
                                return "";
                        }
                    },
                });


            foreach (var r in dataPerYear.OrderBy(m => m.Rok))
            {
                rokyTable.AddRow(r);
            }

            return rokyTable;
        }

        public static ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>> RenderSponzorství(
            IEnumerable<Sponsors.Sponzorstvi<IBookmarkable>> data, bool showYear = true, bool linkStrana = true)
        {
            var yearCol = new ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>>.Column()
            {
                Name = "Rok",
                HtmlRender = (s) => { return s.Rok?.ToString(); },
                OrderValueRender = (s) => { return Util.RenderData.OrderValueFormat(s.Rok ?? 0); },
                CssClass = "number"
            };
            ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>> rokyTable = new ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>>(
                new[]
                {
                    new ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>>.Column()
                    {
                        Name = "Sponzor",
                        HtmlRender = (s) =>
                        {
                            return $"<a href='{s.Sponzor.GetUrl(true)}'>{s.Sponzor.BookmarkName()}</a>";
                        },
                        OrderValueRender =
                            (s) => { return Util.RenderData.OrderValueFormat(s.Sponzor.BookmarkName()); },
                    },
                    new ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>>.Column()
                    {
                        Name = "Částka",
                        HtmlRender = (s) => { return Util.RenderData.NicePrice(s.CastkaCelkem, html: true); },
                        OrderValueRender = (s) => { return Util.RenderData.OrderValueFormat(s.CastkaCelkem); },
                        CssClass = "number"
                    },
                    new ReportDataSource<Sponsors.Sponzorstvi<IBookmarkable>>.Column()
                    {
                        Name = "Strana",
                        HtmlRender = (s) =>
                        {
                            if (linkStrana)
                                return Sponsors.GetStranaHtmlLink(s.Strana);
                            else
                                return s.Strana;
                        },
                        OrderValueRender = (s) => { return Util.RenderData.OrderValueFormat(s.Strana); },
                    },
                });
            if (showYear)
                rokyTable.Columns.Add(yearCol);


            foreach (var r in data.OrderBy(m => m.Rok))
            {
                rokyTable.AddRow(r);
            }

            return rokyTable;
        }
    }
}