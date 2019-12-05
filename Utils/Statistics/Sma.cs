using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using CoinbaseUtils;
using CoinbasePro.Shared.Types;
using CoinbasePro.Services.Products.Types;

namespace Utils.Statistics
{
    public class SmaTests
    {
        public static void RunTests()
        {
            RunDecimalSmaTests();
            RunCandleStreamCrossOverTests();
            RunCandleStreamCrossOverAgentTests();
        }

        public static void RunCrossoverTests()
        {

            var mtf = new MultiTimeFrameSma<decimal>(2);
            var crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);
            var matrix = crossOver.GetMatrix();
            Debug.Assert(matrix[0][0] == true);
            Debug.Assert(matrix[0][1] == true);
            Debug.Assert(matrix[1][0] == true);
            Debug.Assert(matrix[1][1] == true);

            crossOver.AddSample(1);
            matrix = crossOver.GetMatrix();
            var averages = mtf.Averages;
            Debug.Assert(matrix[0][0] == true);
            Debug.Assert(matrix[0][1] == true);
            Debug.Assert(matrix[1][0] == false);
            Debug.Assert(matrix[1][1] == true);
            crossOver.AddSample(1);
            matrix = crossOver.GetMatrix();

            Debug.Assert(matrix[0][0] == true);
            Debug.Assert(matrix[0][1] == true);
            Debug.Assert(matrix[1][0] == true);
            Debug.Assert(matrix[1][1] == true);

        }

