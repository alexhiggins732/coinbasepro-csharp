using CoinbaseUtils;
using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Statistics;

namespace UtilsWinFormApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
            init();
        }

        private void init()
        {

            BindComboBoxToEnum<ProductType>(this.cmbProductType, ProductType.LtcUsd);
            cmbProductType.SelectedIndexChanged += CmbProductType_SelectedIndexChanged;

            BindComboBoxToEnum<CandleGranularity>(this.cmbGran, CandleGranularity.Minutes15);
            cmbGran.SelectedIndexChanged += CmbGran_SelectedIndexChanged;

            var min = CandleService.GetMinDbCandleDate(SelectedProductType, SelectedGranularity);
            this.dtpFrom.Value = min;
        }

        ProductType SelectedProductType => cmbProductType.SelectedIndex > -1 ? (ProductType)cmbProductType.SelectedItem : default(ProductType);
        CandleGranularity SelectedGranularity => cmbGran.SelectedIndex > -1 ? (CandleGranularity)cmbGran.SelectedItem : default(CandleGranularity);

        private void CmbGran_SelectedIndexChanged(object sender, EventArgs e)
        {
            var min = CandleService.GetMinDbCandleDate(SelectedProductType, SelectedGranularity);
            if (dtpFrom.Value < min)
                dtpFrom.Value = min;
            AssuerLatestCandles();
            restartReader();
        }

        private void CmbProductType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var min = CandleService.GetMinDbCandleDate(SelectedProductType, SelectedGranularity);
            if (dtpFrom.Value < min)
                dtpFrom.Value = min;
            AssuerLatestCandles();
            restartReader();
        }

        private void AssuerLatestCandles(bool force = false)
        {
            CandleService.UpdateCandles(SelectedProductType, force);
        }

        void restartReader()
        {
            bool started = rdr != null && rdr.Running;
            DisposeReader();
            if (started)
                StartXOver();
        }

        void BindComboBoxToEnum<T>(ComboBox comboBox, T selectedValue)
        {
            var names = Enum.GetValues(typeof(T)).ToList<T>();
            comboBox.Items.Clear();
            names.ForEach(x => comboBox.Items.Add(x));
            comboBox.SelectedIndex = 0;

            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (((T)comboBox.Items[i]).Equals(selectedValue))
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        void DisposeReader()
        {
            try
            {
                if (rdr != null)
                {
                    rdr.Tick -= Rdr_Tick;
                    rdr.Stop();
                    rdr.Dispose();
                    rdr = null;
                }
            }
            catch { }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisposeReader();
        }

        int mode = 0;
        private void btnGo_Click(object sender, EventArgs e)
        {
            btnGo.Enabled = false;
            if (mode == 0)
            {
                TestXOverGold();
                mode = 1;
            }
            else
            {
                mode = 0;
                TestXOverBlack();
            }
            btnGo.Enabled = true;
        }

        public void TestXOverGold()
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
            this.pictureBox1.Image = last.ToBitmap();

        }
        public void TestXOverBlack()
        {
            int maxMa = 256;

            var mtf = new MultiTimeFrameSma<decimal>(maxMa);
            var crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);

            bool[][] current = crossOver.GetMatrix();

            var sample = 100m;
            var inc = 0.1m;
            for (var i = 0; i < 256; i++, sample -= inc)
            {
                crossOver.AddSample(sample);
            }

            current = crossOver.GetMatrix();
            var matrixString = current.ToBitString();
            bool[][] last = current.CloneDeep();
            this.pictureBox1.Image = last.ToBitmap();

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (btnStart.Text == "Start")
            {
                btnStart.Text = "Stop";
                StartXOver();
            }
            else
            {
                btnStart.Text = "Start";
                StopXOver();
            }
        }

        private void StopXOver()
        {
            rdr?.Stop();
        }

        TimedReader rdr;
        private void StartXOver()
        {
            if (rdr != null && rdr.Running == false)
            {
                rdr.Start();
                return;
            }
            DisposeReader();

            int ms = (int)(1000 / this.numericUpDown1.Value);
            var productType = (ProductType)cmbProductType.Items[cmbProductType.SelectedIndex];
            var candleGranularity = (CandleGranularity)cmbGran.Items[cmbGran.SelectedIndex];
            DateTime? startDate = dtpFrom.Value == DateTime.MinValue ? null : (DateTime?)dtpFrom.Value;
            if (startDate != null)
            {
                startDate = startDate.Value.AddSeconds(-(256 * (int)candleGranularity));
            }
            rdr = new TimedReader(256, ms, productType, candleGranularity, startDate);
            rdr.Tick += Rdr_Tick;
            rdr.Stopped += Rdr_Stopped;
            rdr.Start();
            btnStart.Text = "Stop";
        }

        private void Rdr_Stopped(object sender, EventArgs e)
        {
            rdr.Stop();
            btnStart.Text = "Start";
        }

        private void Rdr_Tick(object sender, EventArgs e)
        {

            this.pictureBox1.Image = rdr.Bitmap;
            if (rdr.Current != null)
            {
                this.txtClose.Text = rdr.Current.Close.Value.ToString("C2");
                this.txtStart.Text = rdr.SimpleMovingAverages.Last().Value.Average.ToString("C2");
                this.txtDate.Text = rdr.Current.Time.ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                rdr.Stop();
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            rdr?.SetInterval((int)(1000 / this.numericUpDown1.Value));
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            rdr.Step();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (rdr != null && !rdr.Running)
            {
                DisposeReader();
                btnStart.Text = "Stop";
                //rdr.Dispose();
            }
            if (btnStart.Text == "Stop")
                StopXOver();
            btnStart.Text = "Stop";
            StartXOver();
        }

        private void btnUpdateDb_Click(object sender, EventArgs e)
        {
            //MaxProfitAnalyzer.Run();

            AssuerLatestCandles(true);
            MessageBox.Show("Updated");
        }

        private void btnOpenHPMTFMA_Click(object sender, EventArgs e)
        {
            var frm = new HPMTFMA();
            frm.Show();
        }
    }

    public class TimedReader : IDisposable
    {
        public int MaxMa;
        MultiTimeFrameSma<decimal> mtf;
        MultiTimeFrameCrossOver<decimal> crossOver;
        CandleDbReader candleStream;
        private IEnumerator<Candle> candleEnumerator;
        Timer timer;
        public Candle Current => candleEnumerator.Current;
        public decimal[] History => crossOver.History;
        public MultiTimeFrameSma<decimal> MtfSma => crossOver.SimpleMovingAverages;
        public Dictionary<int, SmaBase<decimal>> SimpleMovingAverages => MtfSma.SimpleMovingAverages;
        public decimal[] Averages => crossOver.SimpleMovingAverages.Averages;
        bool MoveNext() => candleEnumerator.MoveNext();
        public bool[][] Matrix => crossOver.GetMatrix();
        public bool Running { get; private set; }
        public bool Completed { get; private set; }
        public event EventHandler Tick;
        public event EventHandler Stopped;
        public event EventHandler Started;
        public TimedReader(int maxMa, int interval, ProductType productType, CandleGranularity candleGranularity, DateTime? startDate)
        {
            this.MaxMa = maxMa;
            mtf = new MultiTimeFrameSma<decimal>(maxMa);
            crossOver = new MultiTimeFrameCrossOver<decimal>(mtf);
            candleStream = new CandleDbReader(productType, candleGranularity, startDate);
            this.candleEnumerator = candleStream.GetEnumerator();
            timer = new Timer();
            timer.Interval = interval;
            timer.Tick += Timer_Tick;
            for (var i = 0; i < maxMa; i++)
            {
                if (MoveNext())
                {
                    crossOver.AddSample(Current.Close.Value);
                }
            }
        }
        public bool Step()
        {
            bool result = MoveNext();
            if (result)
            {
                crossOver.AddSample(Current.Close.Value);
            }
            else
            {
                timer.Stop();
                this.Completed = true;
            }

            Tick?.Invoke(this, null);
            return result;
        }
        public Bitmap Bitmap => Matrix.ToBitmap();

        public void Start()
        {
            if (Running) return;
            this.Running = true;
            timer.Start();

            Started?.Invoke(this, null);
        }
        public void Stop()
        {
            if (!Running) return;
            this.Running = false;
            timer.Stop();
            if (Current != null)
                Console.WriteLine($"Stopped at {Current.Time}");

            Stopped?.Invoke(this, null);
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            Step();
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }
            if (candleEnumerator != null)
            {
                candleEnumerator.Dispose();
                candleEnumerator = null;
            }
            if (candleStream != null)
            {
                candleStream = null;
            }
            if (mtf != null)
            {
                mtf = null;
            }
            if (crossOver != null)
            {
                crossOver = null;
            }

        }

        internal void SetInterval(int interval)
        {
            this.timer.Interval = interval;
        }
    }
    public static class MatrixExtensions
    {
        public static Bitmap ToBitmap(this bool[][] source)
        {
            var w = source.Length;
            var h = source[0].Length;
            var result = new Bitmap(w, h);
            for (var x = 0; x < h; x++)
            {
                for (var y = 0; y < w; y++)
                {
                    result.SetPixel(w - x - 1, y, source[x][y] ? Color.Gold : Color.Black);
                }
            }
            return result;
        }

        public static List<T> ToList<T>(this Array array)
        {
            var result = new List<T>();
            foreach (T item in array) { result.Add(item); }
            return result;
        }
    }
}
