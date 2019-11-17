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
        private const string CfgFolderName = "csvFolder";
        private const string DefaultFolder = "extractions";
        private const string CfgFreqName = "extractFrequency";
        private const int DefaultFreq = 5;
        private const string CsvFileHeader = "LocalTime;Volume";
        private const int ServiceErrorDelay = 5 * 1000;

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
        private readonly System.Timers.Timer _timer;

        /// <summary>
        ///     Diagnostic output writer
        /// </summary>
        private readonly StreamWriter _logger;

        public Extractor()
        {
            _ukCulture = CultureInfo.GetCultureInfo("en-GB");
            _logger = new StreamWriter("diagnostics.log", true);

            //read app config
            var folder = ConfigurationManager.AppSettings[CfgFolderName];
            if (string.IsNullOrEmpty(folder))
            {
                Log("Csv folder is not set or empty. Using default value");
                folder = DefaultFolder;
                AddOrUpdateAppSettings(CfgFolderName, DefaultFolder);
            }
            if (!int.TryParse(ConfigurationManager.AppSettings[CfgFreqName], out int freq))
            {
                Log("Extraction frequency is not set or has invalid format. Using default value");
                freq = DefaultFreq;
                AddOrUpdateAppSettings(CfgFreqName, DefaultFreq.ToString(_ukCulture));
            }

            try
            {
                _csvWriter = new CsvWriter(folder, CsvFileHeader);
                _tradingService = new TradingService();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            
            _timer = new System.Timers.Timer();
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
            IEnumerable<Trade> trades = null;
            try
            {
               trades = await _tradingService.GetTradesAsync(now);
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                //cant miss extract
                Log($"Repeating extract in {ServiceErrorDelay} ms");
                var worker = new BackgroundWorker();
                worker.DoWork += async delegate
                {
                    Thread.Sleep(ServiceErrorDelay);
                    await Extract();
                    worker.Dispose();
                };
                worker.RunWorkerAsync();
            }

            if (trades == null)
                return;

            try
            {
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
                    Log("Amount of periods is not 24");
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
            catch (Exception ex)
            {
                Log(ex.Message);
            }
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

        ~Extractor()
        {
            _timer?.Stop();
            _logger?.Close();
        }
    }
}