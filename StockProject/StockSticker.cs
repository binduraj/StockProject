using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StockProject
{
    public class StockSticker : INotifyPropertyChanged
    {
        private string _ID;
        public string ID
        {
            get { return _ID; }
            set { _ID = value; }
        }

        private string _symbol;
        public string Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }


 
        private double _price;
        public double Price
        {
            get { return _price; }
            set {
                if (value != _price)
                {
                    _price = value;
                    NotifyPropertyChanged("Price");
                }
            
            }
        }

        private double _change;
        public double Change
        {
            get { return _change; }
            set {
                if (value != _change)
                {
                    NotifyPropertyChanged("Change");
                    NotifyPropertyChanged("ChangeInPrice");
                    _change = value;
                }
            }
        }

        private double _changepercent;
        public double ChangePercent
        {
            get { return _changepercent; }
            set {
                if (value != _changepercent)
                {
                    _changepercent = value;
                    NotifyPropertyChanged("ChangePercent");
                }
            }
        }

        public string ChangeInPrice
        {
            get { return String.Format("{0:C2} \n (% {1:F2})", Change, ChangePercent); }
        }

        public string description
        {
            get { return Symbol + Environment.NewLine + Name;}
        }

        public StockSticker() { }
        public StockSticker(string ID, string Symbol, string name, double Price, double Change, double ChangePercent)
        {
            this.ID = ID;
            this.Symbol = Symbol;
            this.Name = name;
            this.Price = Price;
            this.Change = Change;
            this.ChangePercent = ChangePercent;

            NotifyPropertyChanged("");
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this,new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
