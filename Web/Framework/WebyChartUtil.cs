﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Text;
using HlidacStatu.Lib.Data.External.Zabbix;

namespace HlidacStatu.Web.Framework
{
    public static class WebyChartUtil
    {

        public static IHtmlContent OnPageSharedJavascript()
        {
            string s = $@"
    <script>

        function getChartStatus(response, style)
        {{
            var s = """";
            if (response == {ZabAvailability.BadHttpCode.ToString(HlidacStatu.Util.Consts.enCulture)})
                if (style == 0) return ""odpověděla s chybou""; else return ""odpovídá<br/>s chybou"";
            if (response == {ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture)})
                if (style == 0) return ""vůbec neodpověděla""; else return ""vůbec<br/>neodpovídá"";
            if (response <= {ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture)})
                if (style == 0) return ""byla rychlá""; else return ""reaguje<br/>rychle"";
            if (response <= {ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture)})
                if (style == 0) return ""byla pomalá""; else return ""reaguje<br/>pomalu"";
             if (response > {ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture)})
                if (style == 0) return ""byla nedostupná""; else return ""reaguje<br/>velmi pomalu"";
          return s;
    }}
    function getChartStatusColor(response, style)
    {{
        var s = """";
        if (response <= {ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture)})
            return ""{ZabAvailability.GetStatusChartColor(Statuses.OK)}"";
        if (response <= {ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture)})
            return ""{ZabAvailability.GetStatusChartColor(Statuses.Pomalé) }"";
        if (response > {ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture) })
            return ""{ZabAvailability.GetStatusChartColor(Statuses.Nedostupné)} "";
        if (response == {ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture) })
            return ""{ZabAvailability.GetStatusChartColor(Statuses.TimeOuted)} "";
        if (response == {ZabAvailability.BadHttpCode.ToString(HlidacStatu.Util.Consts.enCulture)})
            return ""{ZabAvailability.GetStatusChartColor(Statuses.BadHttpCode)} "";
        return s;
    }}
    </script>
