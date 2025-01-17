﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HlidacStatu.Repositories.Searching
{
    public class SplittingQuery
    {
        [DebuggerDisplay("{ToQueryString}")]
        public class Part
        {
            public string Prefix { get; set; } = "";
            public string Value { get; set; } = "";

            public bool ExactValue { get; set; } = false;

            public string ToQueryString
            {
                get
                {
                    return ExportPartAsQuery(true);
                }
            }

            public string ExportPartAsQuery(bool encode = true)
            {
                //force not to encode
                // encode = false;
                if (ExactValue)
                    return Value;
                else
                {
                    if (encode)
                        return (Prefix ?? "") + EncodedValue();
                    else
                    {
                        return (Prefix ?? "") + Value;
                    }
                }
            }


            static char[] reservedAll = new char[] { '+', '=', '!', '(', ')', '{', '}', '[', ']', '^', '\'', '~', '*', '?', ':', '\\', '/' };
            static char[] skipIfPrefix = new char[] { '-', '*', '?' };

            static char[] formulaStart = new char[] { '>', '<', '(', '{', '[' };
            static char[] formulaEnd = new char[] { ')', '}', ']', '*' };
            static char[] ignored = new char[] { '>', '<' };
            public string EncodedValue()
            {
                if (ExactValue)
                    return Value;
                if (string.IsNullOrWhiteSpace(Value))
                    return Value;

                //The reserved characters are:  + - = && || > < ! ( ) { } [ ] ^ " ~ * ? : \ /
                // https://www.elastic.co/guide/en/elasticsearch/reference/7.5/query-dsl-query-string-query.html
                //< and > can’t be escaped at all. The only way to prevent them from attempting to create a range query is to remove them from the query string entirely.


                var val = Value.Trim();
                List<char> sout = new List<char>();
                if (formulaStart.Contains(val[0]) && formulaEnd.Contains(val[val.Length - 1]))
                    return val;

                if (val.EndsWith("*"))
                    return val;

                //allow ~ or ~5 on the end of word
                //replace ~ with chr(254)
                val = System.Text.RegularExpressions.Regex.Replace(val, @"(?<w>\w*) ~ (?<n>\d{0,2})", "${w}" + Devmasters.Core.Chr(254) + "${n}", Util.Consts.DefaultRegexQueryOption);


                for (int i = 0; i < val.Length; i++)
                {
                    if (!string.IsNullOrEmpty(Prefix) && skipIfPrefix.Contains(val[i]))
                    {
                        sout.Add(val[i]);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(Prefix) && i == 0 && formulaStart.Contains(val[i]))
                    {
                        sout.Add(val[i]);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(Prefix) == false
                        && i == 0 && val.Length > 1
                        && formulaStart.Contains(val[i]) && val[i + 1] == '=')
                    {
                        sout.Add(val[i]);
                        sout.Add(val[i + 1]);
                        i = i + 1;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(Prefix) && i == val.Length - 1 && formulaEnd.Contains(val[i]))
                    {
                        sout.Add(val[i]);
                        continue;
                    }

                    if ((i > 0) && ignored.Contains(val[i]))
                        continue;

                    if ((i > 0 || i < val.Length - 1) && reservedAll.Contains(val[i]))
                    {
                        sout.Add('\\');
                        sout.Add(val[i]);
                    }
                    else
                        sout.Add(val[i]);
                }

                var ret = String.Join("", sout.Select(c =>
                {
                    switch (c)
                    {
                        case (char)254: return '~';
                        default:
                            return c;
                    }
                }));

                return ret;
            }

        }
        public static SplittingQuery SplitQuery(string query)
        {
            return new SplittingQuery(query);
        }
        private SplittingQuery(string query)
        {
            _parts = Split(query);
        }
        public SplittingQuery()
            : this(new Part[] { })
        {
        }
        public SplittingQuery(Part[] parts)
        {
            _parts = parts ?? new Part[] { };
        }


        public string FullQuery(bool encode = true)
        {
            if (_parts.Length == 0)
            {
                return "";
            }
            else
            {
                return _parts
                    .Select(m => m.ToQueryString.Trim())
                    .Where(m => m.Length > 0)
                    .Aggregate((f, s) => f + " " + s)
                    .Trim();
            }
        }
        Part[] _parts = null;
        public Part[] Parts { get { return _parts; } }

        public void AddParts(Part[] parts)
        {
            var p = new List<Part>(_parts);
            p.AddRange(parts);
            _parts = p.ToArray();
        }
        public void InsertParts(int index, Part[] parts)
        {
            var p = new List<Part>(_parts);
            p.InsertRange(index, parts);
            _parts = p.ToArray();
        }
        public void ReplaceWith(int index, Part[] parts)
        {

            var p = new List<Part>(_parts);
            p.RemoveAt(index);
            p.InsertRange(index, parts);
            _parts = p.ToArray();
        }


        private Part[] Split(string query)
        {
            List<Part> parts = new List<Part>();
            if (string.IsNullOrWhiteSpace(query))
                return parts.ToArray();

            List<Part> tmpParts = new List<Part>();
            //prvni rozdelit podle ""
            var fixTxts = Devmasters.TextUtil.SplitStringToPartsWithQuotes(query, '\"');


            //spojit a rozdelit podle mezer
            for (int i = 0; i < fixTxts.Count; i++)
            {
                //fixed string
                if (fixTxts[i].Item2)
                {
                    if (i == 0)
                    {
                        tmpParts.Add(new Part()
                        {
                            ExactValue = true,
                            Value = fixTxts[i].Item1
                        }
                        );
                    }
                    else if (i > 0 && fixTxts[i - 1].Item1.EndsWith(":"))
                    {
                        tmpParts[tmpParts.Count - 1].Prefix = tmpParts[tmpParts.Count - 1].Prefix;
                        tmpParts[tmpParts.Count - 1].Value = fixTxts[i].Item1;
                    }
                    else
                    {
                        tmpParts.Add(new Part()
                        {
                            ExactValue = true,
                            Value = fixTxts[i].Item1
                        }
                        );
                    }
                }
                else
                {
                    //string[] mezery = fixTxts[i].Item1.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string tPart = fixTxts[i].Item1;
                    tPart = tPart.Replace("(", " ( ").Replace(")", " ) ")
                        .Replace(": (", ":(");//fix mezera za :
                    string[] mezery = tPart.Split(new char[] { ' ' });

                    foreach (var mt in mezery)
                    {
                        string findPrefixReg = @"(^|\s|[(]) (?<p>(\w|\.)*:) (?<q>(-|\w)* )\s*";
                        var prefix = Devmasters.RegexUtil.GetRegexGroupValue(mt, findPrefixReg, "p");
                        if (!string.IsNullOrEmpty(prefix))
                            tmpParts.Add(new Part()
                            {
                                ExactValue = false,
                                Prefix = prefix,
                                Value = mt.Replace(prefix, "")
                            }
                            );
                        else
                            tmpParts.Add(new Part()
                            {
                                ExactValue = false,
                                Value = mt
                            }
                            );
                    }
                }
            }
            //check prefix with xxx:[ ... ]  or xxx:{   }

            for (int pi = 0; pi < tmpParts.Count; pi++)
            {
                var p = tmpParts[pi];
                if (p.ExactValue)
                    parts.Add(p);
                else
                {
                    if (!string.IsNullOrEmpty(p.Prefix) && (p.Value.StartsWith("{") || p.Value.StartsWith("[")))
                    {
                        //looking until end to the next with ] } and join it together
                        for (int pj = pi + 1; pj < tmpParts.Count; pj++)
                        {

                            if (tmpParts[pi].ExactValue == false
                                && (tmpParts[pj].Value.EndsWith("}") || tmpParts[pj].Value.EndsWith("]"))
                                )
                            {
                                parts.Add(new Part()
                                {
                                    Prefix = p.Prefix,
                                    ExactValue = p.ExactValue,
                                    Value = tmpParts.Skip(pi).Take(pj - pi + 1).Select(m => m.Value).Aggregate((f, s) => f + " " + s)
                                });
                                pi = pj;
                                goto NextPart;
                            }

                        }
                        //no end found
                        parts.Add(new Part()
                        {
                            Prefix = p.Prefix,
                            ExactValue = p.ExactValue,
                            Value = tmpParts.Skip(pi).Take(tmpParts.Count - pi + 1).Select(m => m.Value).Aggregate((f, s) => f + " " + s)
                        });
                        pi = tmpParts.Count;
                        NextPart:
                        continue;
                    }
                    else
                        parts.Add(p);

                }

            }


            return parts.Where(m => m.ToQueryString.Length > 0).ToArray();
        }


    }
}