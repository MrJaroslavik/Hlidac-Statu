using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using HlidacStatu.Connectors;
using HlidacStatu.Entities;
using HlidacStatu.Entities.Views;
using Microsoft.EntityFrameworkCore;

namespace HlidacStatu.Repositories
{
    public static class SponzoringRepo
    {
        public static string[] VelkeStrany = new string[]
        {
            "ANO", "ODS", "ČSSD", "Česká pirátská strana", "KSČM",
            "SPD", "STAN", "KDU-ČSL", "TOP 09", "Strana zelených"
        };

        public static string[] ParlamentníStrany = new string[]
        {
            "ANO", "ODS", "Česká pirátská strana",
            "SPD", "STAN", "KDU-ČSL", "TOP 09"
        };
        public static string[] TopStrany = VelkeStrany.ToArray();

        public static int DefaultLastSponzoringYear()
        {
            return HlidacStatu.Connectors.DirectDB
                .GetList<int?>("select max(DATEPART(yy, Sponzoring.DarovanoDne)) from sponzoring where sponzoring.darovanoDne < GetDate()")
                .FirstOrDefault()
                 ?? (DateTime.Now.Year - 1);
        }



        private static DateTime minBigSponzoringDate = new DateTime(DateTime.Now.Year - 10, 1, 1);
        private static DateTime minSmallSponzoringDate = new DateTime(DateTime.Now.Year - 5, 1, 1);
        private static decimal smallSponzoringThreshold = 5000;

        public static Expression<Func<Sponzoring, bool>> SponzoringLimitsPredicate = s =>
            (s.Hodnota > smallSponzoringThreshold && s.DarovanoDne >= minBigSponzoringDate)
            || (s.Hodnota <= smallSponzoringThreshold && s.DarovanoDne >= minSmallSponzoringDate);

        public static Expression<Func<SponzoringSummed, bool>> SponzoringSummedLimitsPredicate = s =>
            (s.DarCelkem > smallSponzoringThreshold && s.Rok >= minBigSponzoringDate.Year)
            || (s.DarCelkem <= smallSponzoringThreshold && s.Rok >= minSmallSponzoringDate.Year);


        public static List<Sponzoring> GetByDarce(int osobaId, Expression<Func<Sponzoring, bool>> predicate)
        {
            using (DbEntities db = new DbEntities())
            {
                var osobySponzoring = db.Sponzoring
                    .AsNoTracking()
                    .Where(s => s.OsobaIdDarce == osobaId)
                    .Where(SponzoringLimitsPredicate)
                    .Where(predicate)
                    .ToList();

                //sponzoring z navazanych firem kdyz byl statutar
                var firmySponzoring = Osoby.CachedFirmySponzoring.Get(osobaId)
                    .AsQueryable()
                    .Where(SponzoringLimitsPredicate)
                    .Where(predicate)
                    .ToList();

                osobySponzoring.AddRange(firmySponzoring);

                return osobySponzoring;
            }
        }

        public static List<Sponzoring> GetByDarce(string icoDarce, Expression<Func<Sponzoring, bool>> predicate)
        {
            using (DbEntities db = new DbEntities())
            {
                return db.Sponzoring
                    .AsNoTracking()
                    .Where(predicate)
                    .Where(s => s.IcoDarce == icoDarce)
                    .ToList();
            }
        }

        public static List<Sponzoring> GetByPrijemce(int osobaId)
        {
            using (DbEntities db = new DbEntities())
            {
                return db.Sponzoring.AsNoTracking()
                    .Where(s => s.OsobaIdPrijemce == osobaId)
                    .Where(SponzoringLimitsPredicate)
                    .ToList();
            }
        }

        public static List<Sponzoring> GetByPrijemce(string icoPrijemce)
        {
            using (DbEntities db = new DbEntities())
            {
                return db.Sponzoring.AsNoTracking()
                    .Where(s => s.IcoPrijemce == icoPrijemce)
                    .Where(SponzoringLimitsPredicate)
                    .ToList();
            }
        }
        
