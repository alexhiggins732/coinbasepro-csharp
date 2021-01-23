using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using CoinbaseUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils;
using Utils.Statistics;

namespace UtilsWinFormApp
{
    public partial class HPMTFMA : Form
    {

        public HPMTFMA()
        {
            InitializeComponent();
            this.FormClosing += HPMTFMA_FormClosing;
            init();
        }

        private void HPMTFMA_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisposeReader();
        }

        ProductType SelectedProductType => cmbProductType.SelectedIndex > -1 ? (ProductType)cmbProductType.SelectedItem : default(ProductType);
        CandleGranularity SelectedGranularity => cmbGran.SelectedIndex > -1 ? (CandleGranularity)cmbGran.SelectedItem : default(CandleGranularity);

        private void CmbGran_SelectedIndexChanged(object sender, EventArgs e)
        {
            //var min = CandleService.GetMinDbCandleDate(SelectedProductType, SelectedGranularity);
            //if (dtpFrom.Value < min)
            //    dtpFrom.Value = min;
            //AssuerLatestCandles();
            //restartReader();
        }

        private void CmbProductType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var min = CandleService.GetMinDbCandleDate(SelectedProductType, SelectedGranularity);
            if (dtpFrom.Value < min)
                dtpFrom.Value = min;
            AssuerLatestCandles();
            restartReader();
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


        private void btnUpdateDb_Click(object sender, EventArgs e)
        {
            AssuerLatestCandles(true);
            MessageBox.Show("Updated");

        }
        private void AssuerLatestCandles(bool force = false)
        {
            CandleService.UpdateCandles(SelectedProductType, force);
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
        int mode = 0;
        TimedReader rdr = null;


        void restartReader()
        {
            bool started = rdr != null && rdr.Running;
            DisposeReader();
            if (started)
                StartXOver();
        }

        private void btnGo_Click(object sender, EventArgs e)
        {



        }


        private void StopXOver()
        {
            rdr?.Stop();
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

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            rdr?.SetInterval((int)(1000 / this.numericUpDown1.Value));
        }

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
            DateTime? startDate = dtpFrom.Value == DateTime.MinValue ? null : (DateTime?)dtpFrom.Value.Date;
            if (startDate != null)
            {
                //startDate = startDate.Value.AddSeconds(-(256 * (int)candleGranularity));
            }

            rdr = new TimedReader(ms, productType, candleGranularity, startDate);

            if (this.cbGoldHistory.Checked)
            {
                int max = rdr.MTFMaStream.MTFMa.MASizes.Length;
                this.GoldHistory = new TwoDimensionalSma(max, max, max);
            }
             
            rdr.Tick += Rdr_Tick;
            rdr.Stopped += Rdr_Stopped;
            rdr.Start();

        }
        private TwoDimensionalSma GoldHistory;
        private void Rdr_Stopped(object sender, EventArgs e)
        {
            rdr.Stop();
            btnStart.Text = "Start";
        }

        private void Rdr_Tick(object sender, EventArgs e)
        {
            bool draw = false;
            switch (SelectedGranularity)
            {
                case CandleGranularity.Minutes1:
                    draw = true;
                    break;
                case CandleGranularity.Minutes5:
                    if (rdr.Current != null && rdr.Current.Time.Minute % 5 == 0)
                        draw = true;
                    break;
                case CandleGranularity.Minutes15:
                    if (rdr.Current != null && (rdr.Current.Time.Minute == 0 || rdr.Current.Time.Minute == 15 || rdr.Current.Time.Minute == 30 || rdr.Current.Time.Minute == 45))
                        draw = true;
                    break;
                case CandleGranularity.Hour1:
                    if (rdr.Current != null && rdr.Current.Time.Minute == 0)
                        draw = true;
                    break;
                case CandleGranularity.Hour6:
                    if (rdr.Current != null && rdr.Current.Time.Minute == 0 && (rdr.Current.Time.Hour == 0 || rdr.Current.Time.Hour == 6 || rdr.Current.Time.Hour == 12 || rdr.Current.Time.Hour == 18))
                        draw = true;
                    break;
                case CandleGranularity.Hour24:
                    if (rdr.Current != null && rdr.Current.Time.Minute == 0 && rdr.Current.Time.Hour == 0)
                        draw = true;
                    break;
            }
            if (GoldHistory != null)
            {
                var matrix = rdr.Matrix.Select(x => x.Select(y => y ? 1m : 0m).ToArray()).ToArray();
                GoldHistory?.AddSample(matrix);
            }


            if (draw)
                this.pictureBox1.Image = rdr.Bitmap;
            if (rdr.Current != null)
            {
                if (draw)
                {
                    this.txtClose.Text = rdr.Current.Close.Value.ToString("C2");
                    this.txtStart.Text = rdr.MTFMaStream.MTFMa.MovingAverages.Last().ToString("C2");
                    this.txtDate.Text = rdr.Current.Time.ToString("yyyy-MM-dd HH:mm");
                }
            }
            else
            {
                if (GoldHistory != null)
                {
                    var outBmp = new Bitmap(GoldHistory.width, GoldHistory.height);
                    var mtx = GoldHistory.Current();
                    for (var x = 0; x < GoldHistory.width; x++)
                    {
                        for (var y = 0; y < GoldHistory.height; y++)
                        {
                            var avg = mtx[y][x];
                            var ig = (int)(avg * 255);
                            var hex = ig.ToString("X2");
                            var color = Color.FromArgb(ig, ig, ig);
                            outBmp.SetPixel(x, y, color);

                        }
                    }
                    outBmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    outBmp.Save("GoldHistory.bmp", ImageFormat.Bmp);
                }
                rdr.Stop();
            }
        }

        public class TimedReader : IDisposable
        {

            public HighPrecisionMTFMaStream MTFMaStream;
            CandleDbReader candleStream;
            private IEnumerator<Candle> candleEnumerator;
            Timer timer;
            public Candle Current => candleEnumerator.Current;

            bool MoveNext() => MTFMaStream.MoveNext();
            //public bool[][] Matrix => MTFMaStream.GoldMatrix();
            public bool Running { get; private set; }
            public bool Completed { get; private set; }
            public event EventHandler Tick;
            public event EventHandler Stopped;
            public event EventHandler Started;
            public TimedReader(int interval, ProductType productType, CandleGranularity candleGranularity, DateTime? startDate)
            {

                MTFMaStream = new HighPrecisionMTFMaStream(startDate);

                //candleStream = new CandleDbReader(productType, CandleGranularity.Minutes1, startDate);
                this.candleEnumerator = MTFMaStream.CandleEnumerator;
                timer = new Timer();
                timer.Interval = interval;
                timer.Tick += Timer_Tick;

            }
            public bool Step()
            {
                bool result = MoveNext();
                if (!result)
                {
                    timer.Stop();
                    this.Completed = true;
                }

                Tick?.Invoke(this, null);
                return result;
            }
            public Bitmap Bitmap => MTFMaStream.GoldMatrix().ToBitmap();
            public bool[][] Matrix => MTFMaStream.GoldMatrix();
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
                if (MTFMaStream != null)
                {
                    MTFMaStream = null;
                }
            }

            internal void SetInterval(int interval)
            {
                this.timer.Interval = interval;
            }

        }
    }


}
