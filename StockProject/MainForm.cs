using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace StockProject
{
    public partial class lblCost : Form
    {

        #region Private constants
        private const string URL = "http://finance.yahoo.com/webservice/v1/symbols/";
        private const string URLParam = @"/quote?format=json&view=detail";

        private const string chartURL = "http://chart.finance.yahoo.com/z?s=";
        private const string chartURLParam = @"&t=6m&q=l&l=on&z=s&p=m50,m200";

        private const string newsURL = "https://feeds.finance.yahoo.com/rss/2.0/headline?s=";
        private const string newsURLParam = "&region=US&lang=en-US";

        private static Random num = new Random(1);

        #endregion

        #region Private Fields
        private string[] chartDayURL = {"http://chart.finance.yahoo.com/b?s=","http://chart.finance.yahoo.com/w?s=",
                                             "http://chart.finance.yahoo.com/c/3m/","http://chart.finance.yahoo.com/c/6m/",
                                              "http://chart.finance.yahoo.com/c/1y/"," http://chart.finance.yahoo.com/c/2y/",
                                             "http://chart.finance.yahoo.com/c/5y/","http://chart.finance.yahoo.com/c/my/"};

        private BindingList<StockSticker> StockList = new BindingList<StockSticker>();
        private BindingList<PortFolio> PortFolioList = new BindingList<PortFolio>();

        private BackgroundWorker bwasyncUpdate;
        private System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        #endregion

        #region Private Static Fields
        private static Mutex lockBw = new Mutex();
        private static object lockThread = new object();
        #endregion

        #region Public enums
        public enum Gradients
        {
            Red, Green, Grey, Blue, SlateGray, Orange
        }

        #endregion

        #region Constructors

        public lblCost()
        {
            InitializeComponent();
            //this.FormBorderStyle = FormBorderStyle.None;
            //Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

        }
        #endregion

        #region Private Instance Methods
        /// <summary>
        /// Get stock quote using yahoo api
        /// </summary>
        /// <param name="symbols"></param>
        private dynamic loadQuote(string symbols)
        {
            dynamic stuff = null;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL+","+symbols+ "/" + num.Next(10000000).ToString() + URLParam);
            request.Method = "GET";
            request.ContentType = "application/json";
            request.MaximumAutomaticRedirections = 4;
            request.MaximumResponseHeadersLength = 4;

            request.Credentials = CredentialCache.DefaultCredentials;
            request.Proxy = null;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    Console.WriteLine(response.ContentLength.ToString());
                    Console.WriteLine(response.ContentType.ToString());
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                    string temp = readStream.ReadToEnd();
                    stuff = JsonConvert.DeserializeObject(temp);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error loading quote." + Environment.NewLine + ex.Message);
                Shutdown();

            }
            return stuff;

        }

        private void Shutdown()
        {
            this.Close();
        }

        private void LoadStockGrid(dynamic stuff)
        {
            double price = 0;
            double change = 0;
            double chg_percent = 0;
            string symbol = "";
            string name = "";

            try
            {
                foreach (var ticker in stuff)
                {
                    foreach (var stock in ticker.First.resources)
                    {
                        symbol = (string)stock.resource.fields.symbol;
                        name = ((string)stock.resource.fields.issuer_name).Replace("&amp;", "&");
                        price = Math.Round(Double.Parse((string)stock.resource.fields.price), 2);
                        change = Math.Round(Double.Parse((string)stock.resource.fields.change), 2);
                        chg_percent = Math.Round(Double.Parse((string)stock.resource.fields.chg_percent), 2);

                        StockSticker item = StockList.Where(x => x.Symbol == symbol).SingleOrDefault();
                        if (item != null)
                        {
                            item.Price = price;
                            item.Change = change;
                            item.ChangePercent = chg_percent;
                        }
                        else
                        {
                            StockList.Add(new StockSticker((string)stock.resource.fields.ts, symbol,
                                name, price, change, chg_percent));
                        }
                    }

                }
            }
            catch (Exception ex)
            {   
                //log to a file as well
                MessageBox.Show("Unexpected error loading stock details." + Environment.NewLine + ex.Message);
                Shutdown();
            }

        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show("Desktop Portfolio Manager is already running.");
                Close();
            }

            grdPortfolio.AutoGenerateColumns = false;

            //Timer to refresh the data every second
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += new EventHandler(refreshTimer_Tick);

            //Load Main Index like Dow Jones, S&P etc
            LoadStockGrid((loadQuote("^DJI,^IXIC,^GSPC")));

            //Update major index like dow jones, nasdaq etc
            LoadIndex();

            //Load all portfolio list for authenticated user
            loadPortFolios();

            //Load default portfolio 
            loadPortfolio(btnPortfolio.Text);

            grdListPortFolio.Location = new Point(-292, 67);
            //select the first asset as default
            //grdStockSticker.Rows[0].Selected = true;

            //load day chart for default stock
            string symbol = grdPortfolio.Rows[0].Cells["title"].Value.ToString();
            LoadChart(symbol.Substring(0, symbol.IndexOf(Environment.NewLine)), 0);

            //Load news for default stock
            loadNews(symbol.Substring(0, symbol.IndexOf(Environment.NewLine)));

            //Bind grid stock to binding source
            this.bsSticker.DataSource = StockList;
            this.grdStockSticker.DataSource = this.bsSticker;
            bsSticker.ResetBindings(false);

            //start async worker thread to update stock list
            bwasyncUpdate = new BackgroundWorker();
            bwasyncUpdate.DoWork += new DoWorkEventHandler(bwasyncUpdate_DoWork);
            bwasyncUpdate.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwasyncUpdate_RunWorkerCompleted);
            bwasyncUpdate.WorkerSupportsCancellation = true;
            bwasyncUpdate.WorkerReportsProgress = true;

            LoadGrandTotals();
            //Timer to refresh the stock list every second
            refreshTimer.Start();
        }

        private void refreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                bwasyncUpdate.RunWorkerAsync();
            }
            catch (Exception)
            {
                //log to a file    
            }
        }

        private void refreshStockList(dynamic stuff)
        {
            LoadStockGrid(stuff);
        }

        private void bwasyncUpdate_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            dynamic stuff = (dynamic)e.Result;
            refreshStockList(stuff);
            LoadIndex();
            LoadGrandTotals();
        }

        private void LoadGrandTotals()
        {
            double totalMarket = grdPortfolio.Rows.Cast<DataGridViewRow>()
                .Sum(t => Double.Parse(t.Cells[7].Value.ToString()));
            lblGrandMarketValue.Text = String.Format("{0:C2}", totalMarket);

            double totalCost = grdPortfolio.Rows.Cast<DataGridViewRow>()
            .Sum(t => Double.Parse((t.Cells[6].Value).ToString()));

            lblGrandCost.Text = String.Format("{0:C2}", totalCost);
            lblGainLossValue.Text = string.Format("{0:C2}", (totalMarket - totalCost));

            if ((totalMarket - totalCost) < 0)
            {
                lblGainLossValue.BackColor = Color.Red;
            }
            else if((totalMarket - totalCost) > 0)
            {
                lblGainLossValue.BackColor = Color.LightGreen;
            }
        }

        private void bwasyncUpdate_DoWork(object sender, DoWorkEventArgs e)
        {
            string symbols = "";
            foreach (StockSticker stock in StockList)
            {
                symbols += stock.Symbol + ",";
            }

            symbols = symbols.Substring(0, symbols.Length - 1);
            dynamic stuff = loadQuote(symbols);
            e.Result = stuff;
        }


        private void btnAdd_Click(object sender, EventArgs e)
        {
            var item = StockList.Where(x => x.Symbol == txtSymbols.Text.ToUpper()).SingleOrDefault();
            if (item == null)
            {
                LoadStockGrid(loadQuote(txtSymbols.Text));
            }
        }


        private void grdStockSticker_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            if (grdStockSticker.Rows[e.RowIndex].Cells["Stock"].Value == null) return;
            string symbol = grdStockSticker.Rows[e.RowIndex].Cells["Stock"].Value.ToString();
            symbol = symbol.Substring(0, symbol.IndexOf(Environment.NewLine));
            LoadChart(symbol,0);
            loadNews(symbol);
        }

        /// <summary>
        /// Load Chart for selected stock
        /// </summary>
        /// <param name="rowIndex"></param>
        /// <param name="chartindex"></param>
        private void LoadChart(string symbol,byte chartindex)
        {
            try
            {
                lblCompany.Text = symbol;
                picDayChart.Load(chartDayURL[chartindex] + symbol);
            }
            catch (Exception ex)
            {
                //log to a file as well
                MessageBox.Show("Unexpected error loading chart." + Environment.NewLine + ex.Message);
                Shutdown();
            }
        }

        private void charOption_CheckedChanged(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection row = grdStockSticker.SelectedRows;
            RadioButton option = (RadioButton)sender;
            LoadChart(lblCompany.Text, byte.Parse(option.Tag.ToString()));
        }

        /// <summary>
        /// Load Index information
        /// </summary>
        private void LoadIndex()
        {

            try
            {
                StockSticker indexItem = StockList.Where(x => x.Symbol.Equals("^DJI")).SingleOrDefault();
                if (indexItem != null)
                {
                    lblIndex1Value.Text = String.Format("{0:F2}", indexItem.Price);
                    lblChange1.Text = String.Format("{0:F2}", indexItem.Change);
                    if (indexItem.Change < 0)
                    {
                        lblChange1.BackColor = Color.Red;
                    }
                    else if (indexItem.Change > 0)
                    {
                        lblChange1.BackColor = Color.LightGreen;
                    } 
                }

                indexItem = StockList.Where(x => x.Symbol.Equals("^IXIC")).SingleOrDefault();
                if (indexItem != null)
                {
                    lblIndex2Value.Text = String.Format("{0:F2}", indexItem.Price);
                    lblChange2.Text = String.Format("{0:F2}", indexItem.Change);
                    if (indexItem.Change < 0)
                    {
                        lblChange2.BackColor = Color.Red;
                    }
                    else if (indexItem.Change > 0)
                    {
                        lblChange2.BackColor = Color.LightGreen;
                    } 
                }

                indexItem = StockList.Where(x => x.Symbol.Equals("^GSPC")).SingleOrDefault();

                if (indexItem != null)
                {
                    lblIndex3Value.Text = String.Format("{0:F2}", indexItem.Price);
                    lblChange3.Text = String.Format("{0:F2}", indexItem.Change);
                    if (indexItem.Change < 0)
                    {
                        lblChange3.BackColor = Color.Red;
                    }
                    else if (indexItem.Change > 0)
                    {
                        lblChange3.BackColor = Color.LightGreen;
                    } 
                }
            }
            catch (Exception ex)
            {
                //log to a file as well
                MessageBox.Show("Unexpected error loading index details." + Environment.NewLine + ex.Message);
                Shutdown();
            }

        }

        /// <summary>
        /// Load news for selected stock using yahoo api
        /// </summary>
        /// <param name="rowIndex"></param>
        private void loadNews(string symbol)
        {
            string title = "";
            string link = "";
            grdNews.Rows.Clear();
            try
            {
                using (XmlReader reader = XmlReader.Create(newsURL + symbol + newsURLParam))
                {
                    while (reader.ReadToFollowing("item"))
                    {
                        reader.ReadToFollowing("title");
                        title = reader.ReadElementContentAsString();
                        link = reader.ReadElementString("link");
                        grdNews.Rows.Add(title, link);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error loading news." + Environment.NewLine + ex.Message);
                Shutdown();
            }
        }

        private void loadPortFolios()
        {
            Button[] btnPortfolios = new Button[100];
            try
            {
                using (XmlReader reader = XmlReader.Create("Username.Portfolios.xml"))
                {
                    while (reader.ReadToFollowing("portfolio"))
                    {
                        reader.MoveToFirstAttribute();

                        if (!String.IsNullOrEmpty(reader.Value))
                        {
                            grdListPortFolio.Rows.Add(reader.Value);
                        }
                    }
                }

                btnPortfolio.Text = grdListPortFolio.Rows[0].Cells[0].Value.ToString();

            }
            catch (Exception ex)
            {
                //log to a file as well
                MessageBox.Show("Unexpected error loading portfolios." + Environment.NewLine + ex.Message);
                Shutdown();
            }
          
        }

        private void loadPortfolio(string name)
        {
            string symbol = "";
            DateTime tradedate;
            long shares;
            double costbasis;
            string xmlFile = "Username." + name + ".xml";
            PortFolioList.Clear();
            try
            {
                using (XmlReader reader = XmlReader.Create(xmlFile))
                {
                    while (!reader.EOF)
                    {
                        while (reader.ReadToFollowing("stock"))
                        {
                            reader.ReadToFollowing("symbol");
                            symbol = reader.ReadElementContentAsString();
                            tradedate = Convert.ToDateTime(reader.ReadElementString("tradedate"));
                            shares = long.Parse(reader.ReadElementString("shares"));
                            costbasis = Convert.ToDouble(reader.ReadElementString("costbasis"));
                            StockSticker item = StockList.Where(x => x.Symbol == symbol.ToUpper()).SingleOrDefault();
                            if (item == null)
                            {
                                LoadStockGrid(loadQuote(symbol));
                                item = StockList.Where(x => x.Symbol == symbol.ToUpper()).SingleOrDefault();
                            }
                            PortFolioList.Add(new PortFolio(symbol, tradedate, shares, costbasis, item));

                        }

                    }
                    this.bsPortfolio.DataSource = PortFolioList;
                    this.grdPortfolio.DataSource = bsPortfolio;
                    bsPortfolio.ResetBindings(false);
                    btnPortfolio.Text = name;
                }
            }
            catch (Exception ex)
            {
                //log to a file as well
                MessageBox.Show("Unexpected error loading portfolio." + Environment.NewLine + ex.Message);
                Shutdown();
            }
        }

        private void grdNews_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            Process.Start(grdNews.Rows[e.RowIndex].Cells[1].Value.ToString());
        }

        private void grdPortfolio_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (!(e.RowIndex == -1 || e.ColumnIndex == 0 || e.ColumnIndex == 2 || e.ColumnIndex == 8 || e.ColumnIndex == 9)) return;

                if (e.RowIndex == -1)
                {
                    e.Graphics.FillRectangle(GradientColor(Gradients.Orange, e.CellBounds), e.CellBounds.X - 1, e.CellBounds.Y - 1, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
                }
                else
                {
                    if (e.ColumnIndex == 0)
                    {
                        e.Graphics.FillRectangle((GradientColor(Gradients.Blue, e.CellBounds)), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                        e.PaintContent(e.ClipBounds);
                        e.Handled = true;
                    }
                    if (grdPortfolio.Rows[e.RowIndex].Cells["ProfitLoss"].Value == null) return;

                    if (grdPortfolio.Rows[e.RowIndex].Cells["Change"].Value == null) return;

                    if (e.ColumnIndex == 2 || e.ColumnIndex == 8)
                    {
                        if (Double.Parse(grdPortfolio.Rows[e.RowIndex].Cells["Change"].Value.ToString()) < 0)
                        {
                            e.Graphics.FillRectangle((GradientColor(Gradients.Red, e.CellBounds)), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                        }
                        else if (Double.Parse(grdPortfolio.Rows[e.RowIndex].Cells["Change"].Value.ToString()) > 0)
                        {
                            e.Graphics.FillRectangle(GradientColor(Gradients.Green, e.CellBounds), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                        }
                    }
                    else if (e.ColumnIndex == 9)
                    {
                        if (Double.Parse(grdPortfolio.Rows[e.RowIndex].Cells["ProfitLoss"].Value.ToString()) < 0)
                        {
                            e.Graphics.FillRectangle(GradientColor(Gradients.Red, e.CellBounds), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);

                        }
                        else if (Double.Parse(grdPortfolio.Rows[e.RowIndex].Cells["ProfitLoss"].Value.ToString()) > 0)
                        {
                            e.Graphics.FillRectangle(GradientColor(Gradients.Green, e.CellBounds), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                        }
                    }
                }
                e.PaintContent(e.ClipBounds);
                e.Handled = true;
            }
            catch (Exception)
            {
                //log to a file
            }      
        }

        private static LinearGradientBrush GradientColor(Gradients color, Rectangle rect)
        {
            Color c1;
            Color c2;
            Color c3;
            switch (color)
            {
                case Gradients.Green:
                    c1 = Color.FromArgb(255, 0, 100, 0);
                    c2 = Color.FromArgb(255, 50, 205, 50);
                    c3 = Color.FromArgb(255, 0, 255, 0);
                    break;
                case Gradients.Blue:
                    c1 = Color.FromArgb(255, 0,255, 255);
                    c2 = Color.FromArgb(255, 0, 0, 205);
                    c3 = Color.FromArgb(255, 0, 0, 139);
                    break;
                case Gradients.Red:
                    c1 = Color.FromArgb(255, 128, 0, 0);
                    c2 = Color.FromArgb(255, 139, 0, 0);
                    c3 = Color.FromArgb(255, 255, 0, 0);
                    break;
                case Gradients.SlateGray:
                    c1 = Color.FromArgb(255, 112, 128, 144);
                    c2 = Color.FromArgb(255, 32, 178, 178);
                    c3 = Color.FromArgb(255, 112, 128, 144);
                    break;
                case Gradients.Orange:
                    c1 = Color.FromArgb(255, 210, 105, 30);
                    c2 = Color.FromArgb(255, 139, 69, 19);
                    c3 = Color.FromArgb(255, 205, 133, 63);
                    break;
                default:
                    c1 = Color.FromArgb(255, 169, 169, 169);
                    c2 = Color.FromArgb(255, 105, 105, 105);
                    c3 = Color.FromArgb(255, 0, 0, 0);
                    break;
            }

            LinearGradientBrush br = new LinearGradientBrush(rect, c1, c3, 90, true);
            ColorBlend cb = new ColorBlend();
            cb.Positions = new[] { 0, (float)0.5, 1 };
            cb.Colors = new[] { c1, c2, c3 };
            br.InterpolationColors = cb;
            return br;
        }

        private void mainform_Paint(object sender, PaintEventArgs e)
        {

            e.Graphics.FillRectangle(GradientColor(Gradients.SlateGray,this.ClientRectangle),this.ClientRectangle);

        }

        private void pnlIndex_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.FillRectangle(GradientColor(Gradients.Grey, e.ClipRectangle),e.ClipRectangle);
        }

        private void btnPortfolio_Click(object sender, EventArgs e)
        {
            ShowHidePortfolioList();
        }

        private void ShowHidePortfolioList()
        {
            if (grdListPortFolio.Location.X >= 12)
            {
                while (grdListPortFolio.Location.X > -292)
                {
                    grdListPortFolio.Location = new Point(grdListPortFolio.Location.X - 1, grdListPortFolio.Location.Y);
                }
            }
            else
            {
                while (grdListPortFolio.Location.X < 12)
                {
                    grdListPortFolio.Location = new Point(grdListPortFolio.Location.X + 1, grdListPortFolio.Location.Y);
                }
            }
        }

        private void grdListPortFolio_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex == -1) return;
            e.Graphics.FillRectangle((GradientColor(Gradients.Grey, e.CellBounds)), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            e.PaintContent(e.ClipBounds);
            e.Handled = true;
        }

        private void grdListPortFolio_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            btnPortfolio.Text = grdListPortFolio.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
            loadPortfolio(btnPortfolio.Text);
            ShowHidePortfolioList();
        }

        private void grdPortfolio_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex != 0) return;
            string symbol = grdPortfolio.Rows[e.RowIndex].Cells["title"].Value.ToString();
            loadNews(symbol.Substring(0, symbol.IndexOf(Environment.NewLine)));
            LoadChart(symbol.Substring(0, symbol.IndexOf(Environment.NewLine)), 0);
            grdStockSticker.ClearSelection();
        }

        private void grdStockSticker_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (!(e.RowIndex >= 0 && e.ColumnIndex == 2)) return;

            try
            {
                if (grdStockSticker.Rows[e.RowIndex].Cells["TodayChange"].Value == null) return;

                string priceChange = grdStockSticker.Rows[e.RowIndex].Cells["TodayChange"].Value.ToString();
                if (e.ColumnIndex == 2)
                {
                    if (Double.Parse(priceChange) < 0)
                    {
                        e.Graphics.FillRectangle((GradientColor(Gradients.Red, e.CellBounds)), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                    }
                    else if (Double.Parse(priceChange) > 0)
                    {
                        e.Graphics.FillRectangle(GradientColor(Gradients.Green, e.CellBounds), e.CellBounds.X + 1, e.CellBounds.Y + 1, e.CellBounds.Width - 1, e.CellBounds.Height - 2);
                    }
                }

                e.PaintContent(e.ClipBounds);
                e.Handled = true;
            }
            catch (Exception)
            {

            }
        }

        #endregion


    }
}
