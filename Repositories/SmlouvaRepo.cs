using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Devmasters;
using Devmasters.Batch;
using Devmasters.Net.HttpClient;
using HlidacStatu.Datastructures.Graphs;
using HlidacStatu.Connectors;
using HlidacStatu.Entities;
using HlidacStatu.Entities.Issues;
using HlidacStatu.Entities.XSD;
using HlidacStatu.Extensions;
using HlidacStatu.Lib.Analysis.KorupcniRiziko;
using HlidacStatu.Repositories.Searching;
using Nest;
using Manager = HlidacStatu.Repositories.ES.Manager;
using HlidacStatu.Lib.OCR;


namespace HlidacStatu.Repositories
{
    public static partial class SmlouvaRepo
    {
        public static Lib.OCR.Api.CallbackData CallbackDataForOCRReq(Smlouva smlouva, int prilohaindex)
        {
            var url = Config.GetWebConfigValue("ESConnection");

            if (smlouva.platnyZaznam)
                url = url + $"/{Manager.defaultIndexName}/smlouva/{smlouva.Id}/_update";
            else
                url = url + $"/{Manager.defaultIndexName_Sneplatne}/smlouva/{smlouva.Id}/_update";

            string callback =
                Lib.OCR.Api.CallbackData.PrepareElasticCallbackDataForOCRReq(
                    $"prilohy[{prilohaindex}].plainTextContent",
                    true);
            callback = callback.Replace("#ADDMORE#", $"ctx._source.prilohy[{prilohaindex}].lastUpdate = '#NOW#';"
                                                     + $"ctx._source.prilohy[{prilohaindex}].lenght = #LENGTH#;"
                                                     + $"ctx._source.prilohy[{prilohaindex}].wordCount=#WORDCOUNT#;"
                                                     + $"ctx._source.prilohy[{prilohaindex}].contentType='#CONTENTTYPE#'");

            return new Lib.OCR.Api.CallbackData(new Uri(url), callback,
                Lib.OCR.Api.CallbackData.CallbackType.LocalElastic);
        }

        public static bool Delete(Smlouva smlouva, ElasticClient client = null)
        {
            return Delete(smlouva.Id);
        }

        public static Smlouva[] GetPodobneSmlouvy(string idSmlouvy, IEnumerable<QueryContainer> mandatory,
            IEnumerable<QueryContainer> optional = null, IEnumerable<string> exceptIds = null, int numOfResults = 50)
        {
            optional = optional ?? new QueryContainer[] { };
            exceptIds = exceptIds ?? new string[] { };
            Smlouva[] _result = null;

            int tryNum = optional.Count();
            while (tryNum >= 0)
            {
                var query = mandatory.Concat(optional.Take(tryNum)).ToArray();
                tryNum--;

                var tmpResult = new List<Smlouva>();
                var res = SmlouvaRepo.Searching.RawSearch(
                    new QueryContainerDescriptor<Smlouva>().Bool(b => b.Must(query)),
                    1, numOfResults, SmlouvaRepo.Searching.OrderResult.DateAddedDesc, null
                );
                var resN = SmlouvaRepo.Searching.RawSearch(
                    new QueryContainerDescriptor<Smlouva>().Bool(b => b.Must(query)),
                    1, numOfResults, SmlouvaRepo.Searching.OrderResult.DateAddedDesc, null, platnyZaznam: false
                );

                if (res.IsValid == false)
                    Manager.LogQueryError<Smlouva>(res);
                else
                    tmpResult.AddRange(res.Hits.Select(m => m.Source).Where(m => m.Id != idSmlouvy));
                if (resN.IsValid == false)
                    Manager.LogQueryError<Smlouva>(resN);
                else
                    tmpResult.AddRange(resN.Hits.Select(m => m.Source).Where(m => m.Id != idSmlouvy));

                if (tmpResult.Count > 0)
                {
                    var resSml = tmpResult.Where(m =>
                        m.Id != idSmlouvy
                        && !exceptIds.Any(id => id == m.Id)
                    ).ToArray();
                    if (resSml.Length > 0)
                        _result = resSml;
                }
            }

            ;
            if (_result == null)
                _result = new Smlouva[] { }; //not found anything

            return _result.Take(numOfResults).ToArray();
        }