";
            return new HtmlString(s);
        }


        public static IHtmlContent TableNextGroups(string active)
        {
            var s = $@"
    <h2>Další služby</h2>

    <div class='row'>
        <div class='col-xs-12 col-sm-6 '>
            <div class='list-group'>

                <a href='/StatniWeby/Https' class='list-group-item {WebUtil.IfExists(active=="https","disabled")}'>
                    <span class='badge'>{ZabTools.SslStatuses()?.Count() ?? 0}</span>
                    Žebříček státních serverů podle HTTPS Labs hodnocení
                </a>


                <a href='/StatniWeby/Index' class='list-group-item {WebUtil.IfExists(active=="index","disabled")}'>
                    <span class='badge'>{ZabTools.WebyItems("0")?.Count() ?? 0}</span>
                    Nejdůležitější služby státní správy
                </a>

                <a href='/StatniWeby/Dalsi/ustredni' class='list-group-item {WebUtil.IfExists(active == "ustredni", "disabled")}'>
                    <span class='badge'>{ZabTools.WebyItems("ustredni")?.Count() ?? 0}</span>
                    Služby ústředních orgánů státní správy
                </a>
                <a href='/StatniWeby/Dalsi/3' class='list-group-item {WebUtil.IfExists(active == "3", "disabled")}'>
                    <span class='badge'>{ZabTools.WebyItems("3")?.Count() ?? 0}</span>
                    Open source/open data weby
                </a>
                <a href='/StatniWeby/Dalsi/1' class='list-group-item {WebUtil.IfExists(active == "1", "disabled")}'>
                    <span class='badge'>{ZabTools.WebyItems("1")?.Count() ?? 0}</span>
                    Další důležité služby
                </a>
                <a href='/StatniWeby/Dalsi/2' class='list-group-item {WebUtil.IfExists(active == "2", "disabled")}'>
                    <span class='badge'>{ZabTools.WebyItems("2")?.Count() ?? 0}</span>
                    Ostatní měřené služby
                </a>"
                //<a href='#' class='list-group-item disabled'>
                //        <span class='badge'>Připravujeme</span>
                //        Služby krajů ČR
                //    </a>
                + @$"<a href='/StatniWeby/opendata' class='list-group-item {WebUtil.IfExists(active=="opendata","disabled")}'>
                    <span class='badge'>JSON</span>
                    Naměřené údaje jako open data
                </a>
            </div>
        </div>
    </div>
";
            return new HtmlString(s);
        }

        public static IHtmlContent Chart(string dataName, int hoursBack, int height, bool detail, string hash = "")
        {
            var uniqueId = "_chart_" + Devmasters.TextUtil.GenRandomString(8);
            var colsize = 0; //data.Select(d => d.ColSize(fromDate, toDate)).Max();
            var colors = new string[] { ZabAvailability.GetStatusChartColor(Statuses.OK),
        ZabAvailability.GetStatusChartColor(Statuses.Pomalé),
        ZabAvailability.GetStatusChartColor(Statuses.Nedostupné),
        ZabAvailability.GetStatusChartColor(Statuses.TimeOuted),
        ZabAvailability.GetStatusChartColor(Statuses.BadHttpCode),
        ZabAvailability.GetStatusChartColor(Statuses.Unknown)
        };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($@"
<div id='adbmsg{uniqueId}' style='display: none; ' class='row'>
      <div class='col-xs-12 text-center center-block'>
        <div class='alert alert-danger'>
            <p>
                <strong>Nevidíte žádný graf?</strong> Některé verze AdBlock a jiných blokovačů reklam přeruší vykreslení grafu.
            </p><p>Vypněte AdBlock pro naše servery a graf poběží. A nebojte, reklamy vám tu nebudeme ukazovat.</p>
        </div>
    </div>
</div>
");
            sb.AppendLine($"<div id='container{uniqueId}' style='height:{height}px; min-width: 310px; max-width: 1000px; margin: 0 auto'></div>");
            sb.AppendLine("<script>");

            sb.AppendLine($"var chart{uniqueId};\n"
        + $"var cats{uniqueId} =null;\n"
           + @"$(document).ready(function () { //start chart

            function showwarn" + uniqueId + @"() {
                    var dsize = $('#container" + uniqueId + @"').html().length;
                    if (dsize < 1000) {
                        $('#adbmsg" + uniqueId + @"').show();
                    }
            else {
                        $('#adbmsg" + uniqueId + @"').hide();
    }
};

Highcharts.setOptions({
    lang:
    {
        shortWeekdays:['Ne', 'Po', 'Út', 'St', 'Čt', 'Pá', 'So']
    }
});
");

            sb.AppendLine(@"
            var chartopt" + uniqueId + @" = {
                chart: {
                    renderTo:'container" + uniqueId + @"',
                    type: 'heatmap',
                    events: {
                        load: function () {
                            $('#adbmsg" + uniqueId + @"').hide();
                            setTimeout(function () {
                                showwarn" + uniqueId + @";
                            }, 1000);
                        }
                    }
                },
                title: null, //{text: 'Dostupnost služeb za poslední 2 dny'},
                plotOptions: {
                },

                xAxis: {
                    type: 'datetime',
                    labels: {
                        align: 'left',
                        x: 5,
                        y: 14,
                        format: '{value:%a %H:%M}' // long month
                    },
                    showLastLabel: false,
                    //tickLength: 16
                },
                legend: {
                    //symbolWidth: 380,
                    verticalAlign: 'top',
                    align: 'right',
                    y: 25,
                },
");

            sb.AppendLine(@"
                colorAxis: {
                    dataClasses: [{
                        to: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        name: 'Služba ' + getChartStatus(" + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @", 0),
                        color: '" + colors[0] + @"'
                    }, {
                        from: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        to: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        color: '" + colors[1] + @"',
                        name: 'Služba ' + getChartStatus(" + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @", 0)
                    }, {
                        from: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        to: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        color: '" + colors[2] + @"',
                        name: 'Služba ' + getChartStatus(" + (HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.SlowLimit + 1).ToString(HlidacStatu.Util.Consts.enCulture) + @", 0)
                    },
                       {
                        from: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        to: " + (HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.BadHttpCode - 0.001m).ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        color: '" + colors[3] + @"',
                        name: 'Odezva nejde změřit'
                        },
                       {
                        from: " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.BadHttpCode.ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        to: " + (HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.BadHttpCode + 0.001m).ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        color: '" + colors[4] + @"',
                        name: 'Chyba serveru'
                        },
                       {
                        from: " + (HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.BadHttpCode + 0.001m).ToString(HlidacStatu.Util.Consts.enCulture) + @",
                        color: '" + colors[5] + @"',
                        name: 'Odezva nezměřena'
                        }
                    ],");
            sb.AppendLine(@"
                    min: 0,
                    max: 3.2,
                    startOnTick: false,
                    endOnTick: false,
                    step: 4,
                },");

            sb.AppendLine(@"
                yAxis: [{
                    categories: [{} ],
                    title: {
                        text: null
                    },
                    labels: {
                        useHTML: true,
                        formatter: function () {
                            var obj = cats" + uniqueId + @"[this.value];
                            var status = '<span style=""font-size:15px;color:" + colors[0] + @""" class=""glyphicon glyphicon-ok-sign""></span> ';
                            if (obj.lastResponse.Response > " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.OKLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @")
                                status = '<span style=""font-size:15px;color:" + colors[1] + @""" class=""glyphicon glyphicon-ok-sign""></span> ';
                            if (obj.lastResponse.Response > " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.SlowLimit.ToString(HlidacStatu.Util.Consts.enCulture) + @")
                                status = '<span style=""font-size:15px;color:" + colors[2] + @""" class=""glyphicon glyphicon-ok-sign""></span> ';
                            if (obj.lastResponse.Response == " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture) + @")
                                status = '<span style=""font-size:15px;color:" + colors[3] + @""" class=""glyphicon glyphicon-ok-sign""></span> ';
                            if (obj.lastResponse.Response == " + HlidacStatu.Lib.Data.External.Zabbix.ZabAvailability.BadHttpCode.ToString(HlidacStatu.Util.Consts.enCulture) + @")
                                status = '<span style=""font-size:15px;color:" + colors[4] + @""" class=""glyphicon glyphicon-ok-sign""></span> ';
                            var status2 = '<span style=""color:' + getChartStatusColor(obj.lastResponse.Response) + '"">právě ' + getChartStatus(obj.lastResponse.Response, 1) + '</span> ';
                            ");
            sb.AppendLine(detail ?
                                    @"url = '<b>' + obj.host.publicname + '</b>'"
                                    : @"var url = '<a href=""/statniweby/info/' + obj.host.hostid + '?h=' + obj.host.hash + '"">' + obj.host.publicname + '</a>';"
                            );
            sb.AppendLine(@"
                            var s = '<div style=""text-align:right;margin-top:9px;border-bottom:1px solid #ddd"">'
                                + '<table cellpadding=0 cellspacing=0><tr>'
                                + '<td style=""padding:0"" align=""right"">' + url + '</td>'
                                + '<td rowspan=""2""><span style=""padding:0px 5px 0px 10px;"">' + status + '</span></td>'
                                + '<td rowspan=""2""><span style=""margin-right:10px;"">' + status2 + '</span></td></tr>'
                                + '<tr><td style=""padding:0"" align=""right"">' + (obj.host.urad || '') + '</td></tr></table>'
                                + '</div>'

                            return s;
                        }

                    },");
            sb.AppendLine(@"
                    minPadding: 0,
                    maxPadding: 0,
                    startOnTick: false,
                    endOnTick: false,
                    tickWidth: 1,
                }");
            sb.AppendLine("], //yAxis");

            //colsize:hoursBack * 60 * 1000,
            sb.AppendLine(@"
                series: [{
                    boostThreshold: 100,
                    borderWidth: 1,
                    nullColor: '#ccc',
                    color: '#ff0000',
                    connectNulls: true,
                    colsize: " + colsize + @", 
                    rowsize: 1,
                    tooltip: {
                        headerFormat: 'Rychlost odezvy<br/>',
                        //pointFormat: '{point.x:%a %H:%M:%S}  <b>{point.value:.2f}s</b>',
                        pointFormatter: function () {
                            var timeout = " + ZabAvailability.TimeOuted.ToString(HlidacStatu.Util.Consts.enCulture) + @";
                            var badCode = " + ZabAvailability.BadHttpCode.ToString(HlidacStatu.Util.Consts.enCulture) + @";
                            var val = 'odezva ' + Highcharts.numberFormat(this.value, 2) + 's';
                            var s = '';
                            if (this.value == timeout) {
                                s = Highcharts.dateFormat('%a %H:%M:%S', this.x) + ' '
                                    + '<b>Služba ' + getChartStatus(this.value, 0) + '</b>';
                            }
                            else if (this.value == badCode) {
                                s = Highcharts.dateFormat('%a %H:%M:%S', this.x) + ' '
                                    + '<b>Služba ' + getChartStatus(this.value, 0) + '</b>';
                            }
                            else
                                s = Highcharts.dateFormat('%a %H:%M:%S', this.x) + ' '
                                    + '<b>Služba ' + getChartStatus(this.value, 0) + '</b><br/>'
                                    + '(' + val + ')';

                            return s;
                        }
                    },
                    turboThreshold: Number.MAX_VALUE, // #3404, remove after 4.0.5 release
                    data: [{}]

                }]");
            sb.AppendLine("};");
            //Highcharts options
            sb.AppendLine(@"$('#container" + uniqueId + @"').html('<center><img src=""/content/img/loading.gif"" style=""width:127px;height:auto;""><b>Stahujeme data</b>');
                        "+ $"$.getJSON('/StatniWeby/ChartData/{dataName}?h={hoursBack}&hh={hash}'" + @", function (data) {
                    chartopt" + uniqueId + @".series[0].data = data.data;
                    chartopt" + uniqueId + @".yAxis[0].categories = data.categories;
                    cats" + uniqueId + @" = data.cats;
                    colsize = data.colsize;
                    $('#container" + uniqueId + @"').html('');
                    $('#adbmsg" + uniqueId + @"').show();
                    chart" + uniqueId + @" = new Highcharts.Chart(chartopt" + uniqueId + @");
                    $('#adbmsg" + uniqueId + @"').hide();
                });");
            sb.AppendLine("}); //end chart");
            sb.AppendLine("</script>");

            return new HtmlString(sb.ToString());

        }
    }
}