using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkService.Model
{
    using System.ComponentModel;

    namespace NetworkService.Model
    {
        public class ServerEntity : INotifyPropertyChanged
        {
            private double _lastMeasuredValue;
            private bool _isOutOfRange;

            public int Id { get; set; }
            public string Name { get; set; }
            public string IpAddress { get; set; }
            public ServerType Type { get; set; }

            // Validne vrednosti: 45% – 75%
            public static double MinValue => 45.0;
            public static double MaxValue => 75.0;

            public double LastMeasuredValue
            {
                get => _lastMeasuredValue;
                set
                {
                    _lastMeasuredValue = value;
                    IsOutOfRange = value < MinValue || value > MaxValue;
                    OnPropertyChanged(nameof(LastMeasuredValue));
                }
            }

            public bool IsOutOfRange
            {
                get => _isOutOfRange;
                set
                {
                    _isOutOfRange = value;
                    OnPropertyChanged(nameof(IsOutOfRange));
                    OnPropertyChanged(nameof(StatusImagePath));
                }
            }

            public string StatusImagePath =>
                IsOutOfRange ? "/Images/status_alarm.png" : "/Images/status_ok.png";

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