        static string[] bankyIcoList = new string[]
        {
            "29045371", "24131768", "04253434", "07662645", "63492555", "03814742", "06325416", "28198131", "47610921",
            "63078333", "45244782", "44848943", "00001350", "49241397", "60433566", "47116102", "14893649", "61858374",
            "07482728", "13584324", "05638216", "49279866", "47115378", "45317054", "27943445", "60192852", "25672720",
            "47115289", "27427901", "26080222", "07639996", "05658446", "28992610", "47116129", "27184765", "06718159",
            "49241257", "49240901", "28949587", "25083325", "07920245", "60197609", "25307835", "64948242", "00671126",
            "48550019", "01555332", "25778722", "25783301", "27444376", "64946649", "26137755", "64508889", "63083868"
        };

        private static void PrepareBeforeSave(Smlouva smlouva, bool updateLastUpdateValue = true)
        {
            smlouva.SVazbouNaPolitiky = smlouva.JeSmlouva_S_VazbouNaPolitiky(Relation.AktualnostType.Libovolny);
            smlouva.SVazbouNaPolitikyNedavne = smlouva.JeSmlouva_S_VazbouNaPolitiky(Relation.AktualnostType.Nedavny);
            smlouva.SVazbouNaPolitikyAktualni = smlouva.JeSmlouva_S_VazbouNaPolitiky(Relation.AktualnostType.Aktualni);

            if (updateLastUpdateValue)
                smlouva.LastUpdate = DateTime.Now;

            smlouva.ConfidenceValue = smlouva.GetConfidenceValue();

            /////// HINTS

            if (smlouva.Hint == null)
                smlouva.Hint = new HintSmlouva();


            smlouva.Hint.Updated = DateTime.Now;

            smlouva.Hint.SkrytaCena = smlouva.Issues?
                .Any(m => m.IssueTypeId == (int) IssueType.IssueTypes.Nulova_hodnota_smlouvy) == true
                ? 1
                : 0;

            Firma fPlatce = Firmy.Get(smlouva.Platce.ico);
            Firma[] fPrijemci = smlouva.Prijemce.Select(m => m.ico)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => Firmy.Get(m))
                .Where(f => f.Valid)
                .ToArray();

            List<Firma> firmy = fPrijemci
                .Concat(new Firma[] {fPlatce})
                .Where(f => f.Valid)
                .ToList();

            smlouva.Hint.DenUzavreni = (int) Devmasters.DT.Util.TypeOfDay(smlouva.datumUzavreni);

            if (firmy.Count() == 0)
                smlouva.Hint.PocetDniOdZalozeniFirmy = 9999;
            else
                smlouva.Hint.PocetDniOdZalozeniFirmy = (int) firmy
                    .Select(f => (smlouva.datumUzavreni - (f.Datum_Zapisu_OR ?? new DateTime(1990, 1, 1))).TotalDays)
                    .Min();

            smlouva.Hint.SmlouvaSPolitickyAngazovanymSubjektem = (int) HintSmlouva.PolitickaAngazovanostTyp.Neni;
            if (firmy.Any(f => f.IsSponzorBefore(smlouva.datumUzavreni)))
                smlouva.Hint.SmlouvaSPolitickyAngazovanymSubjektem =
                    (int) HintSmlouva.PolitickaAngazovanostTyp.PrimoSubjekt;
            else if (firmy.Any(f => f.MaVazbyNaPolitikyPred(smlouva.datumUzavreni)))
                smlouva.Hint.SmlouvaSPolitickyAngazovanymSubjektem =
                    (int) HintSmlouva.PolitickaAngazovanostTyp.AngazovanyMajitel;

