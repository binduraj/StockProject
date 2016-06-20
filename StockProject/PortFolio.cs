using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StockProject 
{
    public class PortFolio : INotifyPropertyChanged
    {
        #region Public Properties
        private string _symbol;
        public string Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }

        private DateTime _tradedate;
        public DateTime TradeDate
        {
            get { return _tradedate; }
            set { _tradedate = value; }
        }

        private long _shares;
        public long Shares
        {
            get { return _shares; }
            set { _shares = value; }
        }

        private double _costbasis;
        public double CostBasis
        {
            get { return _costbasis; }
            set { _costbasis = value; }
        }

        private double _totalcost;
        public double Totalcost
        {
            get { return _totalcost; }
            set { _totalcost = value; }
        }

        private StockSticker _sticker;
        public StockSticker Sticker
        {
            get { return _sticker; }
            set
            {
                _sticker = value;
                if (_sticker.Price != value.Price)
                {
                    NotifyPropertyChanged("MarketPrice");
                    NotifyPropertyChanged("ChangeInPrice");
                }
            }
        }

        public double MarketPrice
        {
            get { return Sticker.Price; }
        }

        public double MarketValue
        {
            get { return Sticker.Price * Shares; }
        }


        public string ChangeInPrice
        {
            get { return Sticker.ChangeInPrice; }
        }

        public double change
        {
            get { return Sticker.Change; }
        }

        public double GainLossToday
        {
            get { return Sticker.Change * Shares; }
        }

        public double UnrealGainLoss
        {
            get { return MarketValue-Totalcost; }
        }

        public double UnrealGainLossPerc
        {
            get { return (((MarketValue - Totalcost)/ MarketValue) * 100); }
        }

        public string description
        {
            get { return Sticker.description; }
        }

        public string UnrealGainLossDisplay
        {
            get { return String.Format("{0:C2} \n (% {1:F2})",UnrealGainLoss,UnrealGainLossPerc);}
        }
        #endregion

        #region Public Constructor
        public PortFolio(string symbol, DateTime tradedate,long shares,double costbasis,StockSticker sticker)
        {
            this.TradeDate = tradedate;
            this.Symbol = symbol;
            this.Shares = shares;
            this.CostBasis = costbasis;
            this.Totalcost = shares * costbasis;
            this.Sticker = sticker;
            this.Sticker.PropertyChanged += new PropertyChangedEventHandler(Sticker_PropertyChanged);
        }


        #endregion
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        void Sticker_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyPropertyChanged("Price");
            NotifyPropertyChanged("ChangePercent");
        }

        #endregion
    }
}
