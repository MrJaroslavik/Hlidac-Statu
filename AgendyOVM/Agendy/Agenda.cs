﻿using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgendyOVM.Agendy
{
    //from https://app.quicktype.io

    // <auto-generated />
    //
    // To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
    //
    //    using AgendyRaw;
    //
    //    var coordinate = Coordinate.FromJson(jsonString);

    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Agenda
    {
        internal static class Converter
        {
            public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
            };
        }

        public static Agenda FromJson(string json) => JsonConvert.DeserializeObject<Agenda>(json, Agenda.Converter.Settings);

        public OznaceniType[] HlavniUstanovení { get; set; }

        public string Id { get; set; }
        public string AgendaId { get; set; }

        public string Kod { get; set; }

        public string Nazev { get; set; }

        public string OhlasovatelIco { get; set; }

        public DateTime? PlatnostDo { get; set; }

        public DateTime? PlatnostOd { get; set; }

        public DateTime? PosledníZměna { get; set; }

        public string Type { get; set; }

        public OznaceniType[] Ustanoveni { get; set; }


        public string[] VykonavajiciKategorieOvm { get; set; }

        [Description("Vykonávajici kategori soukromoprávních uživatelů údajů")]
        public string[] VykonavajiciKategorieSpuu { get; set; }

        public string[] VykonavajiciOvm { get; set; }

        [Description("Vykonávajici soukromoprávní uživatelé údajů")]
        public string[] VykonavajiciSpuu { get; set; }

        public DateTime? Vznik { get; set; }

        public DateTime? Zanik { get; set; }

        public Cinnosti[] Cinnosti { get; set; }
    }



    public partial class OznaceniType
    {
        public string Oznaceni { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }
    }

    public partial class Cinnosti
    {
        public string Id { get; set; }
        public string CinnostId { get; set; }

        public string KodCinnosti { get; set; }

        public string NazevCinnosti { get; set; }

        public DateTime? PlatnostCinnostiDo { get; set; }

        public DateTime? PlatnostCinnostiOd { get; set; }

        public string PopisCinnosti { get; set; }

        public string TypCinnosti { get; set; }

        public string Type { get; set; }
    }


    public static class Serialize
    {
        public static string ToJson(this Agenda self) => JsonConvert.SerializeObject(self, Agenda.Converter.Settings);
    }

}
