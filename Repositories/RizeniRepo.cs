using System;
using System.Linq;
using HlidacStatu.Entities;
using HlidacStatu.Lib;
using HlidacStatu.Entities.Insolvence;
using HlidacStatu.Repositories.ES;
using Nest;

namespace HlidacStatu.Repositories
{
    public static class RizeniRepo
    {
        public static void PrepareForSave(Rizeni rizeni, bool skipOsobaIdLink = false)
        {
            if (skipOsobaIdLink == false)
                rizeni.OnRadar = false; //reset settings

            foreach (var d in rizeni.Dluznici)
            {
                if (skipOsobaIdLink == false && d.OsobaId == null && d.DatumNarozeni.HasValue)
                {
                    //try to find osobaId from db
                    var found = Validators.JmenoInText(d.PlneJmeno);
                    if (found != null)
                    {
                        var osoba =
                            OsobaRepo.Searching.GetByName(found.Jmeno, found.Prijmeni, d.DatumNarozeni.Value);
                        if (osoba != null)
                        {
                            d.OsobaId = osoba.NameId;
                            rizeni.OnRadar = rizeni.OnRadar || osoba.Status > 0;
                        }
                        else
                            d.OsobaId = "";
                    }
                    else
                        d.OsobaId = "";
                }
            }

            foreach (var d in rizeni.Spravci)
            {
                if (skipOsobaIdLink == false && d.OsobaId == null && d.DatumNarozeni.HasValue)
                {
                    //try to find osobaId from db
                    var found = Validators.JmenoInText(d.PlneJmeno);
                    if (found != null)
                    {
                        var osoba =
                            OsobaRepo.Searching.GetByName(found.Jmeno, found.Prijmeni, d.DatumNarozeni.Value);
                        if (osoba != null)
                        {
                            d.OsobaId = osoba.NameId;
                            //this.OnRadar = this.OnRadar || o.Status > 0;
                        }
                        else
                            d.OsobaId = "";
                    }
                    else
                        d.OsobaId = "";
                }
            }

            foreach (var d in rizeni.Veritele)
            {
                if (skipOsobaIdLink == false && d.OsobaId == null && d.DatumNarozeni.HasValue)
                {
                    //try to find osobaId from db
                    var found = Validators.JmenoInText(d.PlneJmeno);
                    if (found != null)
                    {
                        var osoba =
                            OsobaRepo.Searching.GetByName(found.Jmeno, found.Prijmeni, d.DatumNarozeni.Value);
                        if (osoba != null)
                        {
                            d.OsobaId = osoba.NameId;
                            //this.OnRadar = this.OnRadar || o.Status > 0;
                        }
                        else
                            d.OsobaId = "";
                    }
                    else
                        d.OsobaId = "";
                }
            }

            if (rizeni.Dluznici.Any(m => !(m.Typ == "F" || m.Typ == "PODNIKATEL")))
                rizeni.OnRadar = true;
            else
            {
                if (skipOsobaIdLink == false)
                {
                    foreach (var d in rizeni.Dluznici)
                    {
                        if (OsobaRepo.PolitickyAktivni.Get().Any(m =>
                                m.JmenoAscii == Devmasters.TextUtil.RemoveDiacritics(d.Jmeno())
                                && m.PrijmeniAscii == Devmasters.TextUtil.RemoveDiacritics(d.Prijmeni())
                                && m.Narozeni == d.GetDatumNarozeni() && d.GetDatumNarozeni().HasValue
                            )
                        )
                            rizeni.OnRadar = true;
                        break;
                    }
                }
            }
        }

        public static Lib.OCR.Api.CallbackData CallbackDataForOCRReq(Rizeni rizeni, int prilohaindex)
        {
            var url = Devmasters.Config.GetWebConfigValue("ESConnection");

            url = url +
                  $"/{Manager.defaultIndexName_Insolvence}/rizeni/{System.Net.WebUtility.UrlEncode(rizeni.SpisovaZnacka)}/_update";

            string callback =
                Lib.OCR.Api.CallbackData.PrepareElasticCallbackDataForOCRReq($"dokumenty[{prilohaindex}].plainText", true);
            callback = callback.Replace("#ADDMORE#", $"ctx._source.dokumenty[{prilohaindex}].lastUpdate = '#NOW#';"
                                                     + $"ctx._source.dokumenty[{prilohaindex}].lenght = #LENGTH#;"
                                                     + $"ctx._source.dokumenty[{prilohaindex}].wordCount=#WORDCOUNT#;");

            return new Lib.OCR.Api.CallbackData(new Uri(url), callback, Lib.OCR.Api.CallbackData.CallbackType.LocalElastic);
        }

        public static void Save(Rizeni rizeni, ElasticClient client = null, bool? forceOnRadarValue = null)
        {
            if (rizeni.IsFullRecord == false)
                throw new ApplicationException("Cannot save partial Insolvence document");

            if (client == null)
                client = Manager.GetESClient_Insolvence();
            
            PrepareForSave(rizeni);
            if (forceOnRadarValue.HasValue)
                rizeni.OnRadar = forceOnRadarValue.Value;

            var res = client.Index<Rizeni>(rizeni,
                o => o.Id(rizeni.SpisovaZnacka)); //druhy parametr musi byt pole, ktere je unikatni
            if (!res.IsValid)
            {
                throw new ApplicationException(res.ServerError?.ToString());
            }
        }
    }
}