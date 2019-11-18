using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading;
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
        private const string CfgCsvFolderName = "csvFolder";
        private const string DefaultCsvFolder = "extractions";
        private const string CfgFreqName = "extractFrequency";
        private const int DefaultFreq = 5;
        private const string CsvFileHeader = "LocalTime;Volume";
        private readonly int  _serviceErrorDelay;

        /// <summary>
        ///     Trading service
        /// </summary>
        private readonly TradingService _tradingService;

        /// <summary>
        ///     CSV files writer
        /// </summary>
        private readonly ICsvWriter _csvWriter;

        /// <summary>
        ///     Culture for numbers output
        /// </summary>
        private readonly CultureInfo _ukCulture;

        /// <summary>
        ///     Extract timer
        /// </summary>
        private readonly System.Timers.Timer _timer;

        /// <summary>
        ///     Diagnostic output writer
        /// </summary>
        private readonly StreamWriter _logger;

        /// <summary>
        ///     Paused extracting
        /// </summary>
        private bool _paused;

        /// <summary>
        ///     Test constructor
        /// </summary>
        /// <param name="csvWriter">Csv writer interface</param>
        /// <param name="repeatDelay">Repeat delay in case of TradingService exception</param>
        internal Extractor(ICsvWriter csvWriter, int repeatDelay)
        {
            _paused = false;
            _ukCulture = CultureInfo.GetCultureInfo("en-GB");
            _serviceErrorDelay = repeatDelay;

            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.log");
            _logger = new StreamWriter(logFile, true);

            _csvWriter = csvWriter;

            try
            {                
                _tradingService = new TradingService();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        /// <summary>
        ///     Regular constructor
        /// </summary>
        public Extractor()
        {
            _paused = false;
            _ukCulture = CultureInfo.GetCultureInfo("en-GB");
            _serviceErrorDelay = 5 * 1000; //repeat delay in case of TradingService exception

            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.log");
            _logger = new StreamWriter(logFile, true);

            //System.Diagnostics.Debugger.Launch();

            //read app.config
            //csv folder
            var folder = ConfigurationManager.AppSettings[CfgCsvFolderName];
            if (string.IsNullOrEmpty(folder))
            {
                Log("Csv folder is not set or empty. Using default value");
                folder = DefaultCsvFolder;
                AddOrUpdateAppSettings(CfgCsvFolderName, DefaultCsvFolder);
            }

            //extract frequency
            if (!int.TryParse(ConfigurationManager.AppSettings[CfgFreqName], out int freq))
            {
                Log("Extraction frequency is not set or has invalid format. Using default value");
                freq = DefaultFreq;
                AddOrUpdateAppSettings(CfgFreqName, DefaultFreq.ToString(_ukCulture));
            }

            _timer = new System.Timers.Timer();
            _timer.Interval = freq * 60 * 1000;

            try
            {
                _csvWriter = new CsvWriter(folder, CsvFileHeader);
                _tradingService = new TradingService();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        /// <summary>
        ///     Start periodical extract routine
        /// </summary>
        public async void Start()
        {
            if (_timer == null)
                return; //just in case
            Log("Started");
            await Extract();
            _timer.Elapsed += async (o, i) => { await Extract(); };
            _timer.Start();
        }

        /// <summary>
        ///     Perform extraction of trading data, write results in csv
        /// </summary>
        internal async Task Extract()
        {
            if (_paused)
                return;
            var now = DateTime.Now;
            IEnumerable<Trade> trades = null;
            try
            {
               trades = await _tradingService.GetTradesAsync(now);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                //cant miss extract
                Log($"Repeating extract in {_serviceErrorDelay} ms");
                var worker = new BackgroundWorker();
                worker.DoWork += async delegate
                {
                    Thread.Sleep(_serviceErrorDelay);
                    await Extract();
                    worker.Dispose();
                };
                worker.RunWorkerAsync();
            }

            if (trades == null)
                return;

            try
            {
                var aggregations = AggregateTrades(trades);
                if (aggregations.Count != 24)
                    Log("Amount of periods is not 24");
                var lines = PrepareOutput(aggregations);
                //write to csv
                _csvWriter.Dump($"{now.ToString("yyyyMMdd_HHmm")}.csv", lines);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        /// <summary>
        ///     Aggregate trades
        /// </summary>
        /// <param name="trades">Trades from trading service</param>
        /// <returns>Results(Period -> aggreated volume)</returns>
        internal SortedDictionary<int, double> AggregateTrades(IEnumerable<Trade> trades)
        {
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
            return periodVolumes;
        }

        /// <summary>
        ///     Prepare strings for output
        /// </summary>
        /// <param name="aggregations">Trades aggregated by periods</param>
        /// <returns>Csv lines</returns>
        internal IList<string> PrepareOutput(SortedDictionary<int, double> aggregations)
        { 
            var lines = new List<string>();
            var ts = new DateTime(2000, 1, 1).AddHours(-1); //start at 23:00 previous day
            foreach (var p in aggregations.Keys)
            {
                lines.Add($"{ts.ToString("HH:mm")};{aggregations[p].ToString(_ukCulture)}");
                ts = ts.AddHours(1);
            }

            return lines;
        }

        /// <summary>
        ///     Update app.config value
        /// </summary>        
        private void AddOrUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {
                Log("Error writing app settings");
            }
        }

        /// <summary>
        ///     Write a message to log file
        /// </summary>
        private void Log(string msg)
        {
            _logger.WriteLine($"{DateTime.Now.ToString(_ukCulture)} | {msg}");
            _logger.Flush();
        }

        /// <summary>
        ///     Stop extractor and release resourses
        /// </summary>
        public void Stop()
        {
            Log("Stopping...");
            _timer?.Stop();
            _logger?.Close();
        }

        /// <summary>
        ///     Pause extractions routine
        /// </summary>
        public void Pause()
        {
            _paused = true;
            Log("Paused");
        }
        
        /// <summary>
        ///     Continue extractions routine
        /// </summary>
        public void Continue()
        {
            _paused = false;
            Log("Continued");
        }
    }
}