            if (fPlatce.Valid && fPlatce.PatrimStatu())
            {
                if (fPrijemci.All(f => f.PatrimStatu()))
                    smlouva.Hint.VztahSeSoukromymSubjektem =
                        (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.PouzeStatStat;
                else if (fPrijemci.All(f => f.PatrimStatu() == false))
                    smlouva.Hint.VztahSeSoukromymSubjektem =
                        (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.PouzeStatSoukr;
                else
                    smlouva.Hint.VztahSeSoukromymSubjektem = (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.Kombinovane;
            }

            if (fPlatce.Valid && fPlatce.PatrimStatu() == false)
            {
                if (fPrijemci.All(f => f.PatrimStatu()))
                    smlouva.Hint.VztahSeSoukromymSubjektem =
                        (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.PouzeStatSoukr;
                else if (fPrijemci.All(f => f.PatrimStatu() == false))
                    smlouva.Hint.VztahSeSoukromymSubjektem =
                        (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.PouzeSoukrSoukr;
                else
                    smlouva.Hint.VztahSeSoukromymSubjektem = (int) HintSmlouva.VztahSeSoukromymSubjektemTyp.Kombinovane;
            }

            //U limitu
            smlouva.Hint.SmlouvaULimitu = (int) HintSmlouva.ULimituTyp.OK;
            //vyjimky
            //smlouvy s bankama o repo a vkladech
            bool vyjimkaNaLimit = false;
            Smlouva.SClassification.ClassificationsTypes[] vyjimkyClassif =
                new Smlouva.SClassification.ClassificationsTypes[]
                {
                    Smlouva.SClassification.ClassificationsTypes.finance_formality,
                    Smlouva.SClassification.ClassificationsTypes.finance_repo,
                    Smlouva.SClassification.ClassificationsTypes.finance_bankovni,
                    Smlouva.SClassification.ClassificationsTypes.dary_obecne
                };

            if (vyjimkaNaLimit == false)
            {
                if (
                    (
                        smlouva.hodnotaBezDph >= Consts.Limit1bezDPH_From
                        && smlouva.hodnotaBezDph <= Consts.Limit1bezDPH_To
                    )
                    ||
                    (
                        smlouva.CalculatedPriceWithVATinCZK > Consts.Limit1bezDPH_From * 1.21m
                        && smlouva.CalculatedPriceWithVATinCZK <= Consts.Limit1bezDPH_To * 1.21m
                    )
                )
                    smlouva.Hint.SmlouvaULimitu = (int) HintSmlouva.ULimituTyp.Limit2M;

                if (
                    (
                        smlouva.hodnotaBezDph >= Consts.Limit2bezDPH_From
                        && smlouva.hodnotaBezDph <= Consts.Limit2bezDPH_To
                    )
                    ||
                    (
                        smlouva.CalculatedPriceWithVATinCZK > Consts.Limit2bezDPH_From * 1.21m
                        && smlouva.CalculatedPriceWithVATinCZK <= Consts.Limit2bezDPH_To * 1.21m
                    )
                )
                    smlouva.Hint.SmlouvaULimitu = (int) HintSmlouva.ULimituTyp.Limit6M;
            }

            if (smlouva.Prilohy != null)
            {
                foreach (var p in smlouva.Prilohy)
                    p.UpdateStatistics();
            }


            if (ClassificationOverrideRepo.TryGetOverridenClassification(smlouva.Id, out var classificationOverride))
            {
                var types = new List<Smlouva.SClassification.Classification>();
                if (classificationOverride.CorrectCat1.HasValue)
                    types.Add(new Smlouva.SClassification.Classification()
                    {
                        TypeValue = classificationOverride.CorrectCat1.Value,
                        ClassifProbability = 0.9m
                    });
                if (classificationOverride.CorrectCat2.HasValue)
                    types.Add(new Smlouva.SClassification.Classification()
                    {
                        TypeValue = classificationOverride.CorrectCat1.Value,
                        ClassifProbability = 0.8m
                    });

                smlouva.Classification = new Smlouva.SClassification(types.ToArray());
            }
        }

        public static bool JeSmlouva_S_VazbouNaPolitiky(this Smlouva smlouva, Relation.AktualnostType aktualnost)
        {
            var icos = ico_s_VazbouPolitik;
            if (aktualnost == Relation.AktualnostType.Nedavny)
                icos = ico_s_VazbouPolitikNedavne;
            if (aktualnost == Relation.AktualnostType.Aktualni)
                icos = ico_s_VazbouPolitikAktualni;


            Firma f = null;
            if (smlouva.platnyZaznam)
            {
                f = Firmy.Get(smlouva.Platce.ico);

                if (f.Valid && !f.PatrimStatu())
                {
                    if (!string.IsNullOrEmpty(smlouva.Platce.ico) && icos.Contains(smlouva.Platce.ico))
                        return true;
                }

                foreach (var ss in smlouva.Prijemce)
                {
                    f = Firmy.Get(ss.ico);
                    if (f.Valid && !f.PatrimStatu())
                    {
                        if (!string.IsNullOrEmpty(ss.ico) && icos.Contains(ss.ico))
                            return true;
                    }
                }
            }

            return false;
        }

        static HashSet<string> ico_s_VazbouPolitik = new HashSet<string>(
            StaticData.FirmySVazbamiNaPolitiky_vsechny_Cache.Get().SoukromeFirmy.Select(m => m.Key)
                .Union(StaticData.SponzorujiciFirmy_Vsechny.Get().Select(m => m.IcoDarce))
                .Distinct()
        );

        static HashSet<string> ico_s_VazbouPolitikAktualni = new HashSet<string>(
            StaticData.FirmySVazbamiNaPolitiky_aktualni_Cache.Get().SoukromeFirmy.Select(m => m.Key)
                .Union(StaticData.SponzorujiciFirmy_Nedavne.Get().Select(m => m.IcoDarce))
                .Distinct()
        );

        static HashSet<string> ico_s_VazbouPolitikNedavne = new HashSet<string>(
            StaticData.FirmySVazbamiNaPolitiky_nedavne_Cache.Get().SoukromeFirmy.Select(m => m.Key)
                .Union(StaticData.SponzorujiciFirmy_Nedavne.Get().Select(m => m.IcoDarce))
                .Distinct()
        );

        public static bool Save(Smlouva smlouva, ElasticClient client = null, bool updateLastUpdateValue = true)
        {
            if (smlouva == null)
                return false;

            PrepareBeforeSave(smlouva, updateLastUpdateValue);
            ElasticClient c = client;
            if (c == null)
            {
                if (smlouva.platnyZaznam)
                    c = Manager.GetESClient();
                else
                    c = Manager.GetESClient_Sneplatne();
            }

            var res = c
                //.Update<Smlouva>()
                .Index<Smlouva>(smlouva, m => m.Id(smlouva.Id));

            if (smlouva.platnyZaznam == false && res.IsValid)
            {
                //zkontroluj zda neni v indexu s platnymi. pokud ano, smaz ho tam
                var cExist = Manager.GetESClient();
                var s = Load(smlouva.Id, cExist);
                if (s != null)
                    Delete(smlouva.Id, cExist);
            }

            if (res.IsValid)
            {
                try
                {
                    DirectDB.NoResult("exec smlouvaId_save @id,@active, @created, @updated",
                        new SqlParameter("id", smlouva.Id),
                        new SqlParameter("created", smlouva.casZverejneni),
                        new SqlParameter("updated", smlouva.LastUpdate),
                        new SqlParameter("active",
                            smlouva.znepristupnenaSmlouva() ? (int) 0 : (int) 1)
                    );
                }
                catch (Exception e)
                {
                    Manager.ESLogger.Error("Manager Save", e);
                }


                if (!string.IsNullOrEmpty(smlouva.Platce?.ico))
                {
                    DirectDB.NoResult("exec Firma_IsInRS_Save @ico",
                        new SqlParameter("ico", smlouva.Platce?.ico)
                    );
                }

                if (!string.IsNullOrEmpty(smlouva.VkladatelDoRejstriku?.ico))
                {
                    DirectDB.NoResult("exec Firma_IsInRS_Save @ico",
                        new SqlParameter("ico", smlouva.VkladatelDoRejstriku?.ico)
                    );
                }

                foreach (var s in smlouva.Prijemce ?? new Smlouva.Subjekt[] { })
                {
                    if (!string.IsNullOrEmpty(s.ico))
                    {
                        DirectDB.NoResult("exec Firma_IsInRS_Save @ico",
                            new SqlParameter("ico", s.ico)
                        );
                    }
                }
            }

            return res.IsValid;
        }

        public static void SaveAttachmentsToDisk(Smlouva smlouva, bool rewriteExisting = false)
        {
            var io = Init.PrilohaLocalCopy;

            int count = 0;
            string listing = "";
            if (smlouva.Prilohy != null)
            {
                if (!Directory.Exists(io.GetFullDir(smlouva)))
                    Directory.CreateDirectory(io.GetFullDir(smlouva));

                foreach (var p in smlouva.Prilohy)
                {
                    string attUrl = p.odkaz;
                    if (string.IsNullOrEmpty(attUrl))
                        continue;
                    count++;
                    string fullPath = io.GetFullPath(smlouva, p);
                    listing = listing + string.Format("{0} : {1} \n", count, WebUtility.UrlDecode(attUrl));
                    if (!File.Exists(fullPath) || rewriteExisting)
                    {
                        try
                        {
                            using (URLContent url =
                                new URLContent(attUrl))
                            {
                                url.Timeout = url.Timeout * 10;
                                byte[] data = url.GetBinary().Binary;
                                File.WriteAllBytes(fullPath, data);
                                //p.LocalCopy = System.Text.UTF8Encoding.UTF8.GetBytes(io.GetRelativePath(item, p));
                            }
                        }
                        catch (Exception e)
                        {
                            Util.Consts.Logger.Error(attUrl, e);
                        }
                    }

                    if (p.hash == null)
                    {
                        using (FileStream filestream = new FileStream(fullPath, FileMode.Open))
                        {
                            using (SHA256 mySHA256 = SHA256.Create())
                            {
                                filestream.Position = 0;
                                byte[] hashValue = mySHA256.ComputeHash(filestream);
                                p.hash = new tHash()
                                {
                                    algoritmus = "sha256",
                                    Value = BitConverter.ToString(hashValue).Replace("-", String.Empty)
                                };
                            }
                        }
                    }
                }
            }
        }

        public static void ZmenStavSmlouvyNa(Smlouva smlouva, bool platnyZaznam)
        {
            var issueTypeId = -1;
            var issue = new Issue(null, issueTypeId, "Smlouva byla znepřístupněna",
                "Na žádost subjektu byla tato smlouva znepřístupněna.", ImportanceLevel.Formal,
                permanent: true);

            if (platnyZaznam && smlouva.znepristupnenaSmlouva())
            {
                smlouva.platnyZaznam = platnyZaznam;
                //zmen na platnou
                if (smlouva.Issues.Any(m => m.IssueTypeId == issueTypeId))
                {
                    smlouva.Issues = smlouva.Issues
                        .Where(m => m.IssueTypeId != issueTypeId)
                        .ToArray();
                }

                Save(smlouva);
            }
            else if (platnyZaznam == false && smlouva.znepristupnenaSmlouva() == false)
            {
                smlouva.platnyZaznam = platnyZaznam;
                if (!smlouva.Issues.Any(m => m.IssueTypeId == -1))
                    smlouva.AddSpecificIssue(issue);
                Save(smlouva);
            }
        }

        public static IEnumerable<string> _allIdsFromES(bool deleted, Action<string> outputWriter = null,
            Action<ActionProgressData> progressWriter = null)
        {
            List<string> ids = new List<string>();
            var client = deleted ? Manager.GetESClient_Sneplatne() : Manager.GetESClient();

            Func<int, int, ISearchResponse<Smlouva>> searchFunc =
                searchFunc = (size, page) =>
                {
                    return client.Search<Smlouva>(a => a
                        .Size(size)
                        .From(page * size)
                        .Source(false)
                        .Query(q => q.Term(t => t.Field(f => f.platnyZaznam).Value(deleted ? false : true)))
                        .Scroll("1m")
                    );
                };


            Tools.DoActionForQuery<Smlouva>(client,
                searchFunc, (hit, param) =>
                {
                    ids.Add(hit.Id);
                    return new ActionOutputData() {CancelRunning = false, Log = null};
                }, null, outputWriter, progressWriter, false, blockSize: 100);

            return ids;
        }

        public static IEnumerable<string> AllIdsFromDB()
        {
            return AllIdsFromDB(null);
        }

        public static IEnumerable<string> AllIdsFromDB(bool? deleted)
        {
            List<string> ids = null;
            using (DbEntities db = new DbEntities())
            {
                IQueryable<SmlouvyId> q = db.SmlouvyIds;
                if (deleted.HasValue)
                    q = q.Where(m => m.Active == (deleted.Value ? 0 : 1));

                ids = q.Select(m => m.Id)
                    .ToList();
            }

            return ids;
        }

        public static IEnumerable<string> AllIdsFromES()
        {
            return AllIdsFromES(null);
        }

        public static IEnumerable<string> AllIdsFromES(bool? deleted, Action<string> outputWriter = null,
            Action<ActionProgressData> progressWriter = null)
        {
            if (deleted.HasValue)
                return _allIdsFromES(deleted.Value, outputWriter, progressWriter);
            else
                return
                    _allIdsFromES(false, outputWriter, progressWriter)
                        .Union(_allIdsFromES(true, outputWriter, progressWriter))
                    ;
        }

        public static bool Delete(string Id, ElasticClient client = null)
        {
            if (client == null)
                client = Manager.GetESClient();
            var res = client
                .Delete<Smlouva>(Id);
            return res.IsValid;
        }

        public static bool ExistsZaznam(string id, ElasticClient client = null)
        {
            bool noSetClient = client == null;
            if (client == null)
                client = Manager.GetESClient();
            var res = client
                .DocumentExists<Smlouva>(id);
            if (noSetClient)
            {
                if (res.Exists)
                    return true;
                client = Manager.GetESClient_Sneplatne();
                res = client.DocumentExists<Smlouva>(id);
                return res.Exists;
            }
            else
                return res.Exists;
        }


        public static StringBuilder ExportData(string query, int count, string order,
            ExportDataFormat format, bool withPlainText, out string contenttype)
        {
            //TODO ignored format

            contenttype = "text/tab-separated-values";

            StringBuilder sb = new StringBuilder();

            if (count > 10000)
                count = 10000;

            Func<int, int, ISearchResponse<Smlouva>> searchFunc =
                (size, page) =>
                {
                    return Manager.GetESClient().Search<Smlouva>(a => a
                        .Size(size)
                        .Source(m => m.Excludes(e => e.Field(o => o.Prilohy)))
                        .From(page * size)
                        .Query(q => SmlouvaRepo.Searching.GetSimpleQuery(query))
                        .Scroll("1m")
                    );
                };

            sb.AppendLine(
                "URL\tID smlouvy\tPodepsána\tZveřejněna\tHodnota smlouvy\tPředmět smlouvy\tPlátce\tPlatce IC\tDodavatele a jejich ICO");
            int c = 0;
            Tools.DoActionForQuery<Smlouva>(Manager.GetESClient(),
                searchFunc, (hit, param) =>
                {
                    var s = hit.Source;
                    sb.AppendLine(
                        s.GetUrl(false) + "\t"
                                        + s.Id + "\t"
                                        + s.datumUzavreni.ToString("dd.MM.yyyy") + "\t"
                                        + s.casZverejneni.ToString("dd.MM.yyyy") + "\t"
                                        + s.CalculatedPriceWithVATinCZK.ToString(Util.Consts.czCulture) + "\t"
                                        + TextUtil.NormalizeToBlockText(s.predmet) + "\t"
                                        + s.Platce.nazev + "\t"
                                        + s.Platce.ico + "\t"
                                        + ((s.Prijemce?.Count() > 0)
                                            ? s.Prijemce.Select(p => p.nazev + "\t" + p.ico)
                                                .Aggregate((f, sec) => f + "\t" + sec)
                                            : "")
                    );

                    Console.Write(c++);
                    return new ActionOutputData() {CancelRunning = false, Log = null};
                }, null, null, null, false);


            return sb;
        }

        public static Smlouva Load(string idVerze, ElasticClient client = null, bool includePrilohy = true)
        {
            var s = _load(idVerze, client, includePrilohy);
            if (s == null)
                return s;
            var sclass = s.GetRelevantClassification();
            if (s.Classification?.Version == 1 && s.Classification?.Types != null)
            {
                s.Classification.ConvertToV2();
                Save(s, null, false);
            }

            return s;
        }

        private static Smlouva _load(string idVerze, ElasticClient client = null, bool includePrilohy = true)
        {
            bool specClient = client != null;
            try
            {
                ElasticClient c = null;
                if (specClient)
                    c = client;
                else
                    c = Manager.GetESClient();

                //var res = c.Get<Smlouva>(idVerze);

                var res = includePrilohy
                    ? c.Get<Smlouva>(idVerze)
                    : c.Get<Smlouva>(idVerze, s => s.SourceExcludes(sml => sml.Prilohy));


                if (res.Found)
                    return res.Source;
                else
                {
                    if (specClient == false)
                    {
                        var c1 = Manager.GetESClient_Sneplatne();

                        res = includePrilohy
                            ? c1.Get<Smlouva>(idVerze)
                            : c1.Get<Smlouva>(idVerze, s => s.SourceExcludes(sml => sml.Prilohy));

                        if (res.Found)
                            return res.Source;
                        else if (res.IsValid)
                        {
                            Manager.ESLogger.Warning("Valid Req: Cannot load Smlouva Id " + idVerze +
                                                     "\nDebug:" + res.DebugInformation);
                            //DirectDB.NoResult("delete from SmlouvyIds where id = @id", new System.Data.SqlClient.SqlParameter("id", idVerze));
                        }
                        else if (res.Found == false)
                            return null;
                        else if (res.ServerError.Status == 404)
                            return null;
                        else
                        {
                            Manager.ESLogger.Error(
                                "Invalid Req: Cannot load Smlouva Id " + idVerze + "\n Debug:" + res.DebugInformation +
                                " \nServerError:" + res.ServerError?.ToString(), res.OriginalException);
                        }
                    }

                    return null;
                }
            }
            catch (Exception e)
            {
                Manager.ESLogger.Error("Cannot load Smlouva Id " + idVerze, e);
                return null;
            }
        }

        public static string GetFileFromPrilohaRepository(Smlouva.Priloha att,
            Smlouva smlouva)
        {
            var ext = ".pdf";
            try
            {
                ext = new System.IO.FileInfo(att.nazevSouboru).Extension;
            }
            catch (Exception)
            {
                Util.Consts.Logger.Warning("invalid file name " + (att?.nazevSouboru ?? "(null)"));
            }


            string localFile = Init.PrilohaLocalCopy.GetFullPath(smlouva, att);
            var tmpPath = System.IO.Path.GetTempPath();
            Devmasters.IO.IOTools.DeleteFile(tmpPath);
            if (!System.IO.Directory.Exists(tmpPath))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(tmpPath);
                }
                catch
                {
                }
            }

            string tmpFnSystem = System.IO.Path.GetTempFileName();
            string tmpFn = tmpFnSystem + DocTools.PrepareFilenameForOCR(att.nazevSouboru);
            try
            {
                //System.IO.File.Delete(fn);
                if (System.IO.File.Exists(localFile))
                {
                    //do local copy
                    Util.Consts.Logger.Debug(
                        $"Copying priloha {att.nazevSouboru} for smlouva {smlouva.Id} from local disk {localFile}");
                    System.IO.File.Copy(localFile, tmpFn, true);
                }
                else
                {
                    try
                    {
                        Util.Consts.Logger.Debug(
                            $"Downloading priloha {att.nazevSouboru} for smlouva {smlouva.Id} from URL {att.odkaz}");
                        byte[] data = null;
                        using (Devmasters.Net.HttpClient.URLContent web =
                            new Devmasters.Net.HttpClient.URLContent(att.odkaz))
                        {
                            web.Timeout = web.Timeout * 10;
                            data = web.GetBinary().Binary;
                            System.IO.File.WriteAllBytes(tmpFn, data);
                        }

                        Util.Consts.Logger.Debug(
                            $"Downloaded priloha {att.nazevSouboru} for smlouva {smlouva.Id} from URL {att.odkaz}");
                    }
                    catch (Exception)
                    {
                        try
                        {
                            if (Uri.TryCreate(att.odkaz, UriKind.Absolute, out var urlTmp))
                            {
                                byte[] data = null;
                                Util.Consts.Logger.Debug(
                                    $"Second try: Downloading priloha {att.nazevSouboru} for smlouva {smlouva.Id} from URL {att.odkaz}");
                                using (Devmasters.Net.HttpClient.URLContent web =
                                    new Devmasters.Net.HttpClient.URLContent(att.odkaz))
                                {
                                    web.Tries = 5;
                                    web.IgnoreHttpErrors = true;
                                    web.TimeInMsBetweenTries = 1000;
                                    web.Timeout = web.Timeout * 20;
                                    data = web.GetBinary().Binary;
                                    System.IO.File.WriteAllBytes(tmpFn, data);
                                }

                                Util.Consts.Logger.Debug(
                                    $"Second try: Downloaded priloha {att.nazevSouboru} for smlouva {smlouva.Id} from URL {att.odkaz}");
                                return tmpFn;
                            }

                            return null;
                        }
                        catch (Exception e)
                        {
                            Util.Consts.Logger.Error(att.odkaz, e);
                            return null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Util.Consts.Logger.Error(att.odkaz, e);
                throw;
            }
            finally
            {
                Devmasters.IO.IOTools.DeleteFile(tmpFnSystem);
            }

            return tmpFn;
        }
    }
}