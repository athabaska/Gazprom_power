using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Threading.Tasks;
using System.Timers;
using TradingPlatform;

namespace PowerPosition.Core
{
    /// <summary>
    ///     Trading platform data extractor
    /// </summary>
    public class Extractor
    {
        private const string CfgFolderName = "csvFolder";
        private const string CfgFreqName = "extractFrequency";
        private const string CsvFileHeader = "LocalTime;Volume";

        /// <summary>
        ///     Trading service
        /// </summary>
        private readonly TradingService _tradingService;

        /// <summary>
        ///     CSV files writer
        /// </summary>
        private readonly CsvWriter _csvWriter;

        /// <summary>
        ///     Culture for numbers output
        /// </summary>
        private readonly CultureInfo _ukCulture;

        /// <summary>
        ///     Extract timer
        /// </summary>
        private readonly Timer _timer;

        public Extractor()
        {
            var folder = ConfigurationManager.AppSettings[CfgFolderName];
            if (!int.TryParse(ConfigurationManager.AppSettings[CfgFreqName], out int freq))
            {

            }
            _csvWriter = new CsvWriter(folder, CsvFileHeader);
            _tradingService = new TradingService();
            _ukCulture = CultureInfo.GetCultureInfo("en-GB");
            _timer = new Timer();
            _timer.Interval = freq * 60 * 1000;
        }

        /// <summary>
        ///     Start periodical extract routine
        /// </summary>
        public async void Start()
        {
            await Extract();
            _timer.Elapsed += async (o, i) => { await Extract(); };
        }

        /// <summary>
        ///     Perform extraction of trading data, write results in csv
        /// </summary>
        private async Task Extract()
        {
            var now = DateTime.Now;
            var trades = await _tradingService.GetTradesAsync(now);

            //aggregate volumes by period
            var periodVolumes = new SortedDictionary<int, double>();
            foreach (var t in trades)
            {
                foreach (var period in t.Periods)
                {
                    if (!periodVolumes.TryGetValue(period.Period, out double vol))
                    {
                        periodVolumes[period.Period] = period.Volume;
                    }
                    else
                    {
                        periodVolumes[period.Period] = vol + period.Volume;
                        //todo worry about precision?
                    }
                }
            }

            if (periodVolumes.Count != 24)
            {
                //todo wtf log
            }

            //prepare csv lines
            var lines = new List<string>();
            var ts = new DateTime(now.Year, now.Month, now.Day).AddHours(-1); //start at 23:00 previous day
            foreach (var p in periodVolumes.Keys)
            {
                lines.Add($"{ts.ToString("HH:mm")};{periodVolumes[p].ToString(_ukCulture)}");
                ts = ts.AddHours(1);
            }

            //write to csv
            _csvWriter.Dump($"{now.ToString("yyyyMMdd_HHmm")}.csv", lines);
        }        
    }
}