        public static void RunCandleStreamCrossOverAgentTests()
        {
            int maxMa = 256;
            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1);
            var mtf = new MultiTimeFrameSma<decimal>(maxMa);
            var crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);
            var agents = mtf.SimpleMovingAverageIndexes.SelectMany(kvp =>
            {
                var s = mtf.SimpleMovingAverageIndexes.Select(other =>
                {
                    return new CrossOverAgent<decimal>(mtf.SimpleMovingAverageIndexes[kvp.Key], mtf.SimpleMovingAverageIndexes[other.Key], crossOver);
                });
                return s.ToArray();
            }).ToList();

            var enumerator = candleStream.GetEnumerator();
            while (mtf.TotalSamples < maxMa && enumerator.MoveNext())
            {
                crossOver.AddSample(enumerator.Current.Close.Value);
                agents.ForEach(agent => agent.Sample());
            }
            var matrix = crossOver.GetMatrix();

        }


        public static void RunCandleStreamCrossOverTests()
        {
            int maxMa = 256;

            var mtf = new MultiTimeFrameSma<decimal>(maxMa);
            var crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);

            bool[][] current = crossOver.GetMatrix();

            var sample = 0.1m;
            var inc = 0.1m;
            for (var i = 0; i < 256; i++, sample += inc)
            {
                crossOver.AddSample(sample);
            }

            current = crossOver.GetMatrix();
            var matrixString = current.ToBitString();
            bool[][] last = current.CloneDeep();

            Func<bool, bool, int> comparer = (a, b) => (a == b ? 0 : (a ? 1 : -1));





            List<MatrixElementComparison> changes = Matrix.Compare(last, current, comparer);
            Debug.Assert(changes.Count == 0);



            last.CopyFrom(current);
            crossOver.AddSample(1);
            current = crossOver.GetMatrix();

            changes = Matrix.Compare(last, current, comparer);

            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1);
            var enumerator = candleStream.GetEnumerator();
            while (mtf.TotalSamples < maxMa && enumerator.MoveNext())
            {
                last.CopyFrom(current);

                crossOver.AddSample(enumerator.Current.Close.Value);
                current = crossOver.GetMatrix();

                changes = Matrix.Compare(last, current, comparer);
                var changeDtos = changes.Select(change => new CrossoverDto
                {
                    Ma1 = mtf.SimpleMovingAverageKeys[change.RowIndex],
                    Ma2 = mtf.SimpleMovingAverageKeys[change.ColumnIndex],
                    Gold = change.Comparison > 0,
                    CrossoverDate = enumerator.Current.Time,
                });

            }
            var matrix = crossOver.GetMatrix();
        }

        public static void RunCandleStreamMtfTests()
        {
            var candleStream = new CandleDbReader(ProductType.LtcUsd, CandleGranularity.Minutes1);
            var mtf = new MultiTimeFrameSma<decimal>(256);
            foreach (var candle in candleStream)
            {
                mtf.AddSample(candle.Close.Value);
                if (mtf.TotalSamples == 256)
                {
                    string bp = "";
                }
            }

        }
        public static void RunDecimalSmaTests()
        {
            var ma = new SmaOfDecimal(1);
            Debug.Assert(ma.Average == 0);
            ma.AddSample(1);
            Debug.Assert(ma.Average == 1);
            ma.AddSample(.5);
            Debug.Assert(ma.Average == .5m);

            ma = new SmaOfDecimal(2);
            Debug.Assert(ma.Average == 0);
            ma.AddSample(1);
            Debug.Assert(ma.Average == .5m);
            ma.AddSample(1);
            Debug.Assert(ma.Average == 1);
            ma.AddSample(1);
            Debug.Assert(ma.Average == 1);

            var mtfSma = new MultiTimeFrameSma<decimal>(2);
            Debug.Assert(mtfSma.Averages.All(x => x == 0));
            mtfSma.AddSample(1);
            Debug.Assert(mtfSma.Averages[0] == 1);
            Debug.Assert(mtfSma.Averages[1] == .5m);

            mtfSma.AddSample(1);
            Debug.Assert(mtfSma.Averages[0] == 1);
            Debug.Assert(mtfSma.Averages[1] == 1);

            mtfSma.AddSample(.5m);
            Debug.Assert(mtfSma.Averages[0] == .5m);
            Debug.Assert(mtfSma.Averages[1] == .75m);



        }
    }

    public class CrossoverDto
    {
        public int Ma1 { get; set; }
        public int Ma2 { get; set; }
        public bool Gold { get; set; }
        public DateTime CrossoverDate { get; set; }
    }

    public static class MatrixExt
    {
        public static T[][] CloneDeep<T>(this T[][] source)
        {
            return source.Select(x => x.ToArray()).ToArray();
        }

        public static void CopyFrom<T>(this T[][] dest, T[][] source)
        {
            for (var i = 0; i < dest.Length; i++)
            {
                Array.Copy(source[i], dest[i], source[i].Length);
            }
        }
        public static string ToBitString(this bool[][] source)
        {
            return string.Join("\r\n", source.Select(row => string.Join("", row.Select(x => x ? "1" : "0"))));
        }
    }

    public struct MatrixElementComparison
    {
        public MatrixElementComparison(int rowIndex, int columnIndex, int comparison) : this()
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            Comparison = comparison;
        }

        public int RowIndex { get; set; }
        public int ColumnIndex { get; set; }
        public int Comparison { get; set; }

        public override string ToString()
        {
            return $"{{{RowIndex}:{ColumnIndex}}} = {Comparison}";
        }
    }
    public class Matrix
    {
        public static List<MatrixElementComparison> Compare<T>(T[][] matrixA, T[][] matrixB, Func<T, T, int> comparer = null)
        {
            if (comparer == null && typeof(T).IsAssignableFrom(typeof(IComparable)))
            {
                comparer = (a, b) => ((IComparable)a).CompareTo(b);
            }
            var rowIndex = 0;
            var colLength = matrixA[rowIndex].Length;
            if (matrixA.Length != matrixB.Length ||
                matrixA.Any(row => matrixA[rowIndex].Length != colLength || matrixB[rowIndex++].Length != colLength))
            {
                throw new ArgumentException("Matrices must be rectangular and of the same size");
            }
            if (comparer == null)
            {
                throw new Exception("Failed to find a built in comparer. Comparer must be specified");
            }
            var result = new List<MatrixElementComparison>();
            for (rowIndex = 0; rowIndex < matrixA.Length; rowIndex++)
            {
                for (var colIndex = 0; colIndex < colLength; colIndex++)
                {
                    int comp = comparer(matrixA[rowIndex][colIndex], matrixB[rowIndex][colIndex]);
                    if (comp != 0)
                    {
                        result.Add(new MatrixElementComparison(rowIndex, colIndex, comp));
                    }
                }
            }
            return result;
        }
    }
    public class TradeDto<TNumeric>
        where TNumeric : IConvertible
    {
        public TNumeric BuyPrice;
        public TNumeric SellPrice;
    }
    public class CrossOverAgent<TNumeric>
        where TNumeric : IConvertible
    {
        public int MaIndex;
        public int OtherIndex;
        public bool InUsd;
        public List<TradeDto<TNumeric>> Trades;
        private INumericOperationsProvider<TNumeric> OperationsProvider;
        private TNumeric zero;
        private MultiTimeFrameCrossOver<TNumeric> Crossover;
        public CrossOverAgent(int maIndex, int otherMaxIndex, MultiTimeFrameCrossOver<TNumeric> mtfCrossover, INumericOperationsProvider<TNumeric> operationsProvider = null)
        {
            this.MaIndex = maIndex;
            this.OtherIndex = otherMaxIndex;
            this.InUsd = false;
            this.Trades = new List<TradeDto<TNumeric>>();
            OperationsProvider = operationsProvider ?? OperationsProviderFactory.GetProvider<TNumeric>();
            zero = OperationsProvider.ToNumeric(0);
            Crossover = mtfCrossover;
        }


        public void Sample()
        {
            var isGold = Crossover.GetMatrix()[MaIndex][OtherIndex];
            if (isGold)
            {
                if (InUsd)
                {
                    Buy(Crossover.LastSample);
                }
            }
            else
            {
                if (!InUsd)
                {
                    Sell(Crossover.LastSample);
                }
            }
        }

        private void Sell(TNumeric lastSample)
        {
            if (Trades.Count > 0)
            {
                var last = Trades.Last();
                if (OperationsProvider.IsEqual(last.SellPrice, zero))
                {
                    last.SellPrice = lastSample;
                }

            }
            InUsd = true;
        }

        private void Buy(TNumeric lastSample)
        {
            var dto = new TradeDto<TNumeric> { BuyPrice = lastSample };
            Trades.Add(dto);
            InUsd = false;
        }
    }

    public interface INumericOperationsProvider<TNumeric>
        where TNumeric : IConvertible
    {
        TNumeric Divide(TNumeric dividend, TNumeric divisor);
        TNumeric Multiply(TNumeric multiplicand, TNumeric multiplier);
        TNumeric Add(TNumeric operandA, TNumeric operandB);
        TNumeric Substract(TNumeric operandA, TNumeric operandB);

        bool IsLessThan(TNumeric operandA, TNumeric operandB);
        bool IsLessThanOrEqual(TNumeric operandA, TNumeric operandB);
        bool IsEqual(TNumeric operandA, TNumeric operandB);
        bool IsGreaterThanOrEqual(TNumeric operandA, TNumeric operandB);
        bool IsGreaterThan(TNumeric operandA, TNumeric operandB);

        TNumeric ToNumeric(sbyte value);
        TNumeric ToNumeric(short value);
        TNumeric ToNumeric(int value);
        TNumeric ToNumeric(long value);
        TNumeric ToNumeric(byte value);
        TNumeric ToNumeric(ushort value);
        TNumeric ToNumeric(uint value);
        TNumeric ToNumeric(ulong value);
        TNumeric ToNumeric(float value);
        TNumeric ToNumeric(double value);
        TNumeric ToNumeric(decimal value);
        TNumeric ToNumeric(IConvertible value);
    }



    public abstract class OperationsProviderBase<TNumeric>
        : INumericOperationsProvider<TNumeric>
        where TNumeric : IConvertible
    {

        private static Type Type = typeof(TNumeric);
        public abstract TNumeric Divide(TNumeric dividend, TNumeric divisor);
        public abstract TNumeric Multiply(TNumeric multiplicand, TNumeric multiplier);
        public abstract TNumeric Add(TNumeric operandA, TNumeric operandB);
        public abstract TNumeric Substract(TNumeric operandA, TNumeric operandB);



        public TNumeric ToNumeric(sbyte value) => (TNumeric)Convert.ChangeType(value, Type);
        public TNumeric ToNumeric(short value) => (TNumeric)Convert.ChangeType(value, Type);
        public TNumeric ToNumeric(int value) => (TNumeric)Convert.ChangeType(value, Type);
        public TNumeric ToNumeric(long value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(byte value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(ushort value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(uint value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(ulong value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(float value) => (TNumeric)Convert.ChangeType(value, Type);

        public TNumeric ToNumeric(double value) => (TNumeric)Convert.ChangeType(value, Type);


        public TNumeric ToNumeric(decimal value) => (TNumeric)Convert.ChangeType(value, Type);
        public TNumeric ToNumeric(IConvertible value) => (TNumeric)Convert.ChangeType(value, Type);




        public bool IsLessThan(TNumeric operandA, TNumeric operandB)
            => ((IComparable<TNumeric>)operandA).CompareTo(operandB) < 0;

        public bool IsLessThanOrEqual(TNumeric operandA, TNumeric operandB)
            => ((IComparable<TNumeric>)operandA).CompareTo(operandB) <= 0;

        public bool IsEqual(TNumeric operandA, TNumeric operandB)
            => ((IComparable<TNumeric>)operandA).CompareTo(operandB) == 0;

        public bool IsGreaterThanOrEqual(TNumeric operandA, TNumeric operandB)
            => ((IComparable<TNumeric>)operandA).CompareTo(operandB) >= 0;

        public bool IsGreaterThan(TNumeric operandA, TNumeric operandB)
            => ((IComparable<TNumeric>)operandA).CompareTo(operandB) > 0;
    }
    public class OperationsProviderFactory
    {
        public static OperationsProviderBase<TNumeric> GetProvider<TNumeric>()
            where TNumeric : IConvertible
        {
            var name = typeof(TNumeric).Name;
            switch (name)
            {
                case nameof(Decimal):
                    return new DecimalOperationsProvider() as OperationsProviderBase<TNumeric>;
                case nameof(Single):
                    return new FloatOperationsProvider() as OperationsProviderBase<TNumeric>;
                case nameof(Double):
                    return new DoubleOperationsProvider() as OperationsProviderBase<TNumeric>;
                default:
                    throw new NotImplementedException();
            }
        }
    }
    public class DecimalOperationsProvider : OperationsProviderBase<decimal>
    {
        public override decimal Add(decimal a, decimal b)
            => a + b;

        public override decimal Divide(decimal dividend, decimal divisor)
            => dividend / divisor;


        public override decimal Multiply(decimal multiplicand, decimal multiplier)
            => multiplicand * multiplier;

        public override decimal Substract(decimal a, decimal b)
           => a - b;
    }
    public class FloatOperationsProvider : OperationsProviderBase<float>
    {
        public override float Add(float a, float b)
            => a + b;

        public override float Divide(float dividend, float divisor)
            => dividend / divisor;


        public override float Multiply(float multiplicand, float multiplier)
            => multiplicand * multiplier;

        public override float Substract(float a, float b)
           => a - b;
    }

    public class DoubleOperationsProvider : OperationsProviderBase<double>
    {
        public override double Add(double a, double b)
            => a + b;

        public override double Divide(double dividend, double divisor)
            => dividend / divisor;


        public override double Multiply(double multiplicand, double multiplier)
            => multiplicand * multiplier;

        public override double Substract(double a, double b)
           => a - b;
    }

    public interface ISma<TNumeric>
    {
        int Count { get; }
        void AddSample(TNumeric sample);
        void AddSample(IConvertible sample);
        TNumeric Average { get; }
        List<TNumeric> History { get; }
    }

    public class SmaBase<T> : ISma<T>
        where T : IConvertible
    {
        public int Count { get; private set; }

        public List<T> History { get; private set; }
        public T Average { get; private set; } = default(T);
        public INumericOperationsProvider<T> OperationsProvider { get; private set; }
        public T SampleRatio { get; private set; }
        public SmaBase(int count, INumericOperationsProvider<T> operationsProvider = null)
        {
            if (operationsProvider == null)
                operationsProvider = OperationsProviderFactory.GetProvider<T>();
            this.Count = count;
            this.History = new List<T>();
            this.OperationsProvider = operationsProvider;
            SampleRatio = OperationsProvider.Divide(OperationsProvider.ToNumeric(1), OperationsProvider.ToNumeric(count));
        }

        public void AddSample(T sample)
        {
            T sampleValue = OperationsProvider.Multiply(SampleRatio, sample);

            History.Add(sampleValue);
            Average = OperationsProvider.Add(Average, sampleValue);
            if (History.Count > Count)
            {
                Average = OperationsProvider.Substract(Average, History[0]);
                History.RemoveAt(0);
            }

        }


        public void AddSample(IConvertible sample)
            => AddSample(OperationsProvider.ToNumeric(sample));

    }
    public class SmaOfDecimal : SmaBase<decimal>
    {

        public SmaOfDecimal(int count) : base(count)
        {

        }
    }

    public class MultiTimeFrameSma<TNumeric>
        where TNumeric : IConvertible
    {
        public Dictionary<int, SmaBase<TNumeric>> SimpleMovingAverages;
        public Dictionary<int, int> SimpleMovingAverageIndexes;
        public int[] SimpleMovingAverageKeys;
        private List<Action<TNumeric>> SampleActions;
        public TNumeric[] Averages;
        public int TotalSamples = 0;
        public TNumeric LastSample;

        public List<TNumeric> History { get; private set; }
        public int MaxSampleLength { get; private set; }
        public MultiTimeFrameSma(int maximumMovingAverage) : this(Enumerable.Range(1, maximumMovingAverage))
        {

        }

        public MultiTimeFrameSma(IEnumerable<int> movingAverageSizes)
        {
            SimpleMovingAverages = new Dictionary<int, SmaBase<TNumeric>>();
            SimpleMovingAverageIndexes = new Dictionary<int, int>();
            SimpleMovingAverageKeys = movingAverageSizes.ToArray();
            History = new List<TNumeric>();
            MaxSampleLength = SimpleMovingAverageKeys.Max(x => x);
            this.SampleActions = new List<Action<TNumeric>>();
            var averages = new List<TNumeric>();
            int i = 0;
            foreach (var smaSize in movingAverageSizes.OrderBy(x => x))
            {
                var sma = new SmaBase<TNumeric>(smaSize);
                SampleActions.Add((x) => { sma.AddSample(x); Averages[SimpleMovingAverageIndexes[sma.Count]] = sma.Average; });
                SimpleMovingAverages.Add(smaSize, sma);
                SimpleMovingAverageIndexes.Add(smaSize, i++);
                averages.Add(sma.Average);
            }
            this.Averages = averages.ToArray();
        }
        public void AddSample(TNumeric value)
        {
            History.Add(value);
            if (History.Count > MaxSampleLength)
                History.RemoveAt(0);
            LastSample = value;
            SampleActions.ForEach(action => action(value));
            TotalSamples++;
        }

    }

    public class MultiTimeFrameCrossOver<TNumeric>
        where TNumeric : IConvertible
    {
        public MultiTimeFrameSma<TNumeric> SimpleMovingAverages { get; }
        public List<TNumeric> History => SimpleMovingAverages.History;
        public TNumeric[] Averages => SimpleMovingAverages.Averages;
        public int TotalSamples => SimpleMovingAverages.TotalSamples;
        public TNumeric LastSample => SimpleMovingAverages.LastSample;
        private bool[][] matrix;
        public MultiTimeFrameCrossOver(MultiTimeFrameSma<TNumeric> simpleMovingAverages)
        {
            this.SimpleMovingAverages = simpleMovingAverages;
            int length = this.SimpleMovingAverages.Averages.Length;
            this.matrix = SimpleMovingAverages.Averages.Select(avg => SimpleMovingAverages.Averages.Select(x => true).ToArray()).ToArray();

        }
        public void AddSample(TNumeric value)
        {
            SimpleMovingAverages.AddSample(value);
            int max = SimpleMovingAverages.Averages.Length;

            for (var maIndex = 0; maIndex < max; maIndex++)
            {
                IComparable<TNumeric> ma = (IComparable<TNumeric>)SimpleMovingAverages.Averages[maIndex];
                var row = matrix[maIndex];
                for (var otherIndex = 0; otherIndex < max; otherIndex++)
                {
                    row[otherIndex] = ma.CompareTo(SimpleMovingAverages.Averages[otherIndex]) >= 0;
                }
            }
        }

        public bool[][] GetMatrix() => matrix;

    }
}
