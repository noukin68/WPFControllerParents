using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Classes
{
    public class RequestResponse
    {
        public string uid { get; set; }
        public int timeInSeconds { get; set; }
        public int totalSeconds { get; set; }
        public string subject { get; set; }
        public int grade { get; set; }
    }
}
