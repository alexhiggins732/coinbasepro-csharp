using CoinbasePro.Services.Products.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UtilsWinFormApp
{
    public struct GranPair
    {
        public int SampleSize;
        public CandleGranularity Gran;
        public GranPair(int sampleSize, CandleGranularity gran)
        {
            this.SampleSize = sampleSize;
            this.Gran = gran;
        }
        public override string ToString()
        {
            string gn = "";
            switch(Gran)
            {
                case CandleGranularity.Hour1: gn = "1 Hour"; break;
                case CandleGranularity.Hour6: gn = "6 Hour"; break;
                case CandleGranularity.Hour24: gn = "1 Day"; break;
                case CandleGranularity.Minutes1: gn = "1 minute"; break;
                case CandleGranularity.Minutes15: gn = "15 minute"; break;
                case CandleGranularity.Minutes5: gn = "5 minute"; break;
            }
            return $"{gn} - {SampleSize} MA";
        }
    }
    public class MAMLMatrix
    {
        public MAMLMatrix()
        {
            var grans = new int[] { 1, 5, 15, 30, 60, 120, 240, 360, 1440 };
            var minutes = new List<int>();
            for (var i = 1; i <= 256; i++)
            {
                foreach (var gn in grans)
                {
                    minutes.Add(gn * i);
                }
            }
            minutes = minutes.Distinct().OrderBy(x=> x).ToList();
            var cg = Enum.GetValues(typeof(CandleGranularity)).Cast<CandleGranularity>().ToList();
            var d = new Dictionary<int, List<GranPair>>();
            for (var i = 1; i <= 256; i++)
            {
                foreach (var gn in cg)
                {
                    var gni = (int)gn/60;
                    var m = gni * i;
                    var pair = new GranPair(i, gn);
                    if (d.ContainsKey(m))
                    {
                        d[m].Add(pair);
                    }
                    else
                    {
                        d.Add(m, new List<GranPair> { pair });
                    }
                    //minutes.Add(gn * i);
                }
            }

            var ordered = d.OrderByDescending(x => x.Value.Count).ThenBy(x=> x.Value.Sum(g=> (int)g.Gran)).ThenBy(x=> x.Value.Sum(g=> g.SampleSize))
                .ToList();


            
        }
    }
}