        public static async Task<List<SponzoringDetail>> GetByPrijemceWithPersonDetailsAsync(string icoPrijemce, 
            CancellationToken cancellationToken = default)
        {
            
            using (DbEntities db = new DbEntities())
            {
                return await db.SponzoringDetails.FromSqlInterpolated(
                    $@"SELECT os.NameId NameIdDarce, os.Jmeno JmenoDarce, os.Prijmeni PrijmeniDarce, os.Narozeni DaumNarozeniDarce,
                              sp.IcoDarce, sp.IcoPrijemce, sp.Typ TypDaru, sp.Hodnota HodnotaDaru, sp.Popis, sp.DarovanoDne
                        FROM dbo.Sponzoring sp
                        Left Join dbo.Osoba os on sp.OsobaIdDarce = os.InternalId
                        WHERE sp.IcoPrijemce = {icoPrijemce}")
                    .ToListAsync(cancellationToken);
                
            }
        }

        public static Sponzoring Create(Sponzoring sponzoring, string user)
        {
            using (DbEntities db = new DbEntities())
            {
                return Create(sponzoring, user, db);
            }
        }

        private static Sponzoring Create(Sponzoring sponzoring, string user, DbEntities db)
        {
            if (sponzoring.OsobaIdDarce == 0
                && string.IsNullOrWhiteSpace(sponzoring.IcoDarce))
                throw new Exception(
                    "Cant attach sponzoring to a person or to a company since their reference is empty");

            sponzoring.Created = DateTime.Now;
            sponzoring.Edited = DateTime.Now;
            sponzoring.UpdatedBy = user;

            db.Sponzoring.Add(sponzoring);
            db.SaveChanges();

            AuditRepo.Add(Audit.Operations.Create, user, sponzoring, null);
            return sponzoring;
        }

        public static void MergeDonatingOsoba(int originalOsobaId, int duplicateOsobaId, string user)
        {
            if (originalOsobaId == 0 && duplicateOsobaId == 0)
                throw new ArgumentException("Id osoby nesmí být 0");

            using (DbEntities db = new DbEntities())
            {
                var sponzoring = db.Sponzoring.AsEnumerable().Where(s => s.OsobaIdDarce == duplicateOsobaId).ToList();

                foreach (var donation in sponzoring)
                {
                    var donationBackup = donation.ShallowCopy();

                    donation.OsobaIdDarce = originalOsobaId;
                    donation.Edited = DateTime.Now;
                    AuditRepo.Add(Audit.Operations.Update, user, donation, donationBackup);
                }

                db.SaveChanges();
            }
        }

        public static void Delete(Sponzoring sponzoring, string user)
        {
            if (sponzoring.Id > 0)
            {
                using (DbEntities db = new DbEntities())
                {
                    db.Sponzoring.Attach(sponzoring);
                    db.Entry(sponzoring).State = EntityState.Deleted;
                    AuditRepo.Add(Audit.Operations.Delete, user, sponzoring, null);

                    db.SaveChanges();
                }
            }
        }


        //přidat cache
        public static async Task<List<SponzoringOverview>> PartiesPerYearsOverviewAsync(int? year, CancellationToken cancellationToken)
        {
            int rok = year ?? 0;
            int yearSwitch = year.HasValue ? 0 : 1;

            using (DbEntities db = new DbEntities())
            {
                return await db.SponzoringOverviewView.FromSqlInterpolated(
                    $@"SELECT zs.KratkyNazev, IcoPrijemce as IcoStrany
                      ,Year(DarovanoDne) as Rok, SUM(Hodnota) as DaryCelkem
                      ,SUM(case when icodarce is null or Len(IcoDarce) < 3 then Hodnota end) as DaryOsob
                      ,SUM(case when icodarce is not null and Len(IcoDarce) >= 3 then Hodnota end) as DaryFirem
                      ,COUNT(distinct osobaiddarce) as PocetDarujicichOsob
                      ,COUNT(distinct icodarce) as PocetDarujicichFirem
                      FROM Sponzoring sp
                      Left Join ZkratkaStrany zs on sp.IcoPrijemce = zs.ICO
                      WHERE (year(sp.DarovanoDne) = {rok} or 1={yearSwitch})
                      group by zs.KratkyNazev, IcoPrijemce, Year(DarovanoDne)")
                    .ToListAsync(cancellationToken);
            }
        }
        public static async Task<Dictionary<string, string>> StranyIco()
        {

            var res = DirectDB.GetList<string, string>(@"SELECT zs.KratkyNazev, IcoPrijemce as IcoStrany
                      FROM Sponzoring sp
                      Left Join ZkratkaStrany zs on sp.IcoPrijemce = zs.ICO
                      group by zs.KratkyNazev, IcoPrijemce");
            return res.ToDictionary(k => k.Item1 ?? Firmy.GetJmeno(k.Item2), v => v.Item2);

        }

        public static async Task<Dictionary<int, decimal>> SponzoringPerYear(string party, int minYear, int maxYear, bool persons, bool companies)
        {
            string icoStrany = ZkratkaStranyRepo.IcoStrany(party);
            using (DbEntities db = new DbEntities())
            {
                var dataPerY = db.Sponzoring
                        .Where(m => m.IcoPrijemce == icoStrany && m.DarovanoDne.Value.Year >= minYear && m.DarovanoDne.Value.Year <= maxYear)
                        .Where(m => (persons && m.OsobaIdDarce != null) || (companies && m.IcoDarce != null))
                        .GroupBy(k => k.DarovanoDne.Value.Year, m => m, (k, v) => new { rok = k, sum = v.Sum(m => m.Hodnota ?? 0) })
                        .ToDictionary(k => k.rok, v => v.sum);


                //add missing years
                for (int year = minYear; year <= maxYear; year++)
                    if (!dataPerY.ContainsKey(year))
                        dataPerY.Add(year, 0);

                return dataPerY
                    .OrderBy(m => m.Key)
                    .ToDictionary(k => k.Key, m => m.Value);

            }
        }

        public static async Task<List<SponzoringSummed>> PeopleSponsorsAsync(string party, CancellationToken cancellationToken)
        {
            string icoStrany = ZkratkaStranyRepo.IcoStrany(party);
            int tenYearsBack = DateTime.Now.Year - 10;

            using (DbEntities db = new DbEntities())
            {
                return await db.SponzoringSummedView.FromSqlInterpolated(
                    $@"SELECT zs.KratkyNazev as NazevStrany
                       ,sp.IcoPrijemce as IcoStrany
	                   ,SUM(Hodnota) as DarCelkem
	                   ,Year(DarovanoDne) as Rok
	                   ,os.NameId as Id
	                   ,sp.icoDarce as IcoDarce
	                   ,RTRIM(LTRIM(isnull(os.TitulPred,'') + ' ' + os.Jmeno + ' ' + os.Prijmeni + ' ' + isnull(os.TitulPo,''))) as Jmeno
                       ,1 as typ
                    FROM Sponzoring sp
                    LEFT Join ZkratkaStrany zs on sp.IcoPrijemce = zs.ICO
                    join Osoba os on sp.OsobaIdDarce = os.InternalId
                    where OsobaIdDarce > 0
                      and sp.IcoPrijemce = {icoStrany}
                      and year(sp.DarovanoDne) >= {tenYearsBack}
                    group by zs.KratkyNazev, IcoPrijemce, Year(DarovanoDne)
                      , os.NameId, os.TitulPred, os.Jmeno, os.Prijmeni, os.TitulPo, sp.icoDarce")
                    .Where(SponzoringSummedLimitsPredicate)
                    .ToListAsync(cancellationToken);
            }
        }

        public static async Task<List<SponzoringSummed>> CompanySponsorsAsync(string party, CancellationToken cancellationToken)
        {
            string icoStrany = ZkratkaStranyRepo.IcoStrany(party);
            int tenYearsBack = DateTime.Now.Year - 10;

            using (DbEntities db = new DbEntities())
            {
                return await db.SponzoringSummedView.FromSqlInterpolated(
                        $@"SELECT zs.KratkyNazev as NazevStrany
                               ,sp.IcoPrijemce as IcoStrany
	                           ,SUM(Hodnota) as DarCelkem
	                           ,Year(DarovanoDne) as Rok
	                           ,fi.ICO as Id
	                           ,fi.Jmeno as Jmeno
                               ,2 as typ
                            FROM Sponzoring sp
                            LEFT Join ZkratkaStrany zs on sp.IcoPrijemce = zs.ICO
                            join Firma fi on sp.IcoDarce = fi.ICO
                            where IcoDarce is not null and sp.IcoPrijemce = {icoStrany}
                              and year(sp.DarovanoDne) >= {tenYearsBack}
                            group by zs.KratkyNazev, IcoPrijemce, Year(DarovanoDne), fi.ICO, fi.Jmeno")
                    .ToListAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="year">If null it returns sum for all years</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<SponzoringSummed>> BiggestPeopleSponsorsAsync(int? year, CancellationToken cancellationToken, int? take = null)
        {
            int rok = year ?? 0;
            int yearSwitch = year.HasValue ? 0 : 1;
            int tenYearsBack = DateTime.Now.Year - 10;

            using (DbEntities db = new DbEntities())
            {
                var request = db.SponzoringSummedView.FromSqlInterpolated(
                    $@"SELECT null as NazevStrany
                               ,null as IcoStrany
	                           ,SUM(Hodnota) as DarCelkem
	                           ,{rok} as Rok
	                           ,os.NameId as Id
	                           ,RTRIM(LTRIM(isnull(os.TitulPred,'') + ' ' + os.Jmeno + ' ' + os.Prijmeni + ' ' + isnull(os.TitulPo,''))) as Jmeno
                                ,1 as typ
                            FROM Sponzoring sp
                            join Osoba os on sp.OsobaIdDarce = os.InternalId
                            where (year(sp.DarovanoDne) = {rok} or 1={yearSwitch}) and OsobaIdDarce > 0
                              and year(sp.DarovanoDne) >= {tenYearsBack}
                            group by os.NameId, os.TitulPred, os.Jmeno, os.Prijmeni, os.TitulPo")
                    .OrderByDescending(x => x.DarCelkem);

                if (take != null)
                    return await request.Take(take.Value).ToListAsync(cancellationToken);

                return await request.ToListAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="year">If null it returns sum for all years</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<List<SponzoringSummed>> BiggestCompanySponsorsAsync(int? year, CancellationToken cancellationToken, int? take = null)
        {
            int rok = year ?? 0;
            int yearSwitch = year.HasValue ? 0 : 1;
            int tenYearsBack = DateTime.Now.Year - 10;

            using (DbEntities db = new DbEntities())
            {
                var request = db.SponzoringSummedView.FromSqlInterpolated(
                    $@"SELECT null as NazevStrany
                               ,null as IcoStrany
	                           ,SUM(Hodnota) as DarCelkem
	                           ,{rok} as Rok
	                           ,fi.ICO as Id
	                           ,fi.Jmeno as Jmeno
                               ,2 as typ
                            FROM Sponzoring sp
                            join Firma fi on sp.IcoDarce = fi.ICO
                            where (year(sp.DarovanoDne) = {rok} or 1={yearSwitch}) and IcoDarce is not null
                              and year(sp.DarovanoDne) >= {tenYearsBack}
                            group by fi.ICO, fi.Jmeno")
                    .OrderByDescending(x => x.DarCelkem); ;

                if (take != null)
                    return await request.Take(take.Value).ToListAsync(cancellationToken);

                return await request.ToListAsync(cancellationToken);

            }
        }
    }